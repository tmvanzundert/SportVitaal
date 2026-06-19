using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the "Aanmeldingen" page: the instructor's own lessons (yesterday through two weeks ahead),
/// each summarising how many registered members have checked in. Tapping a lesson opens its roster.
/// </summary>
public partial class InstructorLessonsViewModel : ObservableObject
{
    private static readonly string[] Days = { "Zo", "Ma", "Di", "Wo", "Do", "Vr", "Za" };
    private static readonly string[] Months =
        { "jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec" };

    private readonly ApiClient _api;

    public InstructorLessonsViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private IReadOnlyList<LessonRow> _lessons = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _loaded;

    public int Count => Lessons.Count;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var lessons = await _api.GetInstructorLessonsAsync();
            Lessons = lessons
                .OrderBy(l => l.StartAt)
                .Select(l => new LessonRow(
                    l.LessonId,
                    l.Workout,
                    FormatWhen(l.StartAt),
                    l.Location,
                    $"{l.Attended}/{l.Reserved}"))
                .ToList();
        }
        catch
        {
            Lessons = [];
        }
        finally
        {
            Loaded = true;
            IsBusy = false;
            OnPropertyChanged(nameof(Count));
        }
    }

    private static string FormatWhen(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return $"{Days[(int)local.DayOfWeek]} {local.Day} {Months[local.Month - 1]} · {local:HH:mm}";
    }

    public record LessonRow(Guid LessonId, string Workout, string When, string Location, string Attendance);
}
