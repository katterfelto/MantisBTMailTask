
using MailKit.Security;

namespace MantisBTMailTask
{
    public sealed class MailConfiguration
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public SecureSocketOptions SecureSocketOptions { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string From { get; set; }

        public void CheckOptionalValues()
        {
            Host = CheckString(Host, "localhost");
            Port = CheckInt(Port, 25);
        }

        private string CheckString(string field, string value)
        {
            if (string.IsNullOrEmpty(field))
            {
                return value;
            }
            else
            {
                return field;
            }
        }

        private int CheckInt(int? field, int value)
        {
            if (field == null)
            {
                return value;
            }
            else
            {
                return (int)field;
            }
        }
    }
}