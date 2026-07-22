namespace MantisBTMailTask;

public sealed class MailConfiguration
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public bool ForceRemoveOnFailure { get; set; }
}