namespace MantisBTMailTask;

public sealed class DatabaseConfiguration
{
    public string Host { get; set; } = "localhost";
    public uint Port { get; set; } = 3306;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public void CheckOptionalValues()
    {
        if (string.IsNullOrEmpty(Host))
        {
            Host = "localhost";
        }

        if (Port == 0)
        {
            Port = 3306;
        }
    }
}