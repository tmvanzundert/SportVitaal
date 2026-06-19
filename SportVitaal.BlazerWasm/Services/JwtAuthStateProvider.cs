using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace SportVitaal.BlazerWasm.Services
{
    /// <summary>
    /// Authentication state derived from the WebApi JWT. The token is persisted in the
    /// browser's sessionStorage (cleared when the tab closes) so a refresh keeps the
    /// employee signed in, and mirrored into <see cref="TokenProvider"/> so API calls
    /// stay authenticated. Replaces the Blazor Server ProtectedSessionStorage, which is
    /// unavailable in WebAssembly.
    /// </summary>
    public class JwtAuthStateProvider : AuthenticationStateProvider
    {
        private const string TokenKey = "sv_token";
        private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

        private readonly IJSRuntime _js;
        private readonly TokenProvider _tokenProvider;
        private bool _restored;

        public JwtAuthStateProvider(IJSRuntime js, TokenProvider tokenProvider)
        {
            _js = js;
            _tokenProvider = tokenProvider;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // Restore from sessionStorage once. In standalone WASM there is no prerender,
            // so JS interop is available from the first call.
            if (!_restored && string.IsNullOrWhiteSpace(_tokenProvider.Token))
            {
                try
                {
                    var stored = await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
                    if (!string.IsNullOrWhiteSpace(stored))
                        _tokenProvider.Token = stored;
                }
                catch
                {
                    // Interop unavailable; re-evaluated on the next call.
                }
                _restored = true;
            }

            var principal = string.IsNullOrWhiteSpace(_tokenProvider.Token)
                ? Anonymous
                : BuildPrincipal(_tokenProvider.Token!);
            return new AuthenticationState(principal);
        }

        /// <summary>Call after a successful login (TokenProvider.Token already set).</summary>
        public async Task MarkAuthenticatedAsync()
        {
            if (!string.IsNullOrWhiteSpace(_tokenProvider.Token))
                await _js.InvokeVoidAsync("sessionStorage.setItem", TokenKey, _tokenProvider.Token!);
            _restored = true;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public async Task LogoutAsync()
        {
            _tokenProvider.Token = null;
            await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
        }

        private static ClaimsPrincipal BuildPrincipal(string token)
        {
            var claims = ParseClaims(token, out var expiresAt);
            if (expiresAt is not null && expiresAt <= DateTimeOffset.UtcNow)
                return Anonymous;

            // Tokens from AuthController use the standard ClaimTypes URIs for name and role.
            // Identity.Name resolves to the display name claim (FullName/UserName/Email);
            // the user id stays available via the NameIdentifier claim.
            var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
            return new ClaimsPrincipal(identity);
        }

        private static List<Claim> ParseClaims(string jwt, out DateTimeOffset? expiresAt)
        {
            expiresAt = null;
            var claims = new List<Claim>();
            var parts = jwt.Split('.');
            if (parts.Length < 2) return claims;

            using var doc = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "exp" && prop.Value.TryGetInt64(out var exp))
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);

                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                        claims.Add(new Claim(prop.Name, item.ToString()));
                }
                else
                {
                    claims.Add(new Claim(prop.Name, prop.Value.ToString()));
                }
            }
            return claims;
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }
    }
}
