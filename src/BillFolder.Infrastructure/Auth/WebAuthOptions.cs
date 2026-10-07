namespace BillFolder.Infrastructure.Auth;

public sealed class WebAuthOptions
{
    public const string SectionName = "WebAuth";

    public string CookieName { get; init; } = "bf_refresh";

    public string[] AllowedOrigins { get; init; } = [];
}
