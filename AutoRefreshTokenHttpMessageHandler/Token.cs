namespace AutoRefreshTokenHttpMessageHandler;

public record Token(string AccessToken, DateTimeOffset Expires, string RefreshToken, DateTimeOffset RefreshExpires, string TokenType, DateTimeOffset NotBeforePolicy, string SessionState, string Scope)
{
    internal static Token FromTokenResponse(TokenResponse token, DateTimeOffset currentDateTime)
        => new(token.AccessToken, currentDateTime.AddSeconds(token.ExpiresIn), token.RefreshToken, currentDateTime.AddSeconds(token.RefreshExpiresIn), token.TokenType, currentDateTime.AddSeconds(token.NotBeforePolicy), token.SessionState, token.Scope);
}
