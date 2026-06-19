using System.Net;

namespace SportVitaal.Tests.Ui;

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> that maps request paths to canned responses,
/// so UI tests can drive <c>SportVitaalApiClient</c> without a live WebApi.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new();

    /// <summary>Registers a JSON body + status to return for requests (any verb) whose path ends with <paramref name="pathSuffix"/>.</summary>
    public StubHttpMessageHandler When(string pathSuffix, string json, HttpStatusCode status = HttpStatusCode.OK)
        => WhenGet(pathSuffix, json, status);

    /// <summary>Registers a JSON body to return for GET requests whose path ends with <paramref name="pathSuffix"/>.</summary>
    public StubHttpMessageHandler WhenGet(string pathSuffix, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[pathSuffix] = () => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        return this;
    }

    /// <summary>Registers a fixed status (no body) for requests whose path ends with <paramref name="pathSuffix"/>.</summary>
    public StubHttpMessageHandler WhenStatus(string pathSuffix, HttpStatusCode status)
    {
        _routes[pathSuffix] = () => new HttpResponseMessage(status);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        foreach (var (suffix, factory) in _routes)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(factory());
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No stub registered for {path}")
        });
    }

    public HttpClient ToHttpClient() => new(this) { BaseAddress = new Uri("https://localhost/") };
}
