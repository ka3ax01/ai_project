namespace BookingPlatform.Infrastructure.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "BookingPlatform";
    public string Audience { get; set; } = "BookingPlatform";
    public string Secret { get; set; } = "change_this_dev_secret_key_please";
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
    public int RefreshTokenLifetimeDays { get; set; } = 7;
}

