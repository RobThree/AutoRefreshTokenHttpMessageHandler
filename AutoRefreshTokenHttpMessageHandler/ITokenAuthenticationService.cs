namespace AutoRefreshTokenHttpMessageHandler;

public interface ITokenAuthenticationService
{
    Task<Token> GetTokenAsync(CancellationToken cancellationToken = default);
}
