
namespace MantisBTMailTask
{
    public sealed class DatabaseConfiguration
    {
        public string Host { get; set; }
        public uint Port { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public void CheckOptionalValues()
        {
            Host = CheckString(Host, "localhost");
            Port = CheckUInt(Port, 3306);
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

        private uint CheckUInt(uint? field, uint value)
        {
            if ((field == null) || (field == 0))
            {
                return value;
            }
            else
            {
                return (uint)field;
            }
        }
    }
}