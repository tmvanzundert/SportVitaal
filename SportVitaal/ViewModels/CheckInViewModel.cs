using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;
using SportVitaal.Shared.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the "Inchecken" page: registering attendance for the member's current lesson via GPS
/// (a real device location read, verified server-side against the club location) or RFID
/// (an NFC pass scan at the door, falling back to a simulated scan on devices without NFC).
/// </summary>
public partial class CheckInViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly IRfidReader _rfid;
    private ApiClient.CheckInLesson? _lesson;

    public CheckInViewModel(ApiClient api, IRfidReader rfid)
    {
        _api = api;
        _rfid = rfid;
    }

    [ObservableProperty] private bool _loaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _useGps;
    [ObservableProperty] private bool _checkedIn;
    [ObservableProperty] private string? _gpsStatus;
    [ObservableProperty] private string? _rfidStatus;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private bool _messageIsError;

    /// <summary>True when the device can read an NFC pass; otherwise the page offers a simulated scan.</summary>
    public bool RfidSupported => _rfid.IsSupported;

    public bool HasLesson => _lesson is not null;

    public string Workout => _lesson?.Workout ?? "Geen les";
    public string Location => _lesson?.Location ?? "—";

    /// <summary>Time span of the lesson, e.g. "09:00 – 09:45".</summary>
    public string TimeRange => _lesson is { } l
        ? $"{l.StartAt.ToLocalTime():HH:mm} – {l.StartAt.ToLocalTime().AddMinutes(l.DurationMinutes):HH:mm}"
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

    /// <summary>
    /// Reads the member's NFC pass and checks in with its UID. On devices without an NFC reader this
    /// falls back to a simulated scan (no tag UID), preserving the previous demo behaviour.
    /// </summary>
    [RelayCommand]
    private async Task ScanRfid()
    {
        if (_lesson is null || IsBusy) return;
        IsBusy = true;
        Message = null;
        try
        {
            string? tagUid = null;
            if (_rfid.IsSupported)
            {
                RfidStatus = "Houd je pas tegen de telefoon…";
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                tagUid = await _rfid.ReadTagAsync(cts.Token);
                if (tagUid is null)
                {
                    SetMessage("Geen pas gelezen. Houd je pas tegen de telefoon en probeer het opnieuw.", true);
                    return;
                }
            }

            await SubmitAsync(_api.CheckInRfidAsync(_lesson.LessonId, tagUid));
        }
        catch
        {
            SetMessage("Inchecken via RFID is mislukt. Probeer het opnieuw.", true);
        }
        finally
        {
            RfidStatus = null;
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
