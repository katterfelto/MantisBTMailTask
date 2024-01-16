using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using MySql.Data.MySqlClient;

namespace MantisBTMailTask
{
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
                else
                {
                    await Task.Delay(_frequency, stoppingToken);
                }
            }
        }

        protected async Task ProcessOutstandingEmails(CancellationToken stoppingToken)
        {
            MySqlConnectionStringBuilder csBuilder = new MySqlConnectionStringBuilder()
            {
                Server = _mantisDbConfig.Host,
                Port = _mantisDbConfig.Port,
                Database = _mantisDbConfig.Database,
                UserID = _mantisDbConfig.Username,
                Password = _mantisDbConfig.Password
            };
            using (var oDB = new MySqlConnection(csBuilder.ToString()))
            {
                try
                {
                    oDB.Open();

                    var sentIdList = await SendEmailsFromDatabase(stoppingToken, oDB);

                    if (sentIdList.Count > 0)
                    {
                        RemoveSentEmails(stoppingToken, oDB, sentIdList);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "There was a problem processing the email table");
                }
            }
        }

        protected async Task<List<long>> SendEmailsFromDatabase(CancellationToken stoppingToken, MySqlConnection oDB)
        {
            List<long> sentIdList = new List<long>();
            
            string query = "SELECT `email_id`, `email`, `subject`, `body`, `submitted` FROM `mantis_email_table` ORDER BY `submitted`";
            
            using var cmd = new MySqlCommand(query, oDB);
            using MySqlDataReader data = cmd.ExecuteReader();

            if (data.HasRows)
            {
                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var options = new ClientSecretCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                };
                
                var clientSecretCredential = new ClientSecretCredential(_mailConfig.TenantId, _mailConfig.ClientId, _mailConfig.ClientSecret , options);
                
                using (var client = new GraphServiceClient(clientSecretCredential, scopes)) 
                {
                    while (data.Read() && !stoppingToken.IsCancellationRequested)
                    {
                        await SendEmail(client, data.GetString(1), data.GetString(2), data.GetString(3));
                        sentIdList.Add(data.GetInt64(0));
                    }
                }
            }

            return sentIdList;
        }

        protected bool RemoveSentEmails(CancellationToken stoppingToken, MySqlConnection oDB, List<long> sentIdList)
        {
            int count = 0;
            string query = "DELETE FROM mantis_email_table WHERE email_id = @id";

            using (var cmd = new MySqlCommand(query, oDB))
            {
                cmd.Parameters.Add("id", MySqlDbType.Int64, 10);

                foreach(long id in sentIdList)
                {
                    cmd.Parameters["id"].Value = id;
                    count += cmd.ExecuteNonQuery();
                }
            }

            return (count == sentIdList.Count);
        }

        protected async Task SendEmail(GraphServiceClient client, string to, string subject, string body)
        {
            var message = new Message()
            {
                Subject = subject,
                Body = new ItemBody
                {
                    Content = body,
                    ContentType = BodyType.Text
                },
                ToRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = to
                        }
                    }
                }
            };

            try
            {
                await client.Users[_mailConfig.From]
                    .SendMail
                    .PostAsync(new SendMailPostRequestBody
                    {
                        Message = message
                    });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was a problem sending the email");
            }
        }
    }
}
