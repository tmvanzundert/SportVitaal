using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SportVitaal.Services;

/// <summary>
/// Minimal client over the SportVitaal WebApi for the member portal. Holds the JWT and the
/// signed-in member in memory (registered as a singleton) and persists the token with
/// <see cref="SecureStorage"/> so the session survives an app restart. Raises <see cref="Changed"/>
/// whenever the sign-in state changes so the app shell can re-render.
/// </summary>
public class ApiClient
{
    private const string TokenKey = "sv_token";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Android emulators reach the host machine via 10.0.2.2; everything else uses localhost.
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5272"
            : "http://localhost:5272")
    };

    /// <summary>The signed-in member, populated on a successful login; null when logged out.</summary>
    public MeDto? CurrentUser { get; private set; }

    /// <summary>Base URL of the WebApi, used to resolve relative asset paths such as the profile photo.</summary>
    public string ApiBaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;

    public bool IsLoggedIn => CurrentUser is not null;

    /// <summary>Raised when the sign-in state changes, so the app shell can re-render.</summary>
    public event Action? Changed;

    /// <summary>Restores a persisted session on startup, if the saved token is still valid.</summary>
    public async Task InitializeAsync()
    {
        string? token = null;
        try { token = await SecureStorage.GetAsync(TokenKey); }
        catch { /* secure storage unavailable on this platform/config */ }

        if (!string.IsNullOrWhiteSpace(token))
            await ApplyTokenAsync(token, persist: false);
    }

    /// <summary>Logs in and loads the member profile. Returns null on success, or a Dutch error message.</summary>
    public async Task<string?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/login", new { email, password }, Json, ct);
        if (!resp.IsSuccessStatusCode) return "Onjuiste inloggegevens.";

        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>(Json, ct);
        if (string.IsNullOrWhiteSpace(body?.Token)) return "Geen token ontvangen.";

        if (!await ApplyTokenAsync(body.Token, persist: true, ct))
            return "Dit account heeft geen toegang tot het ledenportaal.";

        return null;
    }

    public void Logout()
    {
        CurrentUser = null;
        _http.DefaultRequestHeaders.Authorization = null;
        SecureStorage.Remove(TokenKey);
        Changed?.Invoke();
    }

    /// <summary>Authenticates the client with a token, loads the member, and optionally persists it.</summary>
    private async Task<bool> ApplyTokenAsync(string token, bool persist, CancellationToken ct = default)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try { CurrentUser = await _http.GetFromJsonAsync<MeDto>("api/users/me", Json, ct); }
        catch { CurrentUser = null; }

        // Members and instructors may use the app; anything else clears the (invalid) session.
        if (CurrentUser?.Role is not ("Member" or "Instructor"))
        {
            CurrentUser = null;
            _http.DefaultRequestHeaders.Authorization = null;
            SecureStorage.Remove(TokenKey);
            Changed?.Invoke();
            return false;
        }

        if (persist)
        {
            try { await SecureStorage.SetAsync(TokenKey, token); }
            catch { /* non-fatal: session just won't survive a restart */ }
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>The signed-in member's next <paramref name="count"/> upcoming reserved lessons.</summary>
    public async Task<IReadOnlyList<UpcomingLesson>> GetMyUpcomingLessonsAsync(int count, CancellationToken ct = default)
    {
        var lessons = await _http.GetFromJsonAsync<List<UpcomingLesson>>("api/reservations/mine", Json, ct) ?? [];
        return lessons
            .OrderBy(l => l.StartAt)
            .Take(count)
            .ToList();
    }

    /// <summary>All lessons between <paramref name="from"/> and <paramref name="to"/>, with names and occupancy.</summary>
    public async Task<IReadOnlyList<ScheduleLesson>> GetScheduleAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var fromQ = Uri.EscapeDataString(from.ToString("o"));
        var toQ = Uri.EscapeDataString(to.ToString("o"));
        var lessonsTask = _http.GetFromJsonAsync<List<ScheduleLessonDto>>($"api/lessons?from={fromQ}&to={toQ}", Json, ct);
        var workoutsTask = _http.GetFromJsonAsync<List<NamedDto>>("api/workouts", Json, ct);
        var instructorsTask = _http.GetFromJsonAsync<List<NamedDto>>("api/instructors", Json, ct);
        await Task.WhenAll(lessonsTask, workoutsTask, instructorsTask);

        var workouts = (await workoutsTask ?? []).ToDictionary(w => w.Id, w => w.Name);
        var instructors = (await instructorsTask ?? []).ToDictionary(i => i.Id, i => i.Name);
        var myId = CurrentUser?.Id;

        return (await lessonsTask ?? [])
            .OrderBy(l => l.StartAt)
            .Select(l => new ScheduleLesson(
                l.Id,
                workouts.GetValueOrDefault(l.WorkoutId, "Les"),
                l.StartAt,
                l.DurationMinutes,
                l.InstructorId is { } id ? instructors.GetValueOrDefault(id) : null,
                // Status is serialized numerically: 0 = Reserved, 1 = Cancelled, 2 = Attended.
                l.Reservations?.Count(r => r.Status != 1) ?? 0,
                l.Capacity,
                myId is { } me && (l.Reservations?.Any(r => r.MemberId == me && r.Status != 1) ?? false)))
            .ToList();
    }

    /// <summary>Detailed info for one lesson (participants, seats, the caller's reservation).</summary>
    public Task<LessonDetail?> GetLessonDetailAsync(Guid lessonId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<LessonDetail>($"api/reservations/lesson/{lessonId}", Json, ct);

    /// <summary>
    /// Reserves a spot, optionally on a specific seat (bike) given as a flat seat number (1..Capacity).
    /// Returns null on success or the server's message.
    /// </summary>
    public async Task<string?> ReserveAsync(Guid lessonId, int? seat = null, CancellationToken ct = default)
    {
        object body = seat is { } s ? new { lessonId, seat = s } : new { lessonId };
        var resp = await _http.PostAsJsonAsync("api/reservations/reserve", body, Json, ct);
        if (resp.IsSuccessStatusCode) return null;
        var msg = await resp.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(msg) ? "Reserveren mislukt." : msg;
    }

    /// <summary>Cancels (afmelden) the member's own reservation. Returns null on success or the server's message.</summary>
    public async Task<string?> CancelReservationAsync(Guid reservationId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/reservations/cancel/{reservationId}", null, ct);
        if (resp.IsSuccessStatusCode) return null;
        var msg = await resp.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(msg) ? "Afmelden mislukt." : msg;
    }

    /// <summary>Removes the member from a lesson's waiting list. Returns null on success.</summary>
    public async Task<string?> LeaveWaitlistAsync(Guid lessonId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/reservations/waitlist/leave/{lessonId}", null, ct);
        return resp.IsSuccessStatusCode ? null : "Kon niet van de wachtlijst worden verwijderd.";
    }

    /// <summary>The signed-in member's lesson history (lessons taken part in over the past year, most recent first).</summary>
    public async Task<IReadOnlyList<HistoryLesson>> GetMyHistoryAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<HistoryLesson>>("api/reservations/history", Json, ct) ?? [];

    /// <summary>The signed-in instructor's own scheduled lessons (next two weeks) for managing attendance.</summary>
    public async Task<IReadOnlyList<InstructorLesson>> GetInstructorLessonsAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<InstructorLesson>>("api/reservations/instructor/mine", Json, ct) ?? [];

    /// <summary>The attendance roster for one of the instructor's lessons: each registered member and whether they checked in.</summary>
    public async Task<InstructorAttendance?> GetInstructorAttendanceAsync(Guid lessonId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<InstructorAttendance>($"api/reservations/instructor/lesson/{lessonId}/attendance", Json, ct);

    /// <summary>Lessons the member is waitlisted for that now have a free spot (in-app alert).</summary>
    public async Task<IReadOnlyList<WaitlistAlert>> GetWaitlistAlertsAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<WaitlistAlert>>("api/reservations/waitlist/available", Json, ct) ?? [];

    // ---- In-app notification feed ----

    /// <summary>The member's notification feed (newest first) plus the unread count for the badge.</summary>
    public async Task<NotificationFeed> GetNotificationsAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<NotificationFeed>("api/notifications", Json, ct)
           ?? new NotificationFeed(0, []);

    /// <summary>Just the unread notification count, for polling the shell badge cheaply.</summary>
    public async Task<int> GetUnreadNotificationCountAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await _http.GetFromJsonAsync<UnreadCountDto>("api/notifications/unread-count", Json, ct);
            return r?.UnreadCount ?? 0;
        }
        catch { return 0; }
    }

    /// <summary>Marks a single notification as read. Returns true on success.</summary>
    public async Task<bool> MarkNotificationReadAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/notifications/{id}/read", null, ct);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>Marks all of the member's notifications as read.</summary>
    public async Task<bool> MarkAllNotificationsReadAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsync("api/notifications/read-all", null, ct);
        return resp.IsSuccessStatusCode;
    }

    // ---- Check-in (RFID / GPS attendance registration) ----

    /// <summary>
    /// The lesson the member can check in for right now: the soonest reserved lesson within the
    /// check-in window. Pass a <paramref name="lessonId"/> to target a specific reservation. Returns
    /// null when there is nothing to check in for.
    /// </summary>
    public async Task<CheckInLesson?> GetCheckInTargetAsync(Guid? lessonId = null, CancellationToken ct = default)
    {
        var url = lessonId is { } id ? $"api/reservations/checkin/current?lessonId={id}" : "api/reservations/checkin/current";
        var resp = await _http.GetAsync(url, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NoContent || !resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<CheckInLesson>(Json, ct);
    }

    /// <summary>Checks in via GPS; the server verifies the coordinates against the club location.</summary>
    public Task<string?> CheckInGpsAsync(Guid lessonId, double latitude, double longitude, CancellationToken ct = default)
        => PostCheckInAsync(new { lessonId, method = "gps", latitude, longitude }, ct);

    /// <summary>Checks in via RFID (simulated pass scan at the door).</summary>
    public Task<string?> CheckInRfidAsync(Guid lessonId, CancellationToken ct = default)
        => PostCheckInAsync(new { lessonId, method = "rfid" }, ct);

    private async Task<string?> PostCheckInAsync(object body, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync("api/reservations/checkin", body, Json, ct);
        if (resp.IsSuccessStatusCode) return null;
        var msg = await resp.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(msg) ? "Inchecken mislukt." : msg;
    }

    // ---- Subscription actions (each refreshes the cached member on success) ----

    /// <summary>Buys a new subscription of <paramref name="type"/>, optionally starting on a future date.</summary>
    public Task<string?> PurchaseAsync(MembershipType type, DateTime? startDate = null, CancellationToken ct = default)
        => PayAndRefreshAsync("api/memberships/purchase", new { type = (int)type, startDate }, ct);

    /// <summary>Renews (extends) the current subscription by one more period.</summary>
    public Task<string?> RenewAsync(CancellationToken ct = default)
        => PayAndRefreshAsync("api/memberships/renew", new { }, ct);

    /// <summary>Upgrades "2x per week" to "Onbeperkt" (monthly rate up; yearly pays the pro-rata difference).</summary>
    public Task<string?> UpgradeAsync(CancellationToken ct = default)
        => PayAndRefreshAsync("api/memberships/upgrade", new { }, ct);

    /// <summary>Cancels the current subscription (monthly only). Returns null on success or a Dutch error.</summary>
    public async Task<string?> CancelAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/memberships/cancel", new { }, Json, ct);
        if (!resp.IsSuccessStatusCode)
            return await resp.Content.ReadAsStringAsync(ct) is { Length: > 0 } msg ? msg : "Opzeggen mislukt.";
        await RefreshUserAsync(ct);
        return null;
    }

    /// <summary>Updates the member's display name and (member-visible) username. Returns null on success.</summary>
    public async Task<string?> UpdateProfileAsync(string? userName, string? fullName, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync("api/users/me", new { userName, fullName }, Json, ct);
        if (!resp.IsSuccessStatusCode) return "Opslaan mislukt.";
        await RefreshUserAsync(ct);
        return null;
    }

    /// <summary>Uploads a new profile photo (from gallery or camera). Returns null on success.</summary>
    public async Task<string?> UploadPhotoAsync(Stream content, string fileName, string? contentType, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        form.Add(fileContent, "file", fileName);

        var resp = await _http.PostAsync("api/users/me/photo", form, ct);
        if (!resp.IsSuccessStatusCode) return "Uploaden mislukt.";
        await RefreshUserAsync(ct);
        return null;
    }

    /// <summary>
    /// Fetches the profile photo as a base64 data URI. The hybrid WebView runs on an https origin,
    /// so an http image URL would be blocked as mixed content; loading the bytes over the native
    /// HttpClient and inlining them sidesteps that. Returns null when there is no photo.
    /// </summary>
    public async Task<string?> GetProfilePhotoDataUriAsync(CancellationToken ct = default)
    {
        var path = CurrentUser?.PhotoUrl;
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            using var resp = await _http.GetAsync(path, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch { return null; }
    }

    /// <summary>Re-fetches the member profile (e.g. after a subscription change) and notifies listeners.</summary>
    public async Task RefreshUserAsync(CancellationToken ct = default)
    {
        try { CurrentUser = await _http.GetFromJsonAsync<MeDto>("api/users/me", Json, ct); }
        catch { /* keep the existing snapshot on a transient failure */ }
        Changed?.Invoke();
    }

    // Creates a (simulated) payment intent, completes it via the webhook, then refreshes the member.
    private async Task<string?> PayAndRefreshAsync(string url, object body, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync(url, body, Json, ct);
        if (!resp.IsSuccessStatusCode)
            return await resp.Content.ReadAsStringAsync(ct) is { Length: > 0 } msg ? msg : "Betaling mislukt.";

        var intent = await resp.Content.ReadFromJsonAsync<IntentResponse>(Json, ct);
        var paymentId = DecodePaymentId(intent?.ClientSecret);
        if (paymentId is null) return "Geen geldige betaling ontvangen.";

        var webhook = await _http.PostAsJsonAsync("api/payments/webhook",
            new { @event = "payment_succeeded", paymentId }, Json, ct);
        if (!webhook.IsSuccessStatusCode) return "Betaling kon niet worden bevestigd.";

        await RefreshUserAsync(ct);
        return null;
    }

    // The simulated client secret is base64 of "{paymentId}:simulated_secret".
    private static string? DecodePaymentId(string? clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientSecret)) return null;
        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(clientSecret));
            var idx = decoded.IndexOf(':');
            return idx > 0 ? decoded[..idx] : null;
        }
        catch { return null; }
    }

    public record LoginResponse(string Token);

    private record IntentResponse(string ClientSecret, decimal Amount);

    public record MeDto(Guid Id, string Email, string? UserName, string? FullName, string? PhotoUrl, string Role,
        MembershipDto? Membership);

    public record MembershipDto(string Type, DateTime StartDate, DateTime? EndDate, bool IsActive);

    public record UpcomingLesson(Guid LessonId, string Workout, DateTime StartAt, string? Instructor, int Reserved, int Capacity);

    public record HistoryLesson(Guid LessonId, string Workout, DateTime StartAt, int DurationMinutes, string? Instructor);

    public record InstructorLesson(Guid LessonId, string Workout, DateTime StartAt, int DurationMinutes,
        string Location, int Reserved, int Attended, int Capacity);

    public record InstructorAttendance(Guid LessonId, string Workout, DateTime StartAt, int DurationMinutes,
        string Location, int Capacity, int Reserved, int Attended, List<AttendanceParticipant> Participants);

    public record AttendanceParticipant(string Name, int? Seat, bool CheckedIn);

    public record ScheduleLesson(Guid LessonId, string Workout, DateTime StartAt, int DurationMinutes,
        string? Instructor, int Reserved, int Capacity, bool ReservedByMe);

    public record LessonDetail(Guid LessonId, string Workout, string? Description, DateTime StartAt,
        int DurationMinutes, string? Instructor, int Capacity, int Reserved, bool AllowsSeatSelection,
        List<int> OccupiedSeats, bool ReservedByMe, int? MySeat, Guid? MyReservationId,
        bool OnWaitlist, int WaitlistCount, List<Participant> Participants);

    public record Participant(string Name, bool IsMe, int? Seat);

    public record WaitlistAlert(Guid LessonId, string Workout, DateTime StartAt);

    public record NotificationFeed(int UnreadCount, List<NotificationItem> Items);

    public record NotificationItem(Guid Id, string Type, string Title, string Body, DateTime CreatedAt, bool Read);

    private record UnreadCountDto(int UnreadCount);

    public record CheckInLesson(Guid LessonId, string Workout, DateTime StartAt, int DurationMinutes,
        string Location, bool AlreadyCheckedIn, bool CanCheckInNow);

    private record ScheduleLessonDto(Guid Id, Guid WorkoutId, DateTime StartAt, int DurationMinutes,
        Guid? InstructorId, int Capacity, List<ReservationDto>? Reservations);

    private record ReservationDto(Guid MemberId, int Status);

    private record NamedDto(Guid Id, string Name);
}

/// <summary>Mirror of SportVitaal.Domain.Enums.MembershipType (the app does not reference Domain).</summary>
public enum MembershipType
{
    None = 0,
    TwiceWeeklyMonthly = 1,
    TwiceWeeklyYearly = 2,
    UnlimitedMonthly = 3,
    UnlimitedYearly = 4
}
