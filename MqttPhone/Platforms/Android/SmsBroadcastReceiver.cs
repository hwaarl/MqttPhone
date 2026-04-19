using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Telephony;

namespace MqttPhone.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = true, Label = "SMS Receiver")]
    [IntentFilter(new[] { "android.provider.Telephony.SMS_RECEIVED" })]
    public class SmsBroadcastReceiver : BroadcastReceiver
    {
        // NOTE: This receiver only sees platform-delivered SMS (plaintext SMS).
        // It will NOT reliably receive RCS/chat messages (Google Messages "chat features")
        // or messages delivered over third-party internet messengers. For those
        // modern transports either the sender must fall back to plain SMS or a
        // different integration (Notification access, default SMS app, or server-side
        // push) is required. TOTP providers typically use plaintext SMS so this
        // receiver is sufficient for those OTP flows.
        public override void OnReceive(Context? context, Intent? intent)
        {
            try
            {
                if (intent?.Extras == null) return;
                var bundle = intent.Extras;
                var messages = Telephony.Sms.Intents.GetMessagesFromIntent(intent);
                if (messages == null || messages.Length == 0) return;

                var sb = new System.Text.StringBuilder();
                foreach (var sms in messages)
                {
                    if (sms == null) continue;
                    // fully qualify to avoid ambiguity
                    var smsBody = sms.MessageBody;
                    if (!string.IsNullOrEmpty(smsBody)) sb.Append(smsBody);
                }

                var body = sb.ToString();
                if (!string.IsNullOrEmpty(body))
                {
                    SmsStore.SetLatest(body);
                }
            }
            catch { }
        }
    }
}
