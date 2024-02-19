using AutoRefreshTokenHttpMessageHandler.Resources;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AutoRefreshTokenHttpMessageHandler;

public class TokenAuthenticationService(IOptions<TokenOptions> options, HttpClient httpClient, TimeProvider? timeProvider = null) : ITokenAuthenticationService
{
    private readonly TokenOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly HttpClient _httpclient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly TimeProvider _timeprovider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, AsyncLazy<Token>> _tokens = new();    // This will only ever contain one token, but we use a dictionary to make it thread-safe
    private const string _tokenkey = "default";

    private async Task<Token> RetrieveTokenAsync(string grantType, CancellationToken cancellationToken = default)
    {
        var httprequest = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["grant_type"] = grantType,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["username"] = _options.Username,
                ["password"] = _options.Password,
                ["scope"] = _options.Scope
            })
        };

        var result = await _httpclient.SendAsync(httprequest, cancellationToken);
        try
        {
            result.EnsureSuccessStatusCode();
            return Token.FromTokenResponse(
                await result.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken) ?? throw new ExtendedHttpRequestException(Translations.INVALID_OR_UNEXPECTED_RESPONSE, null, result.StatusCode),
                _timeprovider.GetUtcNow()
            );
        }
        catch (HttpRequestException ex)
        {
            var err = await result.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken) ?? throw new ExtendedHttpRequestException(Translations.INVALID_OR_UNEXPECTED_RESPONSE, null, result.StatusCode);
            throw new ExtendedHttpRequestException($"[{err.Error}] {err.Description}", ex, result.StatusCode);
        }
    }

    public async Task<Token> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        //TODO: Check "NotBeforePolicy"

        var cacheEntry = _tokens.GetOrAdd(_tokenkey, k => new AsyncLazy<Token>(() => RetrieveTokenAsync(GetGrantType(), cancellationToken)));

        // The cache stores Task, so we need to await to get the actual token
        var token = await cacheEntry.Value.ConfigureAwait(false);
        if (IsAccessTokenExpired(token))
        {
            // If a token is expired, we should revoke it from our cache. We use TryRemove because a different caller may have gotten
            // here before us,  and we don't care about throwing in this case.
            _tokens.TryRemove(_tokenkey, out _);
            // Then we refresh the token.
            cacheEntry = _tokens.GetOrAdd(_tokenkey, k => new AsyncLazy<Token>(() => RefreshTokenAsync(token, cancellationToken)));
        }

        // We have to await again, in case the token was expired after the first await
        return await cacheEntry.Value.ConfigureAwait(false);
    }

    private async Task<Token> RefreshTokenAsync(Token token, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(token.RefreshToken) && !IsRefreshTokenExpired(token))  // Refresh token still valid? Use it.
            {
                return await RetrieveTokenAsync(GrantTypes.Refresh, cancellationToken);
            }
        }
        catch (ExtendedHttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized) { }
        // Refresh token expired or refresh failed? Try to get a new token
        return await RetrieveTokenAsync(GetGrantType(), cancellationToken).ConfigureAwait(false);
    }

    private string GetGrantType() => string.IsNullOrWhiteSpace(_options.Username) ? GrantTypes.ClientCredentials : GrantTypes.Password;
    private bool IsAccessTokenExpired(Token token) => token.Expires.Subtract(_options.ExpiryBuffer) <= _timeprovider.GetUtcNow();
    private bool IsRefreshTokenExpired(Token token) => token.RefreshExpires.Subtract(_options.ExpiryBuffer) <= _timeprovider.GetUtcNow();

    private static class GrantTypes
    {
        public const string ClientCredentials = "client_credentials";
        public const string Password = "password";
        public const string Refresh = "refresh_token";
    }

    private class AsyncLazy<T>(Func<Task<T>> taskFactory) : Lazy<Task<T>>(() => Task.Factory.StartNew(() => taskFactory()).Unwrap()) { }

    private record ErrorResponse
    (
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("error_description")] string Description
    );

    private class ExtendedHttpRequestException(string message, Exception? innerException, HttpStatusCode httpStatusCode) : HttpRequestException(message, innerException)
    {
        public HttpStatusCode StatusCode { get; private set; } = httpStatusCode;
    }
}