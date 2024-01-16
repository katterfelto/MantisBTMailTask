

namespace MantisBTMailTask
{
    public sealed class MailConfiguration
    {
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string From { get; set; }
    }
}