using Microsoft.AspNet.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Net2SignalRClientConsole
{
    internal class Program
    {
        static readonly IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        static readonly string net2APIURL = configuration["Net2Config:ApiUrl"]!;
        static readonly string clientId = configuration["Net2Config:ClientId"]!;
        static readonly string net2OperatorUsername = configuration["Net2Config:OperatorUsername"]!;
        static readonly string net2OperatorPassword = configuration["Net2Config:OperatorPassword"]!;

        static string apiAccessToken;
        static HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });
        private static HubConnection hubCnn;
        private static IHubProxy net2HubProxy;

        static async Task Main(string[] args)
        {
            //Get a Net2 API Access Key
            await GetNet2AccessToken();

            //Connect to the Net2 SignalR Hub
            ConnectToSignalRHub();

            //Subscribe to the hubs based on config
            var subscribeLiveEvents = configuration.GetValue<bool>("Subscriptions:LiveEvents");
            var liveDoorEvents = configuration.GetSection("Subscriptions:LiveDoorEvents").Get<int[]>() ?? Array.Empty<int>();
            var doorStatusEvents = configuration.GetSection("Subscriptions:DoorStatusEvents").Get<int[]>() ?? Array.Empty<int>();
            var rollCallIds = configuration.GetSection("Subscriptions:RollCall").Get<int[]>() ?? Array.Empty<int>();

            if (subscribeLiveEvents)
                SubscribeToLiveEvents();

            if (liveDoorEvents.Length > 0)
                SubscribeToLiveDoorEvents(liveDoorEvents);

            if (doorStatusEvents.Length > 0)
                SubscribeToDoorStatusEvents(doorStatusEvents);

            foreach (var rollCallId in rollCallIds)
                SubscribeToRollCall(rollCallId);

            Console.Read();
        }

        #region Net2 API Connection
        private static async Task GetNet2AccessToken()
        {
            var payload = new Dictionary<string, string>
            {
                {"username", net2OperatorUsername },
                {"password", net2OperatorPassword },
                {"grant_type", "password" },
                {"client_id", clientId }
            };
            var apiUrl = net2APIURL + "/api/v1/authorization/tokens";
            var apiRequestContent = new FormUrlEncodedContent(payload);
            var apiResponse = await httpClient.PostAsync(apiUrl, apiRequestContent);
            string apiResponseString = await apiResponse.Content.ReadAsStringAsync();
            var resultApiTokenJson = JsonConvert.DeserializeObject<dynamic>(apiResponseString);
            apiAccessToken = resultApiTokenJson.access_token;
            var apiAccessTokenExpiry = resultApiTokenJson.expiry_datetime;
            Console.WriteLine($"Access Token - {apiAccessToken}");
            Console.WriteLine();
            Console.WriteLine($"Access Token Expiry - {apiAccessTokenExpiry}");

        }
        private static void ConnectToSignalRHub()
        {
            hubCnn = new HubConnection(net2APIURL, "token=" + apiAccessToken);
            string net2EventHub = "eventHubLocal";
            net2HubProxy = hubCnn.CreateHubProxy(net2EventHub);

            // Use custom IHttpClient that bypasses SSL validation on .NET 6+
            var sslBypassClient = new SslBypassHttpClient();
            hubCnn.Start(new Microsoft.AspNet.SignalR.Client.Transports.LongPollingTransport(sslBypassClient)).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    throw new Exception($"Error opening the connection: {task.Exception.GetBaseException()}");
                }
            }).Wait();
        }
        #endregion
        #region Hub Subs
        private static void SubscribeToLiveEvents()
        {
            net2HubProxy.Invoke("subscribeToLiveEvents").ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Console.WriteLine("Issue calling send: {0}", task.Exception.GetBaseException());
                    Console.ReadKey();
                }
                else
                {
                    Console.WriteLine("Subscribed to Live Events");
                    IncomingLiveEventHandler();
                }
            }).Wait();
        }
        private static void SubscribeToLiveDoorEvents(IEnumerable<int> doorsToMonitor)
        {
            foreach (var door in doorsToMonitor)
            {
                net2HubProxy.Invoke("subscribeToDoorEvents", door).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Console.WriteLine("Issue calling send: {0}", task.Exception.GetBaseException());
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("Subscribed to Door Events");
                        IncomingLiveDoorEventHandler();
                    }
                }).Wait();
            }
        }
        private static void SubscribeToDoorStatusEvents(IEnumerable<int> doorsToMonitor)
        {
            foreach (var door in doorsToMonitor)
            {
                net2HubProxy.Invoke("subscribeToDoorStatusEvents", door).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Console.WriteLine("Issue calling send: {0}", task.Exception.GetBaseException());
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("Subscribed to Door Status Events");
                        IncomingDoorStatusEventHandler();
                    }
                }).Wait();
            }
        }
        private static void SubscribeToRollCall(int rollCallId)
        {
            net2HubProxy.Invoke("subscribeToRollCallEvents", rollCallId).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Console.WriteLine("Issue calling send: {0}", task.Exception.GetBaseException());
                    Console.ReadKey();
                }
                else
                {
                    Console.WriteLine($"Subscribed to Roll Call {rollCallId}");
                    IncomingRollCallHandler();
                }
            }).Wait();
        }
        #endregion
        #region Event Handlers
        private static void IncomingLiveEventHandler()
        {
            Console.WriteLine("Listening for LiveEvents");

            net2HubProxy.On("liveEvents", t =>
            {
                Console.WriteLine(t);    
            });
        }
        private static void IncomingLiveDoorEventHandler()
        {
            Console.WriteLine("Listening for DoorEvents");
            net2HubProxy.On("doorEvents", t =>
            {
                Console.WriteLine(t);
            });
        }
        private static void IncomingDoorStatusEventHandler()
        {
            Console.WriteLine("Listening for DoorStatusEvents");
            net2HubProxy.On("doorStatusEvents ", t =>
            {
                Console.WriteLine(t);
            });
        }
        private static void IncomingRollCallHandler()
        {
            Console.WriteLine("Listening for Safe/Unsafe Events");
            net2HubProxy.On("rollCallEvents  ", t =>
            {
                Console.WriteLine(t);
            });
        }
        #endregion
    }
}