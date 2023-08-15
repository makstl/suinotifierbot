using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiNotifierBot
{
	public class SuiChartService
    {
        protected readonly IServiceProvider serviceProvider;
        protected readonly ILogger logger;
        protected readonly IFormatProvider formatProvider;
        CryptoCompare.CryptoCompareClient client;

        public SuiChartService(IServiceProvider serviceProvider)
        {
            logger = serviceProvider.GetRequiredService<ILogger<SuiChartService>>();
            formatProvider = serviceProvider.GetRequiredService<IFormatProvider>();
            client = serviceProvider.GetRequiredService<CryptoCompare.CryptoCompareClient>();
            this.serviceProvider = serviceProvider;
        }

        public async Task<(MemoryStream, string)> GetMessage()
        {
            var data = await client.GetOHLC();
            
            var chartStream = new MemoryStream();
            var pc = new PriceChart();
            pc.ImageResource = "sui.jpg";
            pc.GraphColor = new SkiaSharp.SKColor(0xFF1b2d4d);
            pc.Create(data, chartStream);
            chartStream.Position = 0;
            var prev24 = data.Where(o => o.TimeStamp > data.Last().TimeStamp.AddHours(-24)).ToList();
            var delta24 = data.Last().Close - prev24.First().Close;
            string text = $@"<b>${data.Last().Close.ToString("###,###,##0.00", formatProvider)} {(delta24 > 0 ? "📈" : "📉")} {(delta24 / prev24.First().Close).ToString("+0.0%;-0.0%", formatProvider)}</b> | 24h

Vol. 24h: {prev24.Sum(o => o.Volume).ToString("###,###,###,###,###,###,###", formatProvider)} USD

<i>Upd.: {DateTime.UtcNow.ToString("MMM d, HH:mm", formatProvider)} UTC</i>, <a href='https://www.cryptocompare.com/coins/sui/overview'>CryptoCompare</a>";

            return (chartStream, text);
        }

        

    }
}
