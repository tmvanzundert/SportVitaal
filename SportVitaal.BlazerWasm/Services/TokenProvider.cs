namespace SportVitaal.BlazerWasm.Services
{
    /// <summary>
    /// Holds the current JWT in memory for the running WASM app (registered scoped — one
    /// instance per loaded app). Used by <see cref="SportVitaalApiClient"/> to authenticate
    /// calls to the WebApi; <see cref="JwtAuthStateProvider"/> persists it to sessionStorage.
    /// </summary>
    public class TokenProvider
    {
        public string? Token { get; set; }
    }
}
