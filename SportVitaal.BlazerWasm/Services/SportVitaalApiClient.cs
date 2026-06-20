using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SportVitaal.BlazerWasm.Services
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

        // ---- Admin: locations ----

        public async Task<List<LocationDto>> GetLocationsAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<LocationDto>>("api/locations", JsonOptions, ct) ?? new();

        // ---- Admin: workouts ----

        public Task<bool> CreateWorkoutAsync(string name, int durationMinutes, string? description, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Post, "api/workouts", new { name, defaultDurationMinutes = durationMinutes, description }, ct);

        public Task<bool> UpdateWorkoutAsync(Guid id, string name, int durationMinutes, string? description, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Put, $"api/workouts/{id}", new { name, defaultDurationMinutes = durationMinutes, description }, ct);

        public Task<bool> DeleteWorkoutAsync(Guid id, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Delete, $"api/workouts/{id}", null, ct);

        // ---- Admin: instructors ----

        // Creating an instructor provisions a full login account with the Instructor role. The
        // server generates a password and e-mails it to the instructor, so only name and e-mail
        // are supplied here.
        public async Task<(bool ok, string? error)> CreateInstructorAsync(string name, string email, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/instructors")
            {
                Content = JsonContent.Create(new { name, email }, options: JsonOptions)
            };
            Authorize(request);
            var resp = await _http.SendAsync(request, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await resp.Content.ReadAsStringAsync(ct));
        }

        public Task<bool> UpdateInstructorAsync(Guid id, string name, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Put, $"api/instructors/{id}", new { name }, ct);

        public Task<bool> DeleteInstructorAsync(Guid id, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Delete, $"api/instructors/{id}", null, ct);

        public async Task<bool> UploadInstructorPhotoAsync(Guid id, Stream content, string fileName, string contentType, CancellationToken ct = default)
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(content);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "file", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/instructors/{id}/photo") { Content = form };
            Authorize(request);
            var resp = await _http.SendAsync(request, ct);
            return resp.IsSuccessStatusCode;
        }

        // ---- Admin: lessons ----

        // Lesson times are stored in UTC. The admin enters wall-clock time in their local zone,
        // so convert to UTC before sending (and back to local when displaying).
        public Task<bool> CreateLessonAsync(Guid workoutId, Guid locationId, DateTime startAt, int durationMinutes, Guid? instructorId, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Post, "api/lessons",
                new { workoutId, locationId, startAt = startAt.ToUniversalTime(), durationMinutes, instructorId }, ct);

        public Task<bool> CreateRecurringLessonAsync(Guid workoutId, Guid locationId, DateTime startAt, int durationMinutes,
            Guid? instructorId, RecurrenceFrequency frequency, int interval, DateTime? until, int? count, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Post, "api/lessons/recurring",
                new { workoutId, locationId, startAt = startAt.ToUniversalTime(), durationMinutes, instructorId, frequency = (int)frequency, interval, until = until?.ToUniversalTime(), count }, ct);

        public Task<bool> UpdateLessonAsync(Guid id, Guid locationId, DateTime startAt, int durationMinutes, Guid? instructorId, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Put, $"api/lessons/{id}",
                new { locationId, startAt = startAt.ToUniversalTime(), durationMinutes, instructorId }, ct);

        public Task<bool> DeleteLessonAsync(Guid id, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Delete, $"api/lessons/{id}", null, ct);

        // ---- Admin: occupancy + members ----

        public async Task<List<OccupancyDto>> GetOccupancyAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        {
            var url = "api/lessons/occupancy";
            var query = new List<string>();
            if (from.HasValue) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
            if (to.HasValue) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
            if (query.Count > 0) url += "?" + string.Join("&", query);
            return await GetAuthAsync<List<OccupancyDto>>(url, ct) ?? new();
        }

        public async Task<List<MemberDto>> GetMembersAsync(CancellationToken ct = default)
            => await GetAuthAsync<List<MemberDto>>("api/users/members", ct) ?? new();

        public Task<bool> DeleteMemberAsync(Guid id, CancellationToken ct = default)
            => SendAuthAsync(HttpMethod.Delete, $"api/users/members/{id}", null, ct);

        // ---- helpers ----

        private async Task<bool> SendAuthAsync(HttpMethod method, string url, object? body, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            Authorize(request);
            var resp = await _http.SendAsync(request, ct);
            return resp.IsSuccessStatusCode;
        }

        private async Task<T?> GetAuthAsync<T>(string url, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            Authorize(request);
            var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return default;
            return await resp.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
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

    public record OccupancyDto(
        Guid LessonId,
        Guid WorkoutId,
        DateTime StartAt,
        int DurationMinutes,
        Guid? LocationId,
        string? LocationName,
        int Capacity,
        int Reserved,
        Guid? InstructorId,
        bool IsPast);

    public record MemberDto(Guid Id, string Email, string? FullName, string? UserName, string Role, MembershipInfo? Membership);

    public record MembershipInfo(string Type, DateTime StartDate, DateTime? EndDate, bool IsActive);

    // Mirror of the WebApi RecurrenceFrequency (same order => same int values).
    public enum RecurrenceFrequency
    {
        Daily = 0,
        Weekly = 1,
        Monthly = 2
    }

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
