using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the "Lesrooster" page: weeks of lessons (navigable) with a day selector and occupancy.
/// Reserving happens on the lesson detail page, not here.
/// </summary>
public partial class ScheduleViewModel : ObservableObject
{
    private static readonly string[] DayAbbr = { "ZO", "MA", "DI", "WO", "DO", "VR", "ZA" };
    private static readonly string[] Palette = { "#f26522", "#e0a400", "#1aa179", "#3b82f6", "#8b5cf6", "#ec4899" };

    private readonly ApiClient _api;
    private IReadOnlyList<ApiClient.ScheduleLesson> _all = [];

    public ScheduleViewModel(ApiClient api)
    {
        _api = api;
        WeekStart = StartOfWeek(DateTime.Today);
        _selected = DateTime.Today.Date;
    }

    public DateTime WeekStart { get; private set; }

    [ObservableProperty] private DateTime _selected;
    [ObservableProperty] private bool _isBusy;

    public int WeekNumber => ISOWeek.GetWeekOfYear(WeekStart.AddDays(3));
    public int Year => WeekStart.AddDays(3).Year;

    public IReadOnlyList<DayChip> Days => Enumerable.Range(0, 7)
        .Select(i => WeekStart.AddDays(i))
        .Select(d => new DayChip(DayAbbr[(int)d.DayOfWeek], d.Day, d, d.Date == Selected.Date))
        .ToList();

    public IReadOnlyList<LessonRow> Lessons => _all
        .Where(l => l.StartAt.ToLocalTime().Date == Selected.Date)
        .Select(ToRow)
        .ToList();

    public bool Loaded { get; private set; }

    [RelayCommand]
    private Task Load() => LoadWeekAsync();

    [RelayCommand]
    private Task PreviousWeek() => ShiftWeekAsync(-7);

    [RelayCommand]
    private Task NextWeek() => ShiftWeekAsync(7);

    [RelayCommand]
    private void SelectDay(DateTime date)
    {
        Selected = date.Date;
        OnPropertyChanged(string.Empty);
    }

    private Task ShiftWeekAsync(int days)
    {
        var offset = (Selected.Date - WeekStart).Days; // keep the same weekday selected
        WeekStart = WeekStart.AddDays(days);
        Selected = WeekStart.AddDays(Math.Clamp(offset, 0, 6));
        return LoadWeekAsync();
    }

    private async Task LoadWeekAsync()
    {
        IsBusy = true;
        try
        {
            // Pad the range a day on each side: StartAt is stored in UTC while the week
            // bounds are local, so a lesson near midnight could otherwise fall just outside
            // the window. The per-day filter (ToLocalTime) still shows only the right days.
            _all = await _api.GetScheduleAsync(WeekStart.AddDays(-1), WeekStart.AddDays(8));
        }
        catch
        {
            _all = [];
        }
        finally
        {
            Loaded = true;
            IsBusy = false;
            OnPropertyChanged(string.Empty);
        }
    }

    private LessonRow ToRow(ApiClient.ScheduleLesson l)
    {
        var percent = l.Capacity > 0 ? Math.Clamp(l.Reserved * 100 / l.Capacity, 0, 100) : 0;
        var full = l.Reserved >= l.Capacity;
        return new LessonRow(
            l.LessonId,
            l.Workout,
            $"{l.StartAt.ToLocalTime():HH:mm} · {l.DurationMinutes} min",
            l.Instructor ?? "nnb",
            $"{l.Reserved}/{l.Capacity}",
            percent,
            AccentFor(l.Workout),
            full,
            l.ReservedByMe);
    }

    private static string AccentFor(string workout) => Palette[workout.Sum(c => c) % Palette.Length];

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    public record DayChip(string Abbr, int Day, DateTime Date, bool IsSelected);

    public record LessonRow(Guid LessonId, string Workout, string Time, string Instructor, string Count,
        int Percent, string Accent, bool Full, bool ReservedByMe);
}
