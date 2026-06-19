using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the "Mijn Profiel" page: editing the display name and member-visible username, and
/// uploading a profile photo from the gallery or camera.
/// </summary>
public partial class ProfileViewModel : ObservableObject
{
    private static readonly string[] FullMonths =
    {
        "januari", "februari", "maart", "april", "mei", "juni",
        "juli", "augustus", "september", "oktober", "november", "december"
    };

    private readonly ApiClient _api;

    public ProfileViewModel(ApiClient api)
    {
        _api = api;
        _fullName = _api.CurrentUser?.FullName ?? string.Empty;
        _userName = _api.CurrentUser?.UserName ?? string.Empty;
    }

    [ObservableProperty] private string _fullName;
    [ObservableProperty] private string _userName;
    [ObservableProperty] private string? _photoDataUri;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private bool _messageIsError;

    public string Email => _api.CurrentUser?.Email ?? string.Empty;

    public string Initials => string.Concat(
        (FullName is { Length: > 0 } ? FullName : Email)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(p => char.ToUpper(p[0])));

    private SportVitaal.Services.ApiClient.MembershipDto? M => _api.CurrentUser?.Membership;
    public string LidSindsLower => M?.StartDate is { } d ? $"{FullMonths[d.Month - 1]} {d.Year}" : "—";

    /// <summary>Loads the existing profile photo (as a data URI) when the page opens.</summary>
    [RelayCommand]
    private async Task Load() => PhotoDataUri = await _api.GetProfilePhotoDataUriAsync();

    [RelayCommand]
    private Task Save() => RunAsync(_api.UpdateProfileAsync(UserName, FullName), "Profiel opgeslagen.");

    [RelayCommand]
    private Task PickFromGallery()
        => UploadAsync(() => MainThread.InvokeOnMainThreadAsync(async () =>
            (await MediaPicker.Default.PickPhotosAsync()).FirstOrDefault()), "Kon de foto niet openen.");

    [RelayCommand]
    private async Task CaptureFromCamera()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            SetMessage("Camera is niet beschikbaar op dit apparaat.", true);
            return;
        }

        var status = await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<Permissions.Camera>());
        if (status != PermissionStatus.Granted)
        {
            SetMessage("Geen toegang tot de camera.", true);
            return;
        }

        await UploadAsync(() => MainThread.InvokeOnMainThreadAsync(() => MediaPicker.Default.CapturePhotoAsync()),
            "Kon de camera niet openen.");
    }

    [RelayCommand]
    private void Logout() => _api.Logout();

    private async Task UploadAsync(Func<Task<FileResult?>> pick, string openError)
    {
        if (IsBusy) return;

        FileResult? file;
        try { file = await pick(); }
        catch { SetMessage(openError, true); return; }
        if (file is null) return; // user cancelled

        IsBusy = true;
        Message = null;
        try
        {
            await using var stream = await file.OpenReadAsync();
            var error = await _api.UploadPhotoAsync(stream, file.FileName, file.ContentType);
            if (error is null) PhotoDataUri = await _api.GetProfilePhotoDataUriAsync();
            SetMessage(error ?? "Profielfoto bijgewerkt.", error is not null);
        }
        catch
        {
            SetMessage("Uploaden mislukt.", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunAsync(Task<string?> action, string successMessage)
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = null;
        try
        {
            var error = await action;
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
}
