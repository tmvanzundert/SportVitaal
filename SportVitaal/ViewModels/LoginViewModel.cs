using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Drives the login screen. On a successful login the <see cref="ApiClient"/> raises its
/// Changed event and the app shell swaps to the dashboard, so this view model does not navigate.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    // The public marketing site (Android emulators reach the host via 10.0.2.2).
    private static readonly string WebsiteUrl = DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:5022"
        : "http://localhost:5022";

    private readonly ApiClient _api;

    public LoginViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _showPassword;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    [RelayCommand]
    private void TogglePassword() => ShowPassword = !ShowPassword;

    [RelayCommand]
    private async Task LoginAsync()
    {
        Error = null;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Vul je e-mailadres en wachtwoord in.";
            return;
        }

        IsBusy = true;
        try
        {
            Error = await _api.LoginAsync(Email, Password);
        }
        catch
        {
            Error = "Er ging iets mis bij het inloggen. Probeer het later opnieuw.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task OpenWebsiteAsync() => Launcher.OpenAsync(WebsiteUrl);
}
