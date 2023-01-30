using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
using System.Net;

namespace Net2SignalRClientConsole
{
    internal class Program
    {
        readonly static string net2APIURL = "http://localhost:8080";
        readonly static string clientId = "";
        readonly static string net2OperatorUsername = "System engineer";
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

            //Subscribe 
            SubscribeToLiveEvents();

            IncomingLiveEventHandler();
            Console.Read();
        }


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
        private static void IncomingLiveEventHandler()
        {
            //List<int> ValidEventTypes = new List<int> { 15, 20, 21, 22, 26, 27, 30, 110 };
            net2HubProxy.On("liveEvents", t =>
            {
                Console.WriteLine(t);    
            });
        }
    }
}