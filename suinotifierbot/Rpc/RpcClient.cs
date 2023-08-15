using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Dynamic.Json;
using Dynamic.Json.Extensions;
using Microsoft.Extensions.Logging;

namespace SuiNotifierBot.Rpc
{
    public class RpcClient : IDisposable
    {
        static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions {
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 100_000,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };
        
        Uri BaseAddress { get; }
        TimeSpan RequestTimeout { get; }
        DateTime Expiration;
        ILogger logger;

        HttpClient _HttpClient;
        protected HttpClient HttpClient
        {
            get
            {
                lock (this)
                {
                    if (DateTime.UtcNow > Expiration)
                    {
                        _HttpClient?.Dispose();
                        _HttpClient = new HttpClient();

                        _HttpClient.BaseAddress = BaseAddress;
                        _HttpClient.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/json"));
                        _HttpClient.Timeout = RequestTimeout;

                        Expiration = DateTime.UtcNow.AddMinutes(60);
                    }
                }

                return _HttpClient;
            }
        }

#pragma warning disable CS8618
		public RpcClient(string? baseUri, ILogger<RpcClient> logger, int timeoutSec = 60)
#pragma warning restore CS8618
		{
            if (string.IsNullOrEmpty(baseUri))
                throw new ArgumentNullException(nameof(baseUri));

            if (!Uri.IsWellFormedUriString(baseUri, UriKind.Absolute))
                throw new ArgumentException("Invalid URI");

            BaseAddress = new Uri($"{baseUri.TrimEnd('/')}/");
            RequestTimeout = TimeSpan.FromSeconds(timeoutSec);
            this.logger = logger;
        }

        public Task<dynamic> PostJson(object data, bool log = false, CancellationToken cancellationToken = default)
        {
            var content = JsonSerializer.Serialize(data, DefaultOptions);
            if (log)
                logger.LogInformation($"POST {content}");
            var json = PostJson(content, cancellationToken);
            return json;
        }

        async Task<dynamic> PostJson(string content, CancellationToken cancellationToken = default)
        {
            var httpContent = new StringContent(content);
            httpContent.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");
            var response = await HttpClient.PostAsync("", httpContent, cancellationToken);
            await EnsureResponceSuccessfull(response);

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                return await DJson.ParseAsync(stream, DefaultOptions, cancellationToken);
            }
        }

        public void Dispose()
        {
            _HttpClient?.Dispose();
        }

        private async Task EnsureResponceSuccessfull(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var message = response.Content.Headers.ContentLength > 0
                    ? await response.Content.ReadAsStringAsync()
                    : string.Empty;

                throw new RpcException(response.StatusCode, message);
            }
        }

        public async Task<(decimal balance, string addr)> GetSuiBalance(string addr)
        {
            var resp = await PostJson(Request.GetBalance(addr, SuiConstants.SuiCoinType), false);
            Dictionary<string, decimal> balances = new Dictionary<string, decimal>();
            decimal balance = 0;
            decimal stake = 0;
            if (resp.result == null)
                return (0, addr);
            balance = ((string)resp.result.totalBalance).ParseSui();
            return (balance,  addr);
        }
    }
}
