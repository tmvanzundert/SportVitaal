using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the attendance roster for one of the instructor's lessons: every registered member with whether
/// they have checked in at the door (RFID/GPS) yet, so the instructor can see who is and is not present.
/// </summary>
public partial class InstructorAttendanceViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private ApiClient.InstructorAttendance? _data;

    public InstructorAttendanceViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private IReadOnlyList<ParticipantRow> _participants = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _loaded;

    public bool HasLesson => _data is not null;

    public string Workout => _data?.Workout ?? "Les";
    public string Location => _data?.Location ?? "—";
    public int Reserved => _data?.Reserved ?? 0;
    public int Attended => _data?.Attended ?? 0;

    /// <summary>Time span of the lesson, e.g. "09:00 – 09:45".</summary>
    public string TimeRange => _data is { } d
        ? $"{d.StartAt.ToLocalTime():HH:mm} – {d.StartAt.ToLocalTime().AddMinutes(d.DurationMinutes):HH:mm}"
        : "—";

    [RelayCommand]
    private async Task LoadAsync(Guid lessonId)
    {
        IsBusy = true;
        try
        {
            _data = await _api.GetInstructorAttendanceAsync(lessonId);
            Participants = _data?.Participants
                .Select(p => new ParticipantRow(p.Name, Initials(p.Name), ColorFor(p.Name), p.CheckedIn))
                .ToList() ?? [];
        }
        catch
        {
            // Keep whatever is already on screen: this method is also called by the page's poll loop,
            // and a transient network blip should not blank out an already-loaded roster.
            if (_data is null) Participants = [];
        }
        finally
        {
            Loaded = true;
            IsBusy = false;
            OnPropertyChanged(string.Empty);
        }
    }

    private static readonly string[] Palette =
        { "#f26522", "#1aa179", "#e0a400", "#8b5cf6", "#e2483d", "#3b82f6", "#ec4899" };

    private static string Initials(string name) => string.Concat(
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));

    private static string ColorFor(string name) => Palette[Math.Abs(name.Sum(c => c)) % Palette.Length];

    public record ParticipantRow(string Name, string Initials, string Color, bool CheckedIn);
}
