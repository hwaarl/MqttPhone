using Android.Content;
using Android.Telephony;
using System.Threading.Tasks;
using System;
using Android.App;

namespace MqttPhone.Platforms.Android
{
    // Android implementation - best-effort read of SIM1 (slot 0) number and format to E.164.
    // Note: MSISDN on SIM is often not present. This returns null if unavailable.
    public class PhoneNumberProvider : MqttPhone.Services.IPhoneNumberProvider
    {
        public Task<string?> GetSim1PhoneNumberAsync()
        {
            try
            {
                var ctx = global::Android.App.Application.Context;
                var tm = (TelephonyManager)ctx.GetSystemService(Context.TelephonyService);

                string? number = null;

                // Try SubscriptionManager first for SIM slot 0
                try
                {
                    // use SubscriptionManager API if available
                    var sm = SubscriptionManager.From(ctx);
                    if (sm != null)
                    {
                        var sub = sm.GetActiveSubscriptionInfoForSimSlotIndex(0);
                        if (sub != null)
                        {
                            number = sub.Number;
                        }
                    }
                }
                catch
                {
                    // ignore and fallback
                }

                if (string.IsNullOrEmpty(number))
                {
                    try
                    {
                        number = tm.Line1Number;
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(number))
                    return Task.FromResult<string?>(null);

                // sanitize and try to ensure E.164
                var sanitized = SanitizeNumber(number);
                if (IsE164(sanitized))
                    return Task.FromResult<string?>(sanitized);

                // attempt simple normalization: if missing + and country code, we cannot safely assume it
                // so return null to enforce security requirement
                return Task.FromResult<string?>(null);
            }
            catch
            {
                return Task.FromResult<string?>(null);
            }
        }

        static string SanitizeNumber(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new System.Text.StringBuilder();
            foreach (var c in input)
            {
                if (char.IsDigit(c) || c == '+') sb.Append(c);
            }
            return sb.ToString();
        }

        static bool IsE164(string number)
        {
            if (string.IsNullOrEmpty(number)) return false;
            // E.164 max 15 digits, must start with + and then digits
            if (!number.StartsWith("+")) return false;
            var digits = 0;
            for (int i = 1; i < number.Length; i++)
            {
                if (!char.IsDigit(number[i])) return false;
                digits++;
            }
            return digits > 0 && digits <= 15;
        }
    }
}
