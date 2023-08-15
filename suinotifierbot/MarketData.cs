using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiNotifierBot
{
	public enum Currency
	{
		Usd = 0,
		Eur = 1,
		Btc = 2,
		Eth = 3,
	}
	public class MarketData
	{
		public decimal price_usd { get; set; }
		public decimal price_btc { get; set; }
		public decimal price_eur { get; set; }

		public decimal CurrencyRate(Currency code) => code switch {
			Currency.Eur => price_eur,
			_ => price_usd,
		};

		public string CurrencyCode(string code) => code switch {
			"eur" => "eur",
			_ => "usd",
		};
	}
}
