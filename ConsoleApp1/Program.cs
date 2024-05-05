using Microsoft.ClearScript.V8;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Server;
using System.Security.Authentication;
using System.Text;

namespace ConsoleApp2
{
    public class Mqtt
    {
        //public delegate void MqttSubscriber(string topic, string payload);

        protected IMqttClient MqttClient { get; init; }
        protected Dictionary<string, List<Action<string, string>>> Subscribers = [];

        public Mqtt(string client, string user, string password, string uri, int port, bool isSecure)
        {
            var messageBuilder = new MqttClientOptionsBuilder()
               .WithClientId(client)
               .WithCredentials(user, password)
               .WithTcpServer(uri, port)
               .WithCleanSession();

            var tlsOptions = new MqttClientTlsOptions()
            {
                SslProtocol = SslProtocols.Tls12,
                IgnoreCertificateChainErrors = true,
                IgnoreCertificateRevocationErrors = true
            };

            var options = isSecure ? messageBuilder.WithTlsOptions(tlsOptions).Build() : messageBuilder.Build();

            var managedOptions = new MqttClientOptionsBuilder()
                .WithClientId(client)
                .WithCredentials(user, password)
                .WithTcpServer(uri, port)
                .WithCleanSession()
                //.WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                //.Opti
                //.WithClientOptions(options)
                .WithTlsOptions(tlsOptions)
                .Build();

            MqttClient = new MqttFactory().CreateMqttClient();

            MqttClient.ConnectAsync(managedOptions).Wait();

            // wait for connection
            while (!MqttClient.IsConnected)
            {
                //Logger.LogTrace("MqttClient not connected... Go to sleep for a second...");
                Thread.Sleep(1000);
            }

            MqttClient.ApplicationMessageReceivedAsync += MessageReceive;
        }

        // receive messaged from all subscribed topics
        private async Task MessageReceive(MqttApplicationMessageReceivedEventArgs e)
        {
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

        public void Subscribe(string queue, Action<string, string> subscriber)
        {
            if (!Subscribers.ContainsKey(queue))
                Subscribers.Add(queue, []);

            Subscribers[queue].Add(subscriber);

            MqttClient.SubscribeAsync(queue);
        }

        public void Publish(string topic, string payload)
        {
            MqttClient.PublishAsync(new MqttApplicationMessage
            {
                Topic = topic,
                PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload))
            });
        }
    }

    internal class Program
    {
        static void Main()
        {
            var MQTTCLIENT = "Test";
            var MQTTUSERNAME = "Test";
            var MQTTPASSWORD = "Test";
            var MQTTURI = "Test";
            var MQTTPORT = 1111;
            var MQTTSECURE = true;
            var mqtt = new Mqtt(MQTTCLIENT, MQTTUSERNAME, MQTTPASSWORD, MQTTURI, MQTTPORT, MQTTSECURE);

            using var engine = new V8ScriptEngine();

            engine.AddHostType(typeof(Console));
            engine.AddHostObject("MQTT", mqtt);
            engine.AddHostType("SubscriberAction", typeof(Action<string, string>));
            engine.Execute(@"
                MQTT.Subscribe('MQTT_PREFIX/DEVICE_ADDRESS',
                    new SubscriberAction(function Message(topic, payload)
                    {
                        let jsonObj = JSON.parse(payload);
                        let stateTo = jsonObj[""state_center""] == ""ON"" ? true : false;

                        Console.WriteLine(stateTo);
                    }));

                Console.WriteLine('{0} is an interesting number.', Math.PI);
            ");

            while (true)
            {
                Thread.Sleep(10);
            }
        }
    }
}
