using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Plugin.NFC;

namespace SportVitaal;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Plugin.NFC needs the current activity to register its foreground reader.
        CrossNFC.Init(this);
        base.OnCreate(savedInstanceState);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        // Tags scanned while the app is in the foreground arrive here; hand them to Plugin.NFC.
        CrossNFC.OnNewIntent(intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        CrossNFC.OnResume();
    }
}
