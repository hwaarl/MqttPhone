using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using MQTTnet;
using static MqttPhone.Services.IAppSettingsService;
#if ANDROID
using MqttPhone.Platforms.Android;
#endif
namespace MqttPhone.Services;

public class MqttService
{
    readonly IMqttClient _client;
    MqttClientOptions _options;
    MqttConfig _config;
    readonly Services.IPhoneNumberProvider? _phoneNumberProvider;

    public MqttService(MqttConfig config,
                       Services.IPhoneNumberProvider? phoneNumberProvider = null)
    {
        _config = config;
        _phoneNumberProvider = phoneNumberProvider;

        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();

        // Handle incoming MQTT messages. The event contains the topic and payload, and we route messages to different handlers based on the topic suffix. For example, messages with topic ending in "/openPhoneNumber" will be routed to the PhoneDialerService to trigger a phone call, and messages with topic ending in "/obtainTOTP" will be routed to the TotpService to obtain a TOTP from the latest received SMS.
        _client.ApplicationMessageReceivedAsync += async e => await HandleMessage(e);

        // Reconnect if disconnected. We add a delay before reconnecting to avoid tight reconnect loops in case of persistent connection issues. We also ensure options are (re)built before reconnecting, in case the config was updated while disconnected.
        _client.DisconnectedAsync += async e =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            try
            {
                // ensure options are (re)built before reconnect attempt
                BuildOptionsIfNeeded();
                await _client.ConnectAsync(_options);
                // if topic templates are configured, subscribe using the SIM1 number
                if (_config.TopicTemplateList != null && _config.TopicTemplateList.Count > 0)
                {
                    foreach (var template in _config.TopicTemplateList)
                    {
                        try { await SubscribeConfiguredAsync(template); } catch { }
                    }
                }
            }
            catch { }
        };
    }

    void BuildOptions()
    {
        var builder = new MqttClientOptionsBuilder();

        if (string.IsNullOrWhiteSpace(_config?.Server))
            throw new InvalidOperationException("MQTT host not configured (MqttConfig.Server)");

        builder = builder.WithTcpServer(_config.Server, _config.Port)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(_config.Username))
            builder = builder.WithCredentials(_config.Username, _config.Password ?? string.Empty);

        if (_config.UseTls)
        {
            builder = builder.WithTlsOptions(new MqttClientTlsOptions
            {
                UseTls = true,
                AllowUntrustedCertificates = !_config.ValidateCertificate,
                IgnoreCertificateChainErrors = !_config.ValidateCertificate,
                IgnoreCertificateRevocationErrors = !_config.ValidateCertificate
            });
        }

        _options = builder.Build();
    }

    void BuildOptionsIfNeeded()
    {
        // build if options are null or server changed
        if (_options == null || string.IsNullOrWhiteSpace(_options.ChannelOptions?.ToString()))
        {
            BuildOptions();
            return;
        }

        // simple guard: if current options host doesn't match config, rebuild
        try
        {
            // MQTTnet stores TCP server info in ChannelOptions; best effort check
            var channel = _options.ChannelOptions?.ToString();
            if (channel == null || !_config.Server?.Contains(channel) == true)
            {
                BuildOptions();
            }
        }
        catch
        {
            BuildOptions();
        }
    }

    public void ReloadOptions()
    {
        BuildOptions();
    }

    public string Connect()
    {
        string connectionString = "(no MQTT connection configured)";

        try
        {
            if (!_client.IsConnected)
            {
                // ensure options are built with current config
                BuildOptions();
                if (_options == null || _options.ChannelOptions == null) return "(MQTT connection not configured or invalid)";

                _client.ConnectAsync(_options).Wait();
                connectionString = $"mqtt{(_config.UseTls ? "s" : string.Empty)}://{_config.Server}:{_config.Port}";
            }
        }
        catch (Exception ex)
        {
            connectionString = $"Connection mqtt://{_config.Server}:{_config.Port} failed:\n{ex.ToString()}";
        }
        return connectionString;
    }

    public async Task DisconnectAsync()
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync();
    }

    // Subscribe using a provided topic template. The template must contain a single '{0}' placeholder
    // which will be replaced by the SIM1 phone number (without the leading '+').
    public async Task<string> SubscribeConfiguredAsync(string template)
    {
        if (!_client.IsConnected) return "(not connected)";
        if (_config == null) return "(no config)";
        if (string.IsNullOrEmpty(template)) return "(no topic template configured)";
        if (_phoneNumberProvider == null) return "(missing permission to obtain MSISDN)";

        // get SIM1 phone number (slot index 0)
        var phone = await _phoneNumberProvider.GetSim1PhoneNumberAsync();
        if (string.IsNullOrEmpty(phone))
        {
            // do not subscribe if number is not available or not valid - security requirement
            return "(SIM returns no MSISDN)";
        }

        var topic = string.Format(template, phone.Trim('+'));
        var filter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
        try {
            // use a cancellation token to avoid hanging indefinitely if subscribe fails
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Attempt to subscribe to the topic. If the subscription fails (e.g. due to network issues or broker errors), we avoid crashing the app by cancelling the subscribe attempt after a timeout.
            await _client.SubscribeAsync(filter, cts.Token);
        }
        catch (Exception ex)
        {
            return $"Subscribe to topic '{topic}' failed: {ex.Message}";
        }
        return topic;
    }

    // Publish a message to the given topic if connected.
    public async Task PublishAsync(string topic, string payload)
    {
        if (string.IsNullOrEmpty(topic)) return;
        try
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload ?? string.Empty)
                .Build();

            if (_client.IsConnected)
            {
                await _client.PublishAsync(msg);
            }
        }
        catch
        {
            // ignore publish errors for now
        }
    }

    // expose the internal callback for consumers
    public Func<MqttApplicationMessageReceivedEventArgs, Task> OnMessageReceived => HandleMessage;

    public void UpdateConfig(MqttConfig config)
    {
        _config = config;
        BuildOptions();
    }

    private static void Log(string message) => File.AppendAllText(path: Path.Combine(FileSystem.AppDataDirectory, "mqtt.log"),
                                                                  $"{DateTime.Now:HH:mm:ss} {message}\n");

    private async Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage?.Topic ?? string.Empty;
            var payload = e.ApplicationMessage?.Payload == null
                ? string.Empty
                : Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            string? msisdn = null;
            if (_phoneNumberProvider != null)
                msisdn = await _phoneNumberProvider.GetSim1PhoneNumberAsync();
            msisdn = msisdn?.Trim('+');  //MQTT Topics cannot contain pluses, the MSISDN always does

            Log($"Received: {topic}, payload {(string.IsNullOrEmpty(payload) ? "(empty)" : payload)}, MSISDN: {msisdn}");

            if (String.IsNullOrEmpty(msisdn)) return;

            // route by topic suffix
            if (topic.EndsWith("/openPhoneNumber", StringComparison.OrdinalIgnoreCase))
                PhoneDialerService.HandleMessage(msisdn, topic, payload);

#if ANDROID
            if (topic.EndsWith("/obtainTOTP", StringComparison.OrdinalIgnoreCase))
            {
                // Obtain TOTP from the latest received SMS
                string? totp = await TotpService.HandleMessage(msisdn, topic, payload);

                if (String.IsNullOrEmpty(totp) && Clipboard.Default.HasText) {
                    totp = await Clipboard.Default.GetTextAsync();
                    if (!String.IsNullOrEmpty(totp))
                        totp = await TotpService.ExtractLongestDigitSequence(totp);
                }
                if (String.IsNullOrEmpty(totp))
                    Log("No SMS received since App start, or SMS has no TOTP. Also clipboard contains no TOTP");

                // Publish the TOTP back to a return topic
                var returnTopic = $"/mqttPhone/{msisdn.Trim('+')}/returnTOTP";
                Log($"Sending: {returnTopic}, payload '{(string.IsNullOrEmpty(totp) ? "(empty)" : totp)}'");
                await PublishAsync(returnTopic, totp);
            }
#endif
        }
        catch
        {
            // ignore
        }
        await Task.CompletedTask;
    }

}
