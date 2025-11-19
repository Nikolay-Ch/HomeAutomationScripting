using Microsoft.ClearScript;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Packets;
using System.Security.Authentication;
using System.Text;

namespace HomeAutomationScriptingService.ScriptingObjects.MqttClient
{
    public interface IScriptingMqttClient
    {
        void Publish(string topic, string payload);
        void Subscribe(string topic, Action<string, string> subscriber);
        void Unsubscribe(string topic, Action<string, string> subscriber);
    }
    public class ScriptingMqttClient :
        ScriptingConfigurableObject<MqttClientConfiguration>, IScriptingMqttClient
    {
        protected ScriptingServiceMetrics Metrics { get; }
        protected CancellationToken CancellationToken { get; }
        protected IMqttClient MqttClient { get; }
        protected Dictionary<string, List<Action<string, string>>> Subscribers { get; } = [];

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        protected void AutoReconnect(MqttClientOptions options)
        {
            _ = Task.Run(async () =>
            {
                int reconnectAttempts = 0;

                while (!CancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!MqttClient.IsConnected)
                        {
                            // try to connect
                            await MqttClient.ConnectAsync(options, CancellationToken);
                            reconnectAttempts = 0; // reset counter on success

                            if (Logger.IsEnabled(LogLevel.Information))
                                Logger.LogInformation("{ClassName} - {MethodName} - Successfully (re)connected",
                                    nameof(ScriptingMqttClient), nameof(AutoReconnect));
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        reconnectAttempts++;

                        if (Logger.IsEnabled(LogLevel.Error))
                            Logger.LogError(ex, "{ClassName} - {MethodName} - Reconnection attempt {Attempt} failed",
                                nameof(ScriptingMqttClient), nameof(AutoReconnect), reconnectAttempts);
                    }

                    // sleep for a while
                    await Task.Delay(5000, CancellationToken);
                }
            }, CancellationToken);
        }

        public override void InitScriptEngine(IScriptEngine scriptEngine)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace("{ClassName} - {MethodName} - {scriptEngine}",
                    nameof(ScriptingMqttClient), nameof(InitScriptEngine), scriptEngine);

            scriptEngine.AddHostObject("MQTT", this);
            scriptEngine.AddHostType(typeof(Console));
            scriptEngine.AddHostType("SubscriberAction", typeof(Action<string, string>));
        }

        public ScriptingMqttClient(
            ILogger<ScriptingMqttClient> logger,
            IOptions<MqttClientConfiguration> componentConfiguration,
            ScriptingServiceMetrics metrics,
            CancellationToken cancellationToken)
            : base(logger, componentConfiguration)
        {
            if (Logger.IsEnabled(LogLevel.Information))
                Logger.LogInformation("Creating MqttClient at: {time}. Uri:{uri}", DateTimeOffset.Now, ComponentConfiguration.MqttUri);

            Metrics = metrics;
            CancellationToken = cancellationToken;

            var tlsOptions = new MqttClientTlsOptions()
            {
                SslProtocol = ComponentConfiguration.MqttSecure ? SslProtocols.Tls12 : SslProtocols.None,
                IgnoreCertificateChainErrors = true,
                IgnoreCertificateRevocationErrors = true
            };

            var options = new MqttClientOptionsBuilder()
                .WithClientId(ComponentConfiguration.ClientId.Replace("-", "").Replace(" ", ""))
                .WithCredentials(ComponentConfiguration.MqttUser, ComponentConfiguration.MqttUserPassword)
                .WithTcpServer(ComponentConfiguration.MqttUri, ComponentConfiguration.MqttPort)
                .WithCleanSession()
                .WithTlsOptions(tlsOptions)
                .Build();

            MqttClient = new MqttClientFactory()
                .CreateMqttClient();

            AutoReconnect(options);

            // wait for connection
            while (!MqttClient.IsConnected)
            {
                Logger.LogTrace("MqttClient not connected... Go to sleep for a second...");
                Task.Delay(1000, CancellationToken);
            }

            MqttClient.ApplicationMessageReceivedAsync += MessageReceive;

            if (Logger.IsEnabled(LogLevel.Information))
                Logger.LogInformation("Creating MqttClient done at: {time}", DateTimeOffset.Now);
        }

        // receive messaged from all subscribed topics
        private async Task MessageReceive(MqttApplicationMessageReceivedEventArgs e)
        {
            Logger.MessageReceive(e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
            Metrics.MessagesReceived.Add(1);

            foreach (var subscriber in Subscribers[e.ApplicationMessage.Topic])
            {
                try
                {
                    subscriber(e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
                }
                catch { }
            }

            await Task.Delay(0);
        }

        public void Subscribe(string topic, Action<string, string> subscriber)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace("{ClassName} - {MethodName} - {topic}, {subscriber}",
                    nameof(ScriptingMqttClient), nameof(Subscribe), topic, subscriber);

            if (!Subscribers.ContainsKey(topic))
                Subscribers.Add(topic, []);

            Subscribers[topic].Add(subscriber);

            var mqttFactory = new MqttClientFactory();
            var topicFilter = mqttFactory.CreateTopicFilterBuilder().WithTopic(topic);//.WithAtLeastOnceQoS();

            var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(topicFilter).Build();

            MqttClient.SubscribeAsync(new MqttTopicFilter { Topic = topic }, CancellationToken).Wait(CancellationToken);
        }

        public void Unsubscribe(string topic, Action<string, string> subscriber)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace("{ClassName} - {MethodName} - {subscriber}, {topic}",
                    nameof(ScriptingMqttClient), nameof(Unsubscribe), subscriber, topic);

            if (!Subscribers.TryGetValue(topic, out List<Action<string, string>>? value))
                return;

            value.Remove(subscriber);

            if (value.Count == 0)
                Subscribers.Remove(topic);

            MqttClient.UnsubscribeAsync(topic, CancellationToken).Wait(CancellationToken);
        }

        public void Publish(string topic, string payload)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace("{ClassName} - {MethodName} - {topic}, {payload}",
                    nameof(ScriptingMqttClient), nameof(Publish), topic, payload);

            MqttClient.PublishStringAsync(topic, payload, cancellationToken: CancellationToken).Wait(CancellationToken);

            Metrics.MessagesSended.Add(1);
        }
    }
}

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "MqttClient received message for topic: {topic}. Payload: {payload}")]
    public static partial void MessageReceive(this ILogger logger, string topic, string payload);
}
