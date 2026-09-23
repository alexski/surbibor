namespace Surbibor.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Surbibor";
    public string Audience { get; set; } = "Surbibor";
    public int ExpiryDays { get; set; } = 7;
}
