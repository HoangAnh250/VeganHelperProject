namespace VeganHelper.BLL.DTOs;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "VeganHelper.API";
    public string Audience { get; set; } = "VeganHelper.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
}
