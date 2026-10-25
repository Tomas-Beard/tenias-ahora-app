namespace TenisAhora.Infrastructure.Payments;

public class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public string AccessToken { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public bool SandboxMode { get; set; } = true;
    public string NotificationUrl { get; set; } = string.Empty;
    public string SuccessBackUrl { get; set; } = "http://localhost:5173/mis-reservas?mp=success";
    public string PendingBackUrl { get; set; } = "http://localhost:5173/mis-reservas?mp=pending";
    public string FailureBackUrl { get; set; } = "http://localhost:5173/mis-reservas?mp=failure";
}
