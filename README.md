# ![icon.png](icon.png) AutoRefreshTokenHttpMessageHandler

This is a thread-safe implementation of a `DelegatingHandler`, available as [Nuget package](https://www.nuget.org/packages/AutoRefreshTokenHttpMessageHandler/), that automatically refreshes the access token when the access token expires whilst **not** serializing all requests. Most implementations use a lock internally which essentially makes all async actions synchronous again. This implementation only blocks during the actual refresh. Inspired by Bryan Helms' [Thread-Safe Auth Token Store Using ConcurrentDictionary and AsyncLazy](https://bryanhelms.com/2021/03/29/thread-safe-auth-token-store-using-concurrentdictionary-and-asynclazy.html).

## Quickstart

```c#
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TokenOptions>(configuration.GetRequiredSection("MyClient"))
    .AddTransient<TokenDelegatingHandler>()
    .AddHttpClient<ITokenAuthenticationService, TokenAuthenticationService>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://auth.myservice.com")).Services
```

## Custom service:
```c#
builder.Services.AddHttpClient<MyService>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://myservice.com"))
    .AddHttpMessageHandler<TokenDelegatingHandler>();

public class MyService(HttpClient client)
{
    public Task<IEnumerable<MyFoo>> GetFoos() => client.GetFromJsonAsync<IEnumerable<Foo>>("/api/v1/foos");
}
```

## ...or using [Refit](https://github.com/reactiveui/refit):
```c#
// ...or Refit:
builder.Services.AddRefitClient<IMyService>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://myservice.com"))
    .AddHttpMessageHandler<TokenDelegatingHandler>();

public interface IMyService
{
    [Get("/api/v1/foos")]
    Task<IEnumerable<Foo>> GetFooss();
}
```

## Adding [Polly](https://www.thepollyproject.org/) to the mix:
```c#
builder.Services.AddHttpClient<MyService>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://myservice.com"))
    .AddHttpMessageHandler<TokenDelegatingHandler>();
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 3))
    ).Services
```
## Add options to appsettings:

### Client credentials:
```json
{
  "MyClient": {
    "ClientId": "myclient",
    "ClientSecret": "clientsecretclientsecretclientsecret",
    "TokenEndpoint": "http://auth.myservice.com/realms/myapi/protocol/openid-connect/token"
  }
}
```

### ...or using username/password:
```json
{
  "MyClient": {
    "ClientId": "myclient",
    "ClientSecret": "clientsecretclientsecretclientsecret",
    "Username": "admin",
    "Password": "mysup3rs4f3p455w0rd",
    "TokenEndpoint": "http://auth.myservice.com/realms/myapi/protocol/openid-connect/token"
  }
}
```

## Attribution

Icon by [Freepik](https://www.freepik.com/icon/key_908229)