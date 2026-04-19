using GoogleGson;
using MqttPhone.Services;
using Application = Android.App.Application;

namespace MqttPhone.Platforms.Android
{
    public class AppSettingsService : IAppSettingsService
    {
        // Define a path where you want to store the log. This ends up being $"/Android/data/{AppInfo.Current.PackageName}/files"

        // -- This ended up in /storage/emulated/0/Android/data/com.waarle.mqttphone/files/appsettings.json
        // private string appSettingsFilePath = System.IO.Path.Combine(Application.Context.GetExternalFilesDir(null)?.AbsolutePath ?? "", "appsettings.json");

        // -- This ended up in /data/user/0/com.waarle.mqttphone/files/appsettings.json - note that this path must correspond with the MainPage.xaml.cs path where the appsettings.json is written back from the UI
        private string appSettingsFilePath = Path.Combine(FileSystem.AppDataDirectory, "appsettings.json");

        // -- This is the path I wanted it to be so I can access it via File Explorer in Windows, but it doesnt exist at runtime. The appSettingsFilePath above is the one that actually works for reading and writing the config file.
        // private string appSettingsFilePath = $"/Android/data/{AppInfo.Current.PackageName}/files/appsettings.json";

        MqttConfig IAppSettingsService.GetMqttConfig()
        {
            string json = "";

            // While debugging I noticed the appSettingsFilePath might not exist but the Exists returns true anyways, so I added an extra check to see if the file can be read and has content
            if (File.Exists(appSettingsFilePath))
                json = File.ReadAllText(appSettingsFilePath);

            if (!File.Exists(appSettingsFilePath) || String.IsNullOrEmpty(json))
            {
                // If the file doesn't exist in the app data directory, fall back to the default appsettings.json in the app package
                using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").Result;
                json = new StreamReader(stream).ReadToEnd();

                // Copy the default appsettings.json to the app data directory for future use
                using (var destStream = File.Create(appSettingsFilePath))
                {
                    stream.CopyTo(destStream);
                    destStream.Flush();
                }
            }

            MqttConfig mqttConfig = new MqttConfig(json);
            return mqttConfig;

            // return System.Text.Json.JsonSerializer.Deserialize<MqttConfig>(json) ?? new MqttConfig(); -- The Deserializer copes with the structure of the json, so I wrote a custom constructor in MqttConfig that can handle the json structure of the default appsettings.json file, which has a "MqttConfig" property that contains the actual config values

        }

        public void ReloadMqttConfig()
        {
            throw new NotImplementedException();
        }

        void IAppSettingsService.SaveMqttConfig(MqttConfig config)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(appSettingsFilePath, json);
        }
    }
}
