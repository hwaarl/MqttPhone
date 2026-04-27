using System.Configuration;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using MQTTnet;

internal class Program
{
    // P/Invoke declarations for clipboard operations (used for copying TOTP to clipboard)
    // Windows only, for .NET Core/5+ where System.Windows.Forms.Clipboard is not available anymore
    [DllImport("user32.dll")]
    internal static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    internal static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    internal static extern bool SetClipboardData(uint uFormat, IntPtr data);

    [STAThread]
    private static void Main(string[] args)
    {
        #region Input parameters and validation
        bool inputValid = (args.Length > 1);

        string? mobileNumber = (inputValid) ? args[0] : null;
        if (inputValid && (String.IsNullOrEmpty(mobileNumber) || !Regex.IsMatch(mobileNumber, @"^\d+$")))
        {
            Console.WriteLine($"=== ERROR\nInvalid mobile number {mobileNumber}.");
            if (mobileNumber != null && mobileNumber.StartsWith('+'))
                Console.WriteLine("Tip: Please provide the mobile number without the leading '+' for the country code, e.g. 41791234567 for +41791234567.");
            Console.WriteLine();
            inputValid = false;
        }

        string? command = (inputValid) ? args[1].ToLower() : null;
        if (inputValid && !(command == "dial" || command == "totp" || command == "info"))
        {
            Console.WriteLine($"=== ERROR\nInvalid command {command}.\n");
            inputValid = false;
        }

        string targetNumber = string.Empty;
        if (inputValid && (command == "dial"))
        {
            if (args.Length < 3)
            {
                Console.WriteLine($"=== ERROR\nMissing target number for dial command.\n");
                inputValid = false;
            }
            else
            {
                targetNumber = args[2];
                if (String.IsNullOrEmpty(targetNumber) || targetNumber.Length < 10)
                {
                    Console.WriteLine($"=== ERROR\nInvalid target number {targetNumber}.\n");
                    inputValid = false;
                }
            }
        }

        if (!inputValid)
        {
            string exeName = System.AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine($"=== USAGE");
            Console.WriteLine($"{exeName} <MobileNumber> [dial <TargetNumber> | totp]");
            Console.WriteLine($"<MobileNumber> : The mobile number of the phone to interact with, including the country code but without the leading '+'.");
            Console.WriteLine($"<TargetNumber> : Whichever phone number your phone can handle, i.e. including areacode. Minimum length 10.\n");
            Console.WriteLine($"=== EXAMPLES");
            Console.WriteLine($"C:\\> {exeName} 41791234567 dial 0441234567\nC:\\> {exeName} 41791234567 dial \"+41 44 123 45 67\"\n---> sends message to the broker, requesting the phone with +41791234567 as main number to open the standard Phone app and pre-populate the phonenumber with 0441234567\n\n");
            Console.WriteLine($"C:\\> {exeName} 41791234567 totp             \n---> sends message to the broker, requesting the phone with +41791234567 to obtain the OTP from the latest SMS, or from the clipboard if none present.\nThis app awaits the response as MQTT message with the TOTP as message body and puts it into the clipboard");
            return;
        }
        #endregion

        #region Configuration
        // ----------------------------------------- Configuration -----------------------------------------
        string? mqttBrokerServer = ConfigurationManager.AppSettings["Server"];
        if (mqttBrokerServer == null)
        {
            Console.WriteLine($"MQTT broker Server not found in configuration. Please check config: {Path.GetFileName(System.Configuration.ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath)}");
            return;
        }
        string? mqttBrokerPort = ConfigurationManager.AppSettings["Port"];
        if (mqttBrokerPort == null)
        {
            Console.WriteLine("MQTT broker Port not found in configuration. Please check config.");
            return;
        }
        string mqttBrokerUsername = ConfigurationManager.AppSettings["Username"] ?? string.Empty;
        string mqttBrokerPassword = ConfigurationManager.AppSettings["Password"] ?? string.Empty;

        bool useTls = (ConfigurationManager.AppSettings["UseTls"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false);
        string mqttProtocol = useTls ? "wss" : "ws";
        bool validateTlsCertificates = (ConfigurationManager.AppSettings["ValidateTlsCertificates"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false);

        string mqttBrokerUri = $"{mqttProtocol}://{mqttBrokerServer}:{mqttBrokerPort}/mqtt";
        #endregion


        #region Build connection options
        // ----------------------------------------- Build connection options -----------------------------------------
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithWebSocketServer(o => o.WithUri($"{mqttBrokerUri}/mqtt"))
            .WithClientId("MyClient_" + Guid.NewGuid());

        if (!String.IsNullOrEmpty(mqttBrokerUsername))
            optionsBuilder.WithCredentials(mqttBrokerUsername, mqttBrokerPassword);

        if (useTls)
            optionsBuilder.WithTlsOptions(o => o.WithTargetHost(mqttBrokerServer)); // Modern TLS config

        var options = optionsBuilder.Build();
        #endregion

        if (command == "info")
        {
            Console.WriteLine($"=== Environment Information");
            Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
            Console.WriteLine($".NET Version: {RuntimeInformation.FrameworkDescription}");
            Console.WriteLine();
            Console.WriteLine($"=== Configuration Information");
            Console.WriteLine($"MQTT Broker Server: {ConfigurationManager.AppSettings["Server"]}");
            Console.WriteLine($"MQTT Broker Port: {ConfigurationManager.AppSettings["Port"]}");
            Console.WriteLine($"MQTT Broker Username: {(String.IsNullOrEmpty(ConfigurationManager.AppSettings["Username"]) ? "(none)" : ConfigurationManager.AppSettings["Username"])}");
            Console.WriteLine($"MQTT Broker Use TLS: {ConfigurationManager.AppSettings["UseTls"]}");
            Console.WriteLine($"MQTT Broker Validate TLS Certificates: {ConfigurationManager.AppSettings["ValidateTlsCertificates"]}");
            Console.WriteLine();
            return;
        }


        #region Connect to MQTT broker
        // ----------------------------------------- Connect to MQTT broker -----------------------------------------
        MqttClientFactory factory = new MqttClientFactory();
        var mqttClient = factory.CreateMqttClient();
        try
        {
            MqttClientConnectResult res = mqttClient.ConnectAsync(options).GetAwaiter().GetResult();
            Console.WriteLine($"Connected to {mqttBrokerUri} successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to {mqttBrokerUri}: {ex.Message}");
            return;
        }
        #endregion

        #region Publish command and await response if needed
        // ----------------------------------------- Process logic -----------------------------------------
        if (command == "dial")
        {
            string? topic = ConfigurationManager.AppSettings["Topic_OpenPhoneNumber"];
            if (topic == null)
            {
                Console.WriteLine("=== ERROR\nTopic 'Topic_OpenPhoneNumber' for dial command not found in configuration. Please check config.");
                return;
            }
            topic = String.Format(topic, mobileNumber);
            string payload = $"{{\"phoneNumber\":\"{targetNumber}\"}}";
            Console.WriteLine($"Publishing dial command for {mobileNumber} to topic {topic} with target number {targetNumber}...");
            MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(false)
                .Build();
            mqttClient.PublishAsync(message).GetAwaiter().GetResult();
            Console.WriteLine($"Published OK.");
        }
        else if (command == "totp")
        {
            // Prepare the request message
            string? topic = ConfigurationManager.AppSettings["Topic_ObtainTOTP"];
            if (topic == null)
            {
                Console.WriteLine("=== ERROR\nTopic 'Topic_ObtainTOTP' for TOTP command not found in configuration. Please check config.");
                return;
            }
            topic = String.Format(topic, mobileNumber);
            MqttApplicationMessage message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithRetainFlag(false)
                .Build();

            // Subscribe to the response topic
            string? topicResponse = ConfigurationManager.AppSettings["Topic_ReturnTOTP"];
            if (topicResponse == null)
            {
                Console.WriteLine("=== ERROR\nTopic 'Topic_ReturnTOTP' for TOTP response not found in configuration. Please check config.");
                return;
            }
            topicResponse = String.Format(topicResponse, mobileNumber);
            mqttClient.SubscribeAsync(topicResponse).GetAwaiter().GetResult();
            Console.WriteLine($"Subscribed to topic {topicResponse} for TOTP response.");

            // Publish the request
            mqttClient.PublishAsync(message).GetAwaiter().GetResult();
            Console.WriteLine($"Published TOTP request for {mobileNumber} to topic {topic}.");

            // Await the response
            var tcs = new TaskCompletionSource<string>();
            mqttClient.ApplicationMessageReceivedAsync += e =>
             {
                 if (e.ApplicationMessage.Topic == topicResponse)
                 {
                     string totp = EncodingExtensions.GetString(Encoding.UTF8, e.ApplicationMessage.Payload);
                     tcs.SetResult(totp);
                 }
                 return Task.CompletedTask;
             };
            var receivedTotp = tcs.Task.GetAwaiter().GetResult();

            Console.WriteLine($"Received TOTP: {receivedTotp}");
            //System.Windows.Forms.Clipboard.SetText(receivedTotp); // Doesnt exist on .NET Core?
            //TextCopy.ClipboardService.SetText( receivedTotp );    // Doesnt work in .NET 10 anymore, probably due to security hardenings in Windows regarding clipboard access. Using P/Invoke as workaround

            OpenClipboard(IntPtr.Zero);
            var ptr = Marshal.StringToHGlobalUni(receivedTotp);
            SetClipboardData(13, ptr);                              // Windows clipboard format for Unicode text is CF_UNICODETEXT = 13
            CloseClipboard();
            Marshal.FreeHGlobal(ptr);

            Console.WriteLine("TOTP has been copied to clipboard.");
        }
        #endregion


        // ----------------------------------------- Disconnect from MQTT Broker -----------------------------------------
        try
        {
            mqttClient.DisconnectAsync().GetAwaiter().GetResult();
            Console.WriteLine($"Disconnected from {mqttBrokerUri} successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to disconnect from {mqttBrokerUri}: {ex.Message}");
        }
    }
}