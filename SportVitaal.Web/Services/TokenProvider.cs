namespace SportVitaal.Web.Services
{
    /// <summary>
    /// Holds the current JWT for the active Blazor Server circuit (registered scoped).
    /// Used by <see cref="SportVitaalApiClient"/> to authenticate calls to the WebApi.
    /// </summary>
    public class TokenProvider
    {
        public string? Token { get; set; }
    }
}
