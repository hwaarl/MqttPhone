using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using MqttPhone.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace MqttPhone.Platforms.Android
{

    [Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
    public class MqttForegroundService : Service
    {
        public const string ActionConnect = "MQTT_CONNECT";
        public const string ActionUpdateConfig = "MQTT_UPDATE_CONFIG";
        public const string ExtraConfig = "config";

        private string NOTIFICATION_CHANNEL_ID = "1000";
        private int NOTIFICATION_ID = 1;
        private string NOTIFICATION_CHANNEL_NAME = "notification";

        private MqttConfig _config;                                                           // Gets passed down with the intent extras when config is updated from MainPage; we can also update it on connect to ensure we have the latest config when connecting, in case service was restarted by the system
        private MqttService _mqttService;                                                     // we want to keep the same instance of MqttService across config updates, so we create it on demand when starting the service and reuse it; it will be null on first start, and we can also check for null before creating a new instance on subsequent starts (e.g. after config update)
        private readonly PhoneNumberProvider _phoneNumberProvider = new PhoneNumberProvider();
        private readonly TotpService _totpService = new TotpService();

        private static string _logfilePath = Path.Combine(FileSystem.AppDataDirectory, "mqtt.log");

        private void startForegroundService()
        {
            var notifcationManager = GetSystemService(Context.NotificationService) as NotificationManager;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                createNotificationChannel(notifcationManager);
            }

            var notification = new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID);
            notification.SetAutoCancel(false);
            notification.SetOngoing(true);
            notification.SetSmallIcon(Resource.Mipmap.appicon);
            notification.SetContentTitle("ForegroundService");
            notification.SetContentText("Foreground Service is running");
            StartForeground(NOTIFICATION_ID,
                            notification.Build());
        }

        private void createNotificationChannel(NotificationManager notificationMnaManager)
        {
            NotificationChannel channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID,
                                                                  NOTIFICATION_CHANNEL_NAME,
                                                                  NotificationImportance.Low);
            notificationMnaManager.CreateNotificationChannel(channel);
        }

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        private static void Log(string message) => File.AppendAllText(
            path: _logfilePath,
            $"{DateTime.Now:HH:mm:ss} {message}\n");


        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            startForegroundService();

            var json = intent?.GetStringExtra(ExtraConfig);
            _config = new MqttConfig(json);

            if (_mqttService == null)
                _mqttService = new MqttService(_config, _phoneNumberProvider);


            if (intent != null && intent.Action.Equals(ActionUpdateConfig))
                _mqttService.UpdateConfig(_config);

            if (intent != null && intent.Action.Equals(ActionConnect))
            {
                try
                {

                    Log("Connecting...");
                    string connection = _mqttService.ConnectAsync();
                    Log($"Connected to {connection}, now subscribing...");

                    // Subscribe for each configured topic template
                    if (_config.TopicTemplateList != null && _config.TopicTemplateList.Count > 0)
                    {
                        foreach (var template in _config.TopicTemplateList)
                        {
                            try
                            { 
                                var topic = _mqttService.SubscribeConfiguredAsync(template).Result;
                                Log($"Subscribed to: {topic}");
                            }
                            catch (Exception ex)
                            {
                                Log($"Subscribe failed for template '{template}': {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Log("No topic templates configured to subscribe to.");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Connect failed: {ex.Message}");
                }
            }

            return StartCommandResult.NotSticky;
        }
    }
}
