using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SportVitaal.Web.Services
{
    /// <summary>
    /// Typed client over the SportVitaal WebApi. Anonymous reads use plain GETs;
    /// authenticated calls attach the JWT held in <see cref="TokenProvider"/>.
    /// </summary>
    public class SportVitaalApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly TokenProvider _tokenProvider;

        public SportVitaalApiClient(HttpClient http, TokenProvider tokenProvider)
        {
            _http = http;
            _tokenProvider = tokenProvider;
        }

        /// <summary>Base URL of the WebApi, used to resolve relative asset paths such as instructor photos.</summary>
        public string ApiBaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;

        // ---- Public reads ----

        public async Task<List<WorkoutDto>> GetWorkoutsAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<WorkoutDto>>("api/workouts", JsonOptions, ct) ?? new();

        public async Task<List<InstructorDto>> GetInstructorsAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<InstructorDto>>("api/instructors", JsonOptions, ct) ?? new();

        public async Task<List<LessonDto>> GetLessonsAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        {
            var url = "api/lessons";
            var query = new List<string>();
            if (from.HasValue) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
            if (to.HasValue) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
            if (query.Count > 0) url += "?" + string.Join("&", query);
            return await _http.GetFromJsonAsync<List<LessonDto>>(url, JsonOptions, ct) ?? new();
        }

        // ---- Signup flow ----

        public async Task<(bool ok, string? error)> RegisterAsync(string email, string password, CancellationToken ct = default)
        {
            var resp = await _http.PostAsJsonAsync("api/auth/register", new { email, password }, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await resp.Content.ReadAsStringAsync(ct));
        }

        public async Task<(bool ok, string? error)> LoginAsync(string email, string password, CancellationToken ct = default)
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login", new { email, password }, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode) return (false, "Onjuiste inloggegevens.");

            var body = await resp.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct);
            if (string.IsNullOrWhiteSpace(body?.Token)) return (false, "Geen token ontvangen.");

            _tokenProvider.Token = body!.Token;
            return (true, null);
        }

        public async Task<PurchaseResponse?> PurchaseAsync(MembershipType type, DateTime startDate, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/memberships/purchase")
            {
                Content = JsonContent.Create(new { type = (int)type, startDate }, options: JsonOptions)
            };
            Authorize(request);

            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PurchaseResponse>(JsonOptions, ct);
        }

        /// <summary>
        /// Completes the simulated payment by invoking the WebApi webhook, which activates
        /// the membership and triggers the confirmation email.
        /// </summary>
        public async Task<bool> CompleteSimulatedPaymentAsync(string clientSecret, CancellationToken ct = default)
        {
            var paymentId = DecodePaymentId(clientSecret);
            if (paymentId is null) return false;

            var payload = new { @event = "payment_succeeded", paymentId };
            var resp = await _http.PostAsJsonAsync("api/payments/webhook", payload, JsonOptions, ct);
            return resp.IsSuccessStatusCode;
        }

        private void Authorize(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(_tokenProvider.Token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenProvider.Token);
        }

        // The simulated client secret is base64 of "{paymentId}:simulated_secret".
        private static string? DecodePaymentId(string clientSecret)
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(clientSecret));
                var idx = decoded.IndexOf(':');
                return idx > 0 ? decoded[..idx] : null;
            }
            catch
            {
                return null;
            }
        }
    }

    // ---- DTOs (mirror the WebApi contracts; extra JSON fields are ignored) ----

    public record WorkoutDto(Guid Id, string Name, string? Description, int DefaultDurationMinutes);

    public record InstructorDto(Guid Id, string Name, string? PhotoUrl);

    public record LocationDto(Guid Id, string Name, int Capacity, bool AllowsSeatSelection);

    public record LessonDto(
        Guid Id,
        Guid WorkoutId,
        DateTime StartAt,
        int DurationMinutes,
        Guid? InstructorId,
        LocationDto? Location);

    public record LoginResponse(string Token);

    public record PurchaseResponse(string ClientSecret, decimal Amount, string Currency, DateTime StartDate);

    // Mirror of SportVitaal.Domain.Enums.MembershipType (Web does not reference Domain).
    public enum MembershipType
    {
        None = 0,
        TwiceWeeklyMonthly = 1,
        TwiceWeeklyYearly = 2,
        UnlimitedMonthly = 3,
        UnlimitedYearly = 4
    }
}
