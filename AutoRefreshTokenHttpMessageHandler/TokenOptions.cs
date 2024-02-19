namespace AutoRefreshTokenHttpMessageHandler;

public class TokenOptions
{
    public string TokenEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string Scope { get; set; } = "openid";
    public string? Audience { get; set; } = null;
    public TimeSpan ExpiryBuffer { get; set; } = TimeSpan.FromMinutes(1);
}