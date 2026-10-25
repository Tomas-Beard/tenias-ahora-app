namespace TenisAhora.Infrastructure.Authentication;

public class SupabaseAuthOptions
{
    public const string SectionName = "Supabase";

    public string ProjectUrl { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = "authenticated";
    public string JwtSecret { get; set; } = string.Empty;
    public bool AllowLegacyGoTokens { get; set; } = true;
}
