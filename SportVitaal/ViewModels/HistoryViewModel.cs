using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the "Historie" page: the member's overview of lessons they took part in over the past year.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private static readonly string[] Days = { "Zo", "Ma", "Di", "Wo", "Do", "Vr", "Za" };
    private static readonly string[] Months =
        { "jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec" };

    private readonly ApiClient _api;

    public HistoryViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private IReadOnlyList<HistoryRow> _lessons = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _loaded;

    public int Count => Lessons.Count;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var history = await _api.GetMyHistoryAsync();
            Lessons = history
                .Select(l => new HistoryRow(
                    l.Workout,
                    FormatWhen(l.StartAt),
                    l.Instructor ?? "nnb",
                    $"{l.DurationMinutes} min"))
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
        return $"{Days[(int)local.DayOfWeek]} {local.Day} {Months[local.Month - 1]} {local.Year} · {local:HH:mm}";
    }

    public record HistoryRow(string Workout, string When, string Instructor, string Duration);
}
