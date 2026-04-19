using MqttPhone.Services;
using System.Text.Json;



#if ANDROID
using Android.Content;
using Android.App;
using MqttPhone.Platforms.Android;
#endif

namespace MqttPhone
{
    public partial class MainPage : ContentPage
    {
        MqttConfig _config;

        // Logging. Because the MQTT service runs in a foreground service on Android, we can't directly log to the UI from that service. Instead, we write logs to a file and tail that file in the MainPage to display logs in real time.
        private string _logPath;
        private long _lastPosition = 0;
        private CancellationTokenSource _cts;

        public MainPage(MqttConfig config)
        {
            InitializeComponent();
            _config = config;

            LoadConfigToUi();

            _logPath = Path.Combine(FileSystem.AppDataDirectory, "mqtt.log");

            _cts = new CancellationTokenSource();
            StartTailing(_cts.Token);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _cts.Cancel();

#if ANDROID
            var context = Android.App.Application.Context;
            var intent = new Intent(context, typeof(MqttForegroundService));
            intent.SetAction(MqttForegroundService.ActionConnect);
            Android.App.Application.Context.StartForegroundService(intent);
            context.StopService(intent);
#endif
        }

        private async void StartTailing(CancellationToken token)
        {
            File.WriteAllText(_logPath, $"{DateTime.Now:HH:mm:ss} Started {_logPath}\n"); // Clear log on each start

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(_logPath))
                    {
                        using var stream = new FileStream(
                            _logPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite);

                        if (stream.Length > _lastPosition)
                        {
                            stream.Seek(_lastPosition, SeekOrigin.Begin);

                            using var reader = new StreamReader(stream);
                            var newText = await reader.ReadToEndAsync();

                            _lastPosition = stream.Position;

                            if (!string.IsNullOrWhiteSpace(newText))
                                Log(newText);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(ex.Message);
                }

                await Task.Delay(500); // adjust (200–1000ms)
            }
        }

        private void LoadConfigToUi()
        {
            ServerEntry.Text = _config.Server;
            PortEntry.Text = _config.Port.ToString();
            UsernameEntry.Text = _config.Username;
            PasswordEntry.Text = _config.Password;
            TlsCheck.IsChecked = _config.UseTls;
            CertValidateCheck.IsChecked = _config.ValidateCertificate;
        }

        async void OnSaveClicked(object? sender, EventArgs e)
        {
            // update config object
            _config.Server = ServerEntry.Text;
            if (int.TryParse(PortEntry.Text, out var p)) _config.Port = p;
            _config.Username = UsernameEntry.Text;
            _config.Password = PasswordEntry.Text;
            _config.UseTls = TlsCheck.IsChecked;
            _config.ValidateCertificate = CertValidateCheck.IsChecked;

            // save back to Resources/Raw/appsettings.json
            try
            {
                var path = Path.Combine(FileSystem.Current.AppDataDirectory, "appsettings.json");
                var doc = new
                {
                    Mqtt = new
                    {
                        Server = _config.Server,
                        Port = _config.Port,
                        Username = _config.Username,
                        Password = _config.Password,
                        UseTls = _config.UseTls,
                        ValidateCertificate = _config.ValidateCertificate,
                        TopicTemplateList = _config.TopicTemplateList
                    }
                };
                var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                Log($"Saved settings to {path}");
                // reload mqtt client options from updated config
#if ANDROID
                var intent = new Intent(Platform.CurrentActivity, typeof(MqttForegroundService));
                intent.SetAction(MqttForegroundService.ActionUpdateConfig);
                intent.PutExtra(MqttForegroundService.ExtraConfig, json);

                Platform.CurrentActivity.StartForegroundService(intent);
#endif
            }
            catch (Exception ex)
            {
                Log($"Failed to save settings: {ex.Message}");
            }
        }

        async void OnConnectClicked(object? sender, EventArgs e)
        {
            var json = JsonSerializer.Serialize(_config);

#if ANDROID
            // Create intent to start the foreground service with the MQTT config as an extra
            Android.Content.Intent intent = new Android.Content.Intent(Android.App.Application.Context, typeof(MqttForegroundService));
            intent.SetAction(MqttForegroundService.ActionConnect);
            intent.PutExtra(MqttForegroundService.ExtraConfig, json);

            // Send intent to start the service
            Android.App.Application.Context.StartForegroundService(intent);
            Task.Delay(500).Wait(); // wait a bit for the service to start and log

            // After connecting, we can also send a subscribe action to subscribe to topics based on the new config
            intent.SetAction(MqttForegroundService.ActionSubscribe);
            Android.App.Application.Context.StartForegroundService(intent);

#endif
        }

        void Log(string text)
        {
            MainThread.BeginInvokeOnMainThread(() => {
                LogEditor.Text = DateTime.Now.ToString("HH:mm:ss") + " - " + text + "\n" + LogEditor.Text;
            });
        }

    }
}
