using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Http;
using System.Net.Http.Headers;

namespace Net2SignalRClientConsole
{
    /// <summary>
    /// Custom IHttpClient for SignalR 2.x that bypasses SSL certificate validation on .NET 6+.
    /// WARNING: Only use in development/testing environments.
    /// </summary>
    internal class SslBypassHttpClient : IHttpClient
    {
        private readonly HttpClient _httpClient;
        private IConnection _connection;

        public SslBypassHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler);
        }

        public void Initialize(IConnection connection)
        {
            _connection = connection;
        }

        public async Task<IResponse> Get(string url, Action<IRequest> prepareRequest, bool isLongRunning)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var requestWrapper = new HttpRequestMessageWrapper(request);
            prepareRequest(requestWrapper);
            ApplyConnectionHeaders(request);

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return new HttpResponseMessageWrapper(response);
        }

        public async Task<IResponse> Post(string url, Action<IRequest> prepareRequest, IDictionary<string, string> postData, bool isLongRunning)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            var requestWrapper = new HttpRequestMessageWrapper(request);
            prepareRequest(requestWrapper);
            ApplyConnectionHeaders(request);

            if (postData != null)
            {
                request.Content = new FormUrlEncodedContent(postData);
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return new HttpResponseMessageWrapper(response);
        }

        private void ApplyConnectionHeaders(HttpRequestMessage request)
        {
            if (_connection?.Headers != null)
            {
                foreach (var header in _connection.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }
    }

    internal class HttpRequestMessageWrapper : IRequest
    {
        private readonly HttpRequestMessage _request;
        private CancellationTokenSource _cts;

        public HttpRequestMessageWrapper(HttpRequestMessage request)
        {
            _request = request;
            _cts = new CancellationTokenSource();
        }

        public string UserAgent
        {
            get => _request.Headers.UserAgent?.ToString();
            set
            {
                _request.Headers.UserAgent.Clear();
                if (!string.IsNullOrEmpty(value))
                    _request.Headers.TryAddWithoutValidation("User-Agent", value);
            }
        }

        public string Accept
        {
            get => _request.Headers.Accept?.ToString();
            set
            {
                _request.Headers.Accept.Clear();
                if (!string.IsNullOrEmpty(value))
                    _request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(value));
            }
        }

        public void Abort()
        {
            _cts?.Cancel();
        }

        public void SetRequestHeaders(IDictionary<string, string> headers)
        {
            if (headers == null) return;
            foreach (var header in headers)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    internal class HttpResponseMessageWrapper : IResponse
    {
        private readonly HttpResponseMessage _response;

        public HttpResponseMessageWrapper(HttpResponseMessage response)
        {
            _response = response;
        }

        public Stream GetStream()
        {
            return _response.Content.ReadAsStream();
        }

        public void Dispose()
        {
            _response?.Dispose();
        }
    }
}
