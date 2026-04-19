using System.Text.Json;
using Android.Content;

namespace MqttPhone.Platforms.Android
{
    public class TotpService
    {
        readonly Services.IPhoneNumberProvider? _phoneNumberProvider;

        public TotpService(Services.IPhoneNumberProvider? phoneNumberProvider = null)
        {
            _phoneNumberProvider = phoneNumberProvider;
        }
        private static void Log(string message) => File.AppendAllText(path: Path.Combine(FileSystem.AppDataDirectory, "mqtt.log"),
                                                                  $"{DateTime.Now:HH:mm:ss} {message}\n");

        public static async Task<string> HandleMessage(string msisdn, string topic, string messageJson)
        {
            Log("Handling obtainTOTP message");
            var parts = topic.Split('/');
            if (parts == null) return "Topic is null";
            if (!parts.Contains("mqttPhone", StringComparer.OrdinalIgnoreCase)) return "Invalid topic";
            if (string.IsNullOrEmpty(msisdn) || !parts.Contains(msisdn, StringComparer.OrdinalIgnoreCase))
            {
                // security requirement: only process messages that contain the MSISDN in the topic
                Log($"Given MSISDN {msisdn} doesnt match topic {topic}");
                return $"Given MSISDN {msisdn} doesnt match topic {topic}";
            }

            try
            {
                // Use the in-memory store filled by SmsBroadcastReceiver when messages arrive
                var latest = SmsStore.GetLatest();
                if (string.IsNullOrEmpty(latest)) return "No SMS received since App start";

                // Extract longest sequence of digits allowing spaces between them
                string resultDigits = await ExtractLongestDigitSequence(latest);

                // remove spaces
                var cleaned = resultDigits?.Replace(" ", string.Empty);

                // clear the store after reading to avoid reusing the same SMS
                //SmsStore.Clear();

                return cleaned;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string> ExtractLongestDigitSequence(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Replace non-digit and non-space with separator, so sequences with spaces remain
            var allowed = new char[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (char.IsDigit(c) || c == ' ') allowed[i] = c; else allowed[i] = '\n';
            }
            var s = new string(allowed);
            var parts = s.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            // choose longest in terms of digits count after removing spaces
            string best = string.Empty;
            foreach (var part in parts)
            {
                var digitsOnly = new string(part.Where(ch => char.IsDigit(ch) || ch == ' ').ToArray());
                var countDigits = digitsOnly.Count(c => char.IsDigit(c));
                var bestDigits = best.Count(c => char.IsDigit(c));
                if (countDigits > bestDigits) best = digitsOnly;
            }
            // allow spaces in between but they will be removed later
            return best;
        }
    }
}
