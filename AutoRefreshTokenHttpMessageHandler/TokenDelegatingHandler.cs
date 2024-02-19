using System.Net.Http.Headers;

namespace AutoRefreshTokenHttpMessageHandler;

public class TokenDelegatingHandler(ITokenAuthenticationService tokenAuthenticationService) : DelegatingHandler
{
    private readonly ITokenAuthenticationService _authenticationservice = tokenAuthenticationService ?? throw new ArgumentNullException(nameof(tokenAuthenticationService));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", (await _authenticationservice.GetTokenAsync(cancellationToken).ConfigureAwait(false)).AccessToken);
        }
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}