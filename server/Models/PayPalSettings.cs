namespace server.Models
{
    public class PayPalSettings
    {
        public string ClientId { get; set; } = null!;
        public string ClientSecret { get; set; } = null!;
        public string Mode { get; set; } = null!;
        public string BaseURL => Mode == "Live"
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";
        public string ReturnUrl { get; set; } = null!;
        public string CancelUrl { get; set; } = null!;
    }
}