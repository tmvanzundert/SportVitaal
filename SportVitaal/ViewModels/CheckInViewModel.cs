using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the "Inchecken" page: registering attendance for the member's current lesson via GPS
/// (a real device location read, verified server-side against the club location) or RFID
/// (a simulated pass scan at the door).
/// </summary>
public partial class CheckInViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private ApiClient.CheckInLesson? _lesson;

    public CheckInViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private bool _loaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _useGps = true;
    [ObservableProperty] private bool _checkedIn;
    [ObservableProperty] private string? _gpsStatus;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private bool _messageIsError;

    public bool HasLesson => _lesson is not null;

    public string Workout => _lesson?.Workout ?? "Geen les";
    public string Location => _lesson?.Location ?? "—";

    /// <summary>Time span of the lesson, e.g. "09:00 – 09:45".</summary>
    public string TimeRange => _lesson is { } l
        ? $"{l.StartAt:HH:mm} – {l.StartAt.AddMinutes(l.DurationMinutes):HH:mm}"
        : "—";

    [RelayCommand]
    private async Task Load(Guid? lessonId)
    {
        IsBusy = true;
        try
        {
            _lesson = await _api.GetCheckInTargetAsync(lessonId);
            CheckedIn = _lesson?.AlreadyCheckedIn ?? false;
        }
        catch
        {
            _lesson = null;
        }
        finally
        {
            Loaded = true;
            IsBusy = false;
            OnPropertyChanged(string.Empty);
        }
    }

    [RelayCommand]
    private void SelectGps()
    {
        UseGps = true;
        Message = null;
    }

    [RelayCommand]
    private void SelectRfid()
    {
        UseGps = false;
        Message = null;
    }

    /// <summary>Reads the device's GPS location and checks in; the server verifies the coordinates.</summary>
    [RelayCommand]
    private async Task CheckInGps()
    {
        if (_lesson is null || IsBusy) return;
        IsBusy = true;
        Message = null;
        GpsStatus = "Locatietoegang controleren…";
        try
        {
            var status = await MainThread.InvokeOnMainThreadAsync(
                () => Permissions.RequestAsync<Permissions.LocationWhenInUse>());
            if (status != PermissionStatus.Granted)
            {
                SetMessage("Geen toegang tot je locatie. Sta dit toe in de instellingen of gebruik RFID.", true);
                return;
            }

            GpsStatus = "GPS locatie bepalen…";
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15));
            var location = await MainThread.InvokeOnMainThreadAsync(
                () => Geolocation.Default.GetLocationAsync(request));
            if (location is null)
            {
                SetMessage("Kon je locatie niet bepalen. Probeer het opnieuw.", true);
                return;
            }

            GpsStatus = "Locatie verifiëren…";
            await SubmitAsync(_api.CheckInGpsAsync(_lesson.LessonId, location.Latitude, location.Longitude));
        }
        catch (FeatureNotSupportedException)
        {
            SetMessage("GPS is niet beschikbaar op dit apparaat. Gebruik RFID.", true);
        }
        catch (PermissionException)
        {
            SetMessage("Geen toegang tot je locatie. Sta dit toe in de instellingen of gebruik RFID.", true);
        }
        catch
        {
            SetMessage("Inchecken via GPS is mislukt. Probeer het opnieuw.", true);
        }
        finally
        {
            GpsStatus = null;
            IsBusy = false;
            OnPropertyChanged(string.Empty);
        }
    }

    /// <summary>Simulates an RFID pass scan at the door and checks in.</summary>
    [RelayCommand]
    private async Task SimulateRfid()
    {
        if (_lesson is null || IsBusy) return;
        IsBusy = true;
        Message = null;
        try
        {
            await SubmitAsync(_api.CheckInRfidAsync(_lesson.LessonId));
        }
        catch
        {
            SetMessage("Inchecken via RFID is mislukt. Probeer het opnieuw.", true);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(string.Empty);
        }
    }

    private async Task SubmitAsync(Task<string?> action)
    {
        var error = await action;
        if (error is null)
        {
            CheckedIn = true;
            SetMessage("Je bent ingecheckt voor de les!", false);
        }
        else
        {
            SetMessage(error, true);
        }
    }

    private void SetMessage(string message, bool isError)
    {
        MessageIsError = isError;
        Message = message;
    }
}
