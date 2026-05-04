using System.Text.Json;
using Microsoft.Maui.ApplicationModel;

namespace MqttPhone.Services;

public static class PhoneDialerService
{

    private static void Log(string message) => File.AppendAllText(path: Path.Combine(FileSystem.AppDataDirectory, "mqtt.log"),
                                                              $"{DateTime.Now:HH:mm:ss} {message}\n");

    // This is the main entry point for handling incoming MQTT messages related to phone dialing; it performs topic-based routing and security checks before attempting to parse the message and dial the number
    // Topic template: mqttphone/{msisdn}/openPhoneNumber
    public static void HandleMessage(string msisdn, string topic, string messageJson)
    {
        var parts = topic.Split('/');
        if (parts == null) return;
        if (!parts.Contains("mqttPhone", StringComparer.OrdinalIgnoreCase)) return;
        if (string.IsNullOrEmpty(msisdn) || !parts.Contains(msisdn, StringComparer.OrdinalIgnoreCase)) 
        {
            Log($"Given MSISDN {msisdn} doesnt match topic {topic}");
            return; // security requirement: only process messages that contain the MSISDN in the topic
        }

        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            if (doc.RootElement.TryGetProperty("phoneNumber", out var pn))
            {
                var number = pn.GetString();
                if (!string.IsNullOrEmpty(number) && PhoneDialer.IsSupported)
                {
                    PhoneDialer.Open(number);
                    Log("OK");
                }
            }
        }
        catch (Exception e) {
            Log($"NOK: {e.Message}");
        }
    }
}
