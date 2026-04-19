using Android;
using Android.App;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace MqttPhone.Platforms.Android;

public static class PermissionsHelper
{
    public const string CallPhone = Manifest.Permission.CallPhone;
    public const string ReadPhoneNumbers = Manifest.Permission.ReadPhoneNumbers;
    public const string ReadPhoneState = Manifest.Permission.ReadPhoneState;
    public const string ReadSms = Manifest.Permission.ReadSms;
    public const string ReceiveSms = Manifest.Permission.ReceiveSms;

    public static bool HasCallPhonePermission(Activity activity)
    {
        return ContextCompat.CheckSelfPermission(activity, CallPhone) == Permission.Granted;
    }

    public static void RequestCallPhonePermission(Activity activity, int requestCode)
    {
        ActivityCompat.RequestPermissions(activity, new[] { CallPhone }, requestCode);
    }

    public static bool HasReadPhoneNumbersPermission(Activity activity)
    {
        return ContextCompat.CheckSelfPermission(activity, ReadPhoneNumbers) == Permission.Granted;
    }

    public static void RequestReadPhoneNumbersPermission(Activity activity, int requestCode)
    {
        ActivityCompat.RequestPermissions(activity, new[] { ReadPhoneNumbers }, requestCode);
    }

    public static bool HasReadPhoneStatePermission(Activity activity)
    {
        return ContextCompat.CheckSelfPermission(activity, ReadPhoneState) == Permission.Granted;
    }

    public static void RequestReadPhoneStatePermission(Activity activity, int requestCode)
    {
        ActivityCompat.RequestPermissions(activity, new[] { ReadPhoneState }, requestCode);
    }

    public static bool HasReadSmsPermission(Activity activity)
    {
        return ContextCompat.CheckSelfPermission(activity, ReadSms) == Permission.Granted;
    }

    public static void RequestReadSmsPermission(Activity activity, int requestCode)
    {
        ActivityCompat.RequestPermissions(activity, new[] { ReadSms }, requestCode);
    }

    public static bool HasReceiveSmsPermission(Activity activity)
    {
        return ContextCompat.CheckSelfPermission(activity, ReceiveSms) == Permission.Granted;
    }

    public static void RequestReceiveSmsPermission(Activity activity, int requestCode)
    {
        ActivityCompat.RequestPermissions(activity, new[] { ReceiveSms }, requestCode);
    }
}
