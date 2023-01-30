using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
using System.Net;

namespace Net2SignalRClientConsole
{
    internal class Program
    {
        //Net2 Local API Address
        readonly static string net2APIURL = "http://localhost:8080";
        //Net2 API client ID. This is the name of the licence file provided to you. This is a GUID.
        readonly static string clientId = "";
        //Net2 Operator. This can be any Net2 operator.
        readonly static string net2OperatorUsername = "OEM Client";
        //Password for the above user.
        readonly static string net2OperatorPassword = "admin";

        static string apiAccessToken;
        static HttpClient httpClient = new HttpClient();
        private static HubConnection hubCnn;
        private static IHubProxy net2HubProxy;

        static async Task Main(string[] args)
        {
            //Get a Net2 API Access Key
            await GetNet2AccessToken();

            //Connect to the Net2 SignalR Hub
            ConnectToSignalRHub();

            //Subscribe to the hubs. 

            //SubscribeToLiveEvents();
            //SubscribeToDoorStatusEvents(new List<int> { 7898066 });
            //SubscribeToDoorStatusEvents(new List<int> { 7898066 });
            //SubscribeToRollCall(1);
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
            hubCnn.Start().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    throw new Exception(string.Format("Error opening the connection:{ 0 }", task.Exception.GetBaseException()));
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