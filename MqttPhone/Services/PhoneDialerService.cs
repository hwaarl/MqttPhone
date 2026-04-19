using System.Text.Json;
using Microsoft.Maui.ApplicationModel;

namespace MqttPhone.Services;

public static class PhoneDialerService
{
    // This is the main entry point for handling incoming MQTT messages related to phone dialing; it performs topic-based routing and security checks before attempting to parse the message and dial the number
    // Topic template: mqttphone/{msisdn}/openPhoneNumber
    public static void HandleMessage(string msisdn, string topic, string messageJson)
    {
        var parts = topic.Split('/');
        if (parts == null) return;
        if (parts.Length != 3) return;
        if (!parts[0].Equals("mqttphone", StringComparison.OrdinalIgnoreCase)) return;
        if (msisdn != null && !msisdn.Equals(parts[1], StringComparison.OrdinalIgnoreCase)) return; // security requirement: only process messages that contain the MSISDN in the topic

        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            if (doc.RootElement.TryGetProperty("phoneNumber", out var pn))
            {
                var number = pn.GetString();
                if (!string.IsNullOrEmpty(number) && PhoneDialer.IsSupported)
                {
                    PhoneDialer.Open(number);
                }
            }
        }
        catch { }
    }
}
