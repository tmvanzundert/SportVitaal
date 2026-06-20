using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the lesson detail page: full info, registered members (by username), seat (bike) selection
/// for seat-based lessons, and reserving / cancelling.
/// </summary>
public partial class LessonDetailViewModel : ObservableObject
{
    private static readonly string[] Palette = { "#f26522", "#1aa179", "#e0a400", "#8b5cf6", "#e2483d", "#3b82f6", "#ec4899" };

    private readonly ApiClient _api;
    private ApiClient.LessonDetail? _d;

    public LessonDetailViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private bool _messageIsError;
    [ObservableProperty] private int? _selectedSeat;
    [ObservableProperty] private bool _loaded;

    public string Workout => _d?.Workout ?? "Les";
    public string? Description => _d?.Description;
    public string Subtitle => $"{_d?.DurationMinutes ?? 0} min · {_d?.Instructor ?? "nnb"}";
    public string Time => _d?.StartAt.ToLocalTime().ToString("HH:mm") ?? "—";
    public string Duration => $"{_d?.DurationMinutes ?? 0} min";
    public string Plekken => _d is null ? "—" : $"{_d.Reserved}/{_d.Capacity}";

    public bool AllowsSeatSelection => _d?.AllowsSeatSelection ?? false;
    public bool ReservedByMe => _d?.ReservedByMe ?? false;
    public bool Full => _d is not null && _d.Reserved >= _d.Capacity;
    public bool TooFarAhead => _d is not null && _d.StartAt > DateTime.UtcNow.AddDays(7);
    public int ParticipantCount => _d?.Participants.Count ?? 0;

    public bool OnWaitlist => _d?.OnWaitlist ?? false;
    public int WaitlistCount => _d?.WaitlistCount ?? 0;
    /// <summary>A spot has opened on a lesson the member is waitlisted for (the "plek vrijgekomen" alert).</summary>
    public bool SpotFreed => OnWaitlist && !Full;

    public IReadOnlyList<Bike> Bikes => _d is { AllowsSeatSelection: true }
        ? Enumerable.Range(1, _d.Capacity)
            .Select(n => new Bike(n, _d.OccupiedSeats.Contains(n) && _d.MySeat != n, _d.MySeat == n, SelectedSeat == n))
            .ToList()
        : [];

    public IReadOnlyList<ParticipantRow> Participants => _d?.Participants
        .Select(p => new ParticipantRow(p.Name, Initials(p.Name), ColorFor(p.Name), p.IsMe))
        .ToList() ?? [];

    public bool CanReserve => _d is not null && !ReservedByMe && !Full && !TooFarAhead
        && (!AllowsSeatSelection || SelectedSeat is not null);

    public string ReserveLabel => TooFarAhead ? "Nog niet te reserveren"
        : Full ? "Les is vol"
        : AllowsSeatSelection && SelectedSeat is null ? "Selecteer eerst een fiets"
        : AllowsSeatSelection && SelectedSeat is { } s ? $"Reserveer · Fiets {s}"
        : "Reserveer plek";

    [RelayCommand]
    private async Task Load(Guid lessonId)
    {
        IsBusy = true;
        try { _d = await _api.GetLessonDetailAsync(lessonId); SelectedSeat = null; }
        catch { _d = null; }
        finally { Loaded = true; IsBusy = false; OnPropertyChanged(string.Empty); }
    }

    [RelayCommand]
    private void SelectSeat(int seat)
    {
        if (_d is null || (_d.OccupiedSeats.Contains(seat) && _d.MySeat != seat)) return; // bezet
        SelectedSeat = SelectedSeat == seat ? null : seat;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private Task Reserve()
        => RunAsync(_api.ReserveAsync(_d!.LessonId, AllowsSeatSelection ? SelectedSeat : null), "Je bent aangemeld voor de les!");

    [RelayCommand]
    private Task JoinWaitlist()
        => RunAsync(_api.ReserveAsync(_d!.LessonId, null), "Je staat nu op de wachtlijst.");

    [RelayCommand]
    private Task LeaveWaitlist()
        => RunAsync(_api.LeaveWaitlistAsync(_d!.LessonId), "Je bent van de wachtlijst gehaald.");

    [RelayCommand]
    private Task Cancel()
        => _d?.MyReservationId is { } rid
            ? RunAsync(_api.CancelReservationAsync(rid), "Je bent afgemeld.")
            : Task.CompletedTask;

    private async Task RunAsync(Task<string?> action, string successMessage)
    {
        if (_d is null || IsBusy) return;
        IsBusy = true;
        Message = null;
        try
        {
            var error = await action;
            if (error is null)
            {
                _d = await _api.GetLessonDetailAsync(_d.LessonId);
                SelectedSeat = null;
            }
            SetMessage(error ?? successMessage, error is not null);
        }
        catch
        {
            SetMessage("Er ging iets mis. Probeer het later opnieuw.", true);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(string.Empty);
        }
    }

    private void SetMessage(string message, bool isError)
    {
        MessageIsError = isError;
        Message = message;
    }

    private static string Initials(string name) => string.Concat(
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));

    private static string ColorFor(string name) => Palette[Math.Abs(name.Sum(c => c)) % Palette.Length];

    public record Bike(int Number, bool Occupied, bool Mine, bool Selected);

    public record ParticipantRow(string Name, string Initials, string Color, bool IsMe);
}
