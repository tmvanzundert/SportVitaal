using Microsoft.Extensions.Logging;
using SportVitaal.Shared.Services;
using SportVitaal.Services;
using SportVitaal.ViewModels;

namespace SportVitaal;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        // Add device-specific services used by the SportVitaal.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();

        // Member portal: WebApi client holding the JWT and the signed-in member.
        builder.Services.AddSingleton<ApiClient>();

        // View models (MVVM) for the portal screens.
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<SubscriptionViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ScheduleViewModel>();
        builder.Services.AddTransient<LessonDetailViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<CheckInViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}