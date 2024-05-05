using Microsoft.ClearScript;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System.Security.Authentication;
using System.Text;

namespace HomeAutomationScriptingService.ScriptingObjects
{
    public interface IMqttClient
    {
        bool IsMqttConnected { get; }

        void Publish(string topic, string payload);
        void Subscribe(string topic, Action<string, string> subscriber);
        void Unsubscribe(Action<string, string> subscriber, string topic);
    }

    public class MqttClient : ScriptingConfigurableObject<MqttClientConfiguration>, IMqttClient
    {
        protected IManagedMqttClient ManagedMqttClient { get; }
        protected Dictionary<string, List<Action<string, string>>> Subscribers { get; } = [];

        public bool IsMqttConnected => ManagedMqttClient.IsConnected;

        public override void InitScriptEngine(IScriptEngine scriptEngine)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {scriptEngine}",
                nameof(MqttClient), nameof(InitScriptEngine), scriptEngine);

            scriptEngine.AddHostObject("MQTT", this);
            scriptEngine.AddHostType(typeof(Console));
            scriptEngine.AddHostType("SubscriberAction", typeof(Action<string, string>));
        }

        public MqttClient(ILogger<MqttClient> logger, IOptions<MqttClientConfiguration> configuration)
            : base(logger, configuration)
        {
            Logger.LogInformation("Creating MqttClient at: {time}. Uri:{uri}", DateTimeOffset.Now, Configuration.MqttUri);

            var tlsOptions = new MqttClientTlsOptions()
            {
                SslProtocol = Configuration.MqttSecure ? SslProtocols.Tls12 : SslProtocols.None,
                IgnoreCertificateChainErrors = true,
                IgnoreCertificateRevocationErrors = true
            };

            var options = new MqttClientOptionsBuilder()
                .WithClientId(Configuration.ClientId.Replace("-", "").Replace(" ", ""))
                .WithCredentials(Configuration.MqttUser, Configuration.MqttUserPassword)
                .WithTcpServer(Configuration.MqttUri, Configuration.MqttPort)
                .WithCleanSession()
                .WithTlsOptions(tlsOptions)
                .Build();

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(options)
                .Build();

            ManagedMqttClient = new MqttFactory()
                .CreateManagedMqttClient();

            ManagedMqttClient.StartAsync(managedOptions).Wait();

            // wait for connection
            while (!ManagedMqttClient.IsConnected)
            {
                Logger.LogTrace("MqttClient not connected... Go to sleep for a second...");
                Thread.Sleep(1000);
            }

            ManagedMqttClient.ApplicationMessageReceivedAsync += MessageReceive;

            Logger.LogInformation("Creating MqttClient done at: {time}", DateTimeOffset.Now);
        }

        // receive messaged from all subscribed topics
        private async Task MessageReceive(MqttApplicationMessageReceivedEventArgs e)
        {
            Logger.LogTrace("MqttClient received message for topic: {topic}. Payload: {payload}",
                e.ApplicationMessage.Topic,
                Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));

            foreach (var subscriber in Subscribers[e.ApplicationMessage.Topic])
            {
                try
                {
                    subscriber(e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
                }
                catch { }
            }

            await Task.Delay(0);
        }

        public void Subscribe(string topic, Action<string, string> subscriber)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {topic}, {subscriber}",
                nameof(MqttClient), nameof(Subscribe), topic, subscriber);

            if (!Subscribers.ContainsKey(topic))
                Subscribers.Add(topic, []);

            Subscribers[topic].Add(subscriber);

            ManagedMqttClient.SubscribeAsync(topic).Wait();
        }

        public void Unsubscribe(Action<string, string> subscriber, string topic)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {subscriber}, {topic}",
                nameof(MqttClient), nameof(Unsubscribe), subscriber, topic);

            if (!Subscribers.TryGetValue(topic, out List<Action<string, string>>? value))
                return;

            value.Remove(subscriber);

            if (value.Count == 0)
                Subscribers.Remove(topic);

            ManagedMqttClient.UnsubscribeAsync(topic).Wait();
        }

        public void Publish(string topic, string payload)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {topic}, {payload}",
                nameof(MqttClient), nameof(Publish), topic, payload);

            ManagedMqttClient.EnqueueAsync(topic, payload).Wait();
        }
    }
}
