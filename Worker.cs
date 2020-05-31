using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using MySql.Data.MySqlClient;

namespace MantisBTMailTask
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly DatabaseConfiguration _mantisDbConfig;
        private readonly MailConfiguration _mailConfig;

        private readonly bool _runOnce;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;

            _mantisDbConfig = new DatabaseConfiguration();  
            configuration.GetSection("MantisDB").Bind(_mantisDbConfig);
            _mantisDbConfig.CheckOptionalValues();

            _mailConfig = new MailConfiguration();  
            configuration.GetSection("MailServer").Bind(_mailConfig);
            _mailConfig.CheckOptionalValues();

            _runOnce = configuration.GetValue<bool>("RunOnce");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ProcessOutstandingEmails(stoppingToken);
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
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        protected void ProcessOutstandingEmails(CancellationToken stoppingToken)
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
                oDB.Open();

                var sentIdList = SendEmailsFromDatabase(stoppingToken, oDB);

                if (sentIdList.Count > 0)
                {
                    RemoveSentEmails(stoppingToken, oDB, sentIdList);
                }
            }
        }

        protected List<long> SendEmailsFromDatabase(CancellationToken stoppingToken, MySqlConnection oDB)
        {
            List<long> sentIdList = new List<long>();
            
            string query = "SELECT `email_id`, `email`, `subject`, `body`, `submitted` FROM `mantis_email_table`";
            
            using var cmd = new MySqlCommand(query, oDB);
            using MySqlDataReader data = cmd.ExecuteReader();

            if (data.HasRows)
            {
                using (var client = new SmtpClient()) 
                {
                    client.Connect (_mailConfig.Host, _mailConfig.Port, _mailConfig.SecureSocketOptions);
                    client.Authenticate (_mailConfig.Username, _mailConfig.Password);

                    while (data.Read() && !stoppingToken.IsCancellationRequested)
                    {
                        if (SendEmail(client, data.GetString(1), data.GetString(2), data.GetString(3)))
                        {
                            sentIdList.Add(data.GetInt64(0));
                        }
                    }

                    client.Disconnect (true);
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

        protected bool SendEmail(SmtpClient client, string to, string subject, string body)
        {
            bool result = true;
            var message = new MimeMessage()
            {
                Subject = subject,
                Body = new TextPart("plain") {
                    Text = body
                }
            };
            message.From.Add(new MailboxAddress("Mantis", _mailConfig.From));
            message.To.Add(new MailboxAddress(to));
            try
            {
                client.Send(message);
            }
            catch
            {
                result = false;
            }
            return result;
        }
    }
}
