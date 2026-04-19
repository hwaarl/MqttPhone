using Android.App;
using Android.Content.PM;
using Android.OS;
using static Java.Util.Jar.Attributes;

namespace MqttPhone
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        const int RequestReadPhone = 9001;

        protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // request phone number permissions at app start
            try
            {
                if (!Platforms.Android.PermissionsHelper.HasReadPhoneNumbersPermission(this))
                    Platforms.Android.PermissionsHelper.RequestReadPhoneNumbersPermission(this, RequestReadPhone);

                if (!Platforms.Android.PermissionsHelper.HasReadPhoneStatePermission(this))
                    Platforms.Android.PermissionsHelper.RequestReadPhoneStatePermission(this, RequestReadPhone);

                if (!Platforms.Android.PermissionsHelper.HasReadSmsPermission(this))
                    Platforms.Android.PermissionsHelper.RequestReadSmsPermission(this, RequestReadPhone);

                if (!Platforms.Android.PermissionsHelper.HasReceiveSmsPermission(this))
                    Platforms.Android.PermissionsHelper.RequestReceiveSmsPermission(this, RequestReadPhone);

            }
            catch { }
        }
    }
}
