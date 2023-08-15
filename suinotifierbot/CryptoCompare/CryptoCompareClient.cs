using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiNotifierBot.CryptoCompare
{
	public class CryptoCompareClient
	{
        ILogger<CryptoCompareClient> logger;
        HttpClient http;

        string _cryptoCompareToken;
        public CryptoCompareClient(string apiKey, HttpClient http, ILogger<CryptoCompareClient> logger)
        {
            this.logger = logger;
            this.http = http;
            _cryptoCompareToken = apiKey;

        }
        public MarketData GetMarketData()
        {
            string str =
                Download(
                    $"https://min-api.cryptocompare.com/data/price?fsym={Symbol}&tsyms=BTC,USD,EUR,ETH&api_key={_cryptoCompareToken}");
            var dto = JsonConvert.DeserializeObject<CryptoComparePrice>(str);
            return new MarketData {
                price_usd = dto.USD,
                price_btc = dto.BTC,
                price_eur = dto.EUR
            };
        }
        string Download(string addr)
        {
            try
            {
                logger.LogDebug("download " + addr);
                // TODO: Make requests async
                var result = http.GetStringAsync(addr)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                logger.LogDebug("download complete: " + addr);
                return result;
            }
            catch (HttpRequestException we)
            {
                logger.LogError(we, "Error downloading from " + addr);
                throw;
            }
        }
        const string Symbol = "SUI";
        public async Task<List<OHLC>> GetOHLC()
        {
            var histUrl = $"https://min-api.cryptocompare.com/data/v2/histominute?fsym={Symbol}&tsym=USDT&limit=1440&api_key={_cryptoCompareToken}";
            var httpClient = new HttpClient();
            var histDataStr = await httpClient.GetStringAsync(histUrl);
            var histData1 = JsonConvert.DeserializeObject<CryptoCompare.HistohourResult>(histDataStr);
            //histDataStr = await httpClient.GetStringAsync(histUrl + $"&toTs={histData1.Data.TimeFrom}");
            //var histData2 = JsonConvert.DeserializeObject<CryptoCompare.HistohourResult>(histDataStr);
            //histDataStr = await httpClient.GetStringAsync(histUrl + $"&toTs={histData2.Data.TimeFrom}");
            //var histData3 = JsonConvert.DeserializeObject<CryptoCompare.HistohourResult>(histDataStr);
            List<OHLC> result = new List<OHLC>();
            //result.AddRange(histData3.Data.Data.Select(x => new OHLC { Open = x.open, High = x.high, Low = x.low, Close = x.close, TimeStamp = x.Timestamp, Volume = x.volumeto }));
            //result.AddRange(histData2.Data.Data.Select(x => new OHLC { Open = x.open, High = x.high, Low = x.low, Close = x.close, TimeStamp = x.Timestamp, Volume = x.volumeto }));
            result.AddRange(histData1.Data.Data.Select(x => new OHLC { Open = x.open, High = x.high, Low = x.low, Close = x.close, TimeStamp = x.Timestamp, Volume = x.volumeto }));
            return result;
        }

        public class CryptoComparePrice
        {
            public decimal BTC { get; set; }
            public decimal ETH { get; set; }
            public decimal USD { get; set; }
            public decimal EUR { get; set; }
        }
    }
}
