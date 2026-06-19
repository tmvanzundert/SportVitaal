using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the member dashboard. Logging out clears the session on the <see cref="ApiClient"/>,
/// whose Changed event makes the app shell swap back to the login screen.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    // Dutch weekday abbreviations, indexed by DayOfWeek (Sunday = 0).
    private static readonly string[] Days = { "Zo", "Ma", "Di", "Wo", "Do", "Vr", "Za" };
    private static readonly string[] Months =
        { "jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec" };

    private readonly ApiClient _api;

    public DashboardViewModel(ApiClient api) => _api = api;

    public string Name => _api.CurrentUser?.FullName is { Length: > 0 } n ? n
        : _api.CurrentUser?.Email ?? "Lid";

    public string Initials => string.Concat(
        Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(p => char.ToUpper(p[0])));

    /// <summary>True when the signed-in user is an instructor, who gets the extra attendance shortcut.</summary>
    public bool IsInstructor => _api.CurrentUser?.Role == "Instructor";

    /// <summary>Member's profile photo as a data URI (loaded on init), or null to fall back to initials.</summary>
    [ObservableProperty] private string? _photoDataUri;

    /// <summary>The current subscription shaped for display, or null when the member has none.</summary>
    public PlanInfo? Plan => BuildPlan(_api.CurrentUser?.Membership);

    /// <summary>
    /// In-app notice shown when a yearly subscription expires within 6 weeks, prompting renewal.
    /// Null when not applicable.
    /// </summary>
    public string? ExpiryNotice
    {
        get
        {
            var m = _api.CurrentUser?.Membership;
            if (m is null || m.Type is not ("TwiceWeeklyYearly" or "UnlimitedYearly") || !m.IsActive || m.EndDate is not { } end)
                return null;

            var days = (int)(end.Date - DateTime.UtcNow.Date).TotalDays;
            return days is >= 0 and <= 42
                ? $"Je jaarabonnement verloopt over {days} dagen. Koop alvast een nieuw abonnement."
                : null;
        }
    }

    [ObservableProperty] private IReadOnlyList<LessonRow> _lessons = [];
    [ObservableProperty] private bool _loaded;

    /// <summary>Unread in-app notification count, shown as a badge on the bell in the header.</summary>
    [ObservableProperty] private int _unreadNotifications;

    /// <summary>In-app notice when a spot has opened on a lesson the member is waitlisted for.</summary>
    [ObservableProperty] private string? _waitlistNotice;
    public Guid? WaitlistLessonId { get; private set; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        PhotoDataUri = await _api.GetProfilePhotoDataUriAsync();

        UnreadNotifications = await _api.GetUnreadNotificationCountAsync();

        try
        {
            var alerts = await _api.GetWaitlistAlertsAsync();
            if (alerts.Count > 0)
            {
                WaitlistLessonId = alerts[0].LessonId;
                WaitlistNotice = $"Er is een plek vrijgekomen voor {alerts[0].Workout}. Reserveer snel!";
            }
        }
        catch { /* non-fatal */ }

        try
        {
            var upcoming = await _api.GetMyUpcomingLessonsAsync(3);
            // The check-in window opens 30 min before a lesson starts; upcoming lessons are already
            // future-only, so a lesson within that window gets a direct "Inchecken" shortcut.
            var checkInFrom = DateTime.UtcNow.AddMinutes(30);
            Lessons = upcoming
                .Select(l => new LessonRow(l.LessonId, l.Workout, FormatWhen(l.StartAt), l.Instructor ?? "nnb",
                    $"{l.Reserved}/{l.Capacity}", l.StartAt <= checkInFrom))
                .ToList();
        }
        catch
        {
            Lessons = [];
        }
        finally
        {
            Loaded = true;
        }
    }

    private static string FormatWhen(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return $"{Days[(int)local.DayOfWeek]} {local:HH:mm}";
    }

    // Membership dates are stored as calendar dates, so they are formatted without timezone shifting.
    private static string FormatDate(DateTime d) => $"{d.Day} {Months[d.Month - 1]} {d.Year}";

    // Plan name, billing period and price live in the API's price table and are mirrored here for
    // display. Returns null when there is no real membership.
    private static PlanInfo? BuildPlan(ApiClient.MembershipDto? m)
    {
        if (m is null || m.Type is "None") return null;

        var (name, billing, price, unit) = m.Type switch
        {
            "TwiceWeeklyMonthly" => ("2x per week", "Maandelijks", 29, "/mnd"),
            "TwiceWeeklyYearly"  => ("2x per week", "Jaarlijks", 299, "/jr"),
            "UnlimitedMonthly"   => ("Onbeperkt", "Maandelijks", 55, "/mnd"),
            "UnlimitedYearly"    => ("Onbeperkt", "Jaarlijks", 549, "/jr"),
            _ => (m.Type, "", 0, "")
        };

        var priceLine = price > 0 ? $"{billing} · €{price}{unit}" : billing;
        var status = m.IsActive ? "ACTIEF" : "VERLOPEN";

        // Open-ended membership: nothing to count down.
        if (m.EndDate is not { } end)
            return new PlanInfo(name, priceLine, m.IsActive, status, false, "", "", 0, "Doorlopend abonnement");

        var endText = FormatDate(end);
        if (!m.IsActive)
            return new PlanInfo(name, priceLine, false, status, false, "", "", 0, $"Verlopen op {endText}");

        var daysLeft = Math.Max(0, (int)(end.Date - DateTime.UtcNow.Date).TotalDays);
        var total = (end - m.StartDate).TotalDays;
        var percentLeft = total > 0
            ? Math.Clamp((int)Math.Round((end - DateTime.UtcNow).TotalDays / total * 100), 0, 100)
            : 0;
        var nextLine = billing == "Maandelijks" ? $"Volgende betaling: {endText}" : $"Verloopt op: {endText}";

        return new PlanInfo(name, priceLine, true, status, true, "Verloopt over", $"{daysLeft} dagen", percentLeft, nextLine);
    }

    public record LessonRow(Guid LessonId, string Workout, string When, string Instructor, string Spots, bool CanCheckIn);

    public sealed record PlanInfo(string Name, string PriceLine, bool IsActive, string StatusBadge,
        bool ShowProgress, string ExpiryLabel, string ExpiryValue, int PercentLeft, string NextLine);
}
