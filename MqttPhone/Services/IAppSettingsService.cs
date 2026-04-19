using System;
using System.Collections.Generic;
using System.Text;

namespace MqttPhone.Services
{
    public class MqttConfig
    {
        public string? Server { get; set; }
        public int Port { get; set; } = 1883;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool UseTls { get; set; }
        public bool ValidateCertificate { get; set; }
        public List<string>? TopicTemplateList { get; set; }

        public MqttConfig() { }

        // Deserialize from JSON string manually, with some error handling for missing or invalid properties
        public MqttConfig(string json) {

            Object? obj = System.Text.Json.JsonSerializer.Deserialize<Object>(json);

            if (obj == null ) return;
            if (obj is not System.Text.Json.JsonElement jsonElement) return;

            // Unwrap the parameters from the "Mqtt" property if it exists, otherwise use the root. This is where it might've gone wrong before if the JSON structure was not as expected.
            jsonElement.TryGetProperty("Mqtt", out var je);
            if (je.ValueKind == System.Text.Json.JsonValueKind.Object)
                jsonElement = je;

            string? server = jsonElement.GetProperty("Server").GetString();
            int port = jsonElement.GetProperty("Port").GetInt32();

            string? username = jsonElement.GetProperty("Username").GetString();
            string? password = jsonElement.GetProperty("Password").GetString();

            bool useTls = jsonElement.GetProperty("UseTls").GetBoolean();
            bool validateCertificate = jsonElement.GetProperty("ValidateCertificate").GetBoolean();

            string? topicTemplateListJson = jsonElement.GetProperty("TopicTemplateList").GetRawText();
            List<string>? topicTemplateList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(topicTemplateListJson);

            Server = server;
            Port = port;
            Username = username;
            Password = password;
            UseTls = useTls;
            ValidateCertificate = validateCertificate;
            TopicTemplateList = topicTemplateList;
        }

        public MqttConfig(string? server, int port, string? username, string? password, bool useTls, bool validateCertificate, List<string>? topicTemplateList)
        {
            Server = server;
            Port = port;
            Username = username;
            Password = password;
            UseTls = useTls;
            ValidateCertificate = validateCertificate;
            TopicTemplateList = topicTemplateList;
        }

        public MqttConfig(IAppSettingsService appSettingsService)
        {
            var config = appSettingsService.GetMqttConfig();  // Note that this loops back onto the constructor that uses the JSON string
            Server = config.Server;
            Port = config.Port;
            Username = config.Username;
            Password = config.Password;
            UseTls = config.UseTls;
            ValidateCertificate = config.ValidateCertificate;
            TopicTemplateList = config.TopicTemplateList;
        }

    }

    public interface IAppSettingsService
    {
        public MqttConfig GetMqttConfig();
        public void ReloadMqttConfig();
        public void SaveMqttConfig(MqttConfig config);
    }
}
