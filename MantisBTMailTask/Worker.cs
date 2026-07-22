using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using MySqlConnector;

namespace MantisBTMailTask;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly DatabaseConfiguration _mantisDbConfig;
    private readonly MailConfiguration _mailConfig;

    private readonly bool _runOnce;

    private readonly int _frequency;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;

        _mantisDbConfig = new DatabaseConfiguration();
        configuration.GetSection("MantisDB").Bind(_mantisDbConfig);
        _mantisDbConfig.CheckOptionalValues();

        _mailConfig = new MailConfiguration();
        configuration.GetSection("MailServer").Bind(_mailConfig);

        _runOnce = configuration.GetValue<bool>("RunOnce");

        _frequency = configuration.GetValue<int>("Frequency");
        if (_frequency == 0)
        {
            _frequency = 10000; // Every 10 seconds
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutstandingEmails(stoppingToken);
            }
            catch (MySqlException e)
            {
                _logger.LogError(e, "There was a problem with the database connection");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was a problem");
            }

            if (_runOnce)
            {
                break;
            }

            await Task.Delay(_frequency, stoppingToken);
        }
    }

    protected async Task ProcessOutstandingEmails(CancellationToken stoppingToken)
    {
        var csBuilder = new MySqlConnectionStringBuilder
        {
            Server = _mantisDbConfig.Host,
            Port = _mantisDbConfig.Port,
            Database = _mantisDbConfig.Database,
            UserID = _mantisDbConfig.Username,
            Password = _mantisDbConfig.Password
        };

        await using var oDB = new MySqlConnection(csBuilder.ToString());

        try
        {
            await oDB.OpenAsync(stoppingToken);

            var sentIdList = await SendEmailsFromDatabase(stoppingToken, oDB);

            if (sentIdList.Count > 0)
            {
                await RemoveSentEmails(stoppingToken, oDB, sentIdList);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was a problem processing the email table");
        }
    }

    protected async Task<List<long>> SendEmailsFromDatabase(CancellationToken stoppingToken, MySqlConnection oDB)
    {
        var sentIdList = new List<long>();

        const string query = "SELECT `email_id`, `email`, `subject`, `body`, `submitted` FROM `mantis_email_table` ORDER BY `submitted`";

        await using var cmd = new MySqlCommand(query, oDB);
        await using var data = await cmd.ExecuteReaderAsync(stoppingToken);

        if (data.HasRows)
        {
            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var options = new ClientSecretCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            };

            var clientSecretCredential = new ClientSecretCredential(_mailConfig.TenantId, _mailConfig.ClientId, _mailConfig.ClientSecret, options);

            using var client = new GraphServiceClient(clientSecretCredential, scopes);

            while (await data.ReadAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                var id = data.GetInt64(0);
                var sent = await SendEmail(client, data.GetString(1), data.GetString(2), data.GetString(3));
                if (sent)
                {
                    sentIdList.Add(id);
                }
                else if (_mailConfig.ForceRemoveOnFailure)
                {
                    _logger.LogWarning("Removing email {EmailId} from the queue despite send failure because ForceRemoveOnFailure is enabled", id);
                    sentIdList.Add(id);
                }
            }
        }

        return sentIdList;
    }

    protected async Task<bool> RemoveSentEmails(CancellationToken stoppingToken, MySqlConnection oDB, List<long> sentIdList)
    {
        var count = 0;
        const string query = "DELETE FROM mantis_email_table WHERE email_id = @id";

        await using var cmd = new MySqlCommand(query, oDB);
        cmd.Parameters.Add("id", MySqlDbType.Int64);

        foreach (var id in sentIdList)
        {
            cmd.Parameters["id"].Value = id;
            count += await cmd.ExecuteNonQueryAsync(stoppingToken);
        }

        return count == sentIdList.Count;
    }

    protected async Task<bool> SendEmail(GraphServiceClient client, string to, string subject, string body)
    {
        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody
            {
                Content = body,
                ContentType = BodyType.Text
            },
            ToRecipients =
            [
                new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = to
                    }
                }
            ]
        };

        try
        {
            await client.Users[_mailConfig.From]
                .SendMail
                .PostAsync(new SendMailPostRequestBody
                {
                    Message = message
                });

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was a problem sending the email");
            return false;
        }
    }
}
