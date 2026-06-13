using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace SportVitaal.Web.Services
{
    /// <summary>
    /// Authentication state derived from the WebApi JWT. The token is persisted in
    /// ProtectedSessionStorage so a refresh/reconnect keeps the employee signed in,
    /// and mirrored into <see cref="TokenProvider"/> so API calls stay authenticated.
    /// </summary>
    public class JwtAuthStateProvider : AuthenticationStateProvider
    {
        private const string TokenKey = "sv_token";
        private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

        private readonly ProtectedSessionStorage _storage;
        private readonly TokenProvider _tokenProvider;
        private bool _restored;

        public JwtAuthStateProvider(ProtectedSessionStorage storage, TokenProvider tokenProvider)
        {
            _storage = storage;
            _tokenProvider = tokenProvider;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // Restore from session storage once (skipped during prerender, when JS interop is unavailable).
            if (!_restored && string.IsNullOrWhiteSpace(_tokenProvider.Token))
            {
                try
                {
                    var stored = await _storage.GetAsync<string>(TokenKey);
                    if (stored.Success && !string.IsNullOrWhiteSpace(stored.Value))
                        _tokenProvider.Token = stored.Value;
                    _restored = true;
                }
                catch
                {
                    // Prerendering: interop not yet available; re-evaluated after first interactive render.
                }
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
                await _storage.SetAsync(TokenKey, _tokenProvider.Token!);
            _restored = true;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public async Task LogoutAsync()
        {
            _tokenProvider.Token = null;
            await _storage.DeleteAsync(TokenKey);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
        }

        private static ClaimsPrincipal BuildPrincipal(string token)
        {
            var claims = ParseClaims(token, out var expiresAt);
            if (expiresAt is not null && expiresAt <= DateTimeOffset.UtcNow)
                return Anonymous;

            // Tokens from AuthController use the standard ClaimTypes URIs for name id and role.
            var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.NameIdentifier, ClaimTypes.Role);
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
