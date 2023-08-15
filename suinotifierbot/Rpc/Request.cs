using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SuiNotifierBot.Rpc
{
	internal class Request
	{
		public string jsonrpc { get; set; }
		public string id { get; set; }
		public string method { get; set; }
		public object[] @params { get; set; }

		private Request(string method, params object[] p)
		{
			jsonrpc = "2.0";
			this.method = method;
			id = Guid.NewGuid().ToString();
			@params = p ?? new object[0];
		}

		public static Request QueryTransactionBlocks(string cursor, int limit)
		{
			return new Request("suix_queryTransactionBlocks", new {
				options = new {
					showBalanceChanges = true,
					showEvents = true
				}
			},
			cursor,
			limit,
			false);
		}

		public static Request QueryTransactionBlocks()
		{
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
			return new Request("suix_queryTransactionBlocks", new {
				options = null as object
			},
			null,
			1,
			true);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
		}

		//public static Request GetTotalTransactionBlocks()
		//{
		//	return new Request("sui_getTotalTransactionBlocks");
		//}

		public static Request GetBalance(string address, string coinType)
		{
			return new Request("suix_getBalance", address, coinType);
		}

		public static Request GetStakes(string address)
		{
			return new Request("suix_getStakes", address);
		}

		//public static Request GetEvents((string txDigest, int eventSeq) cursor, int limit)
		//{
		//	return new Request("suix_queryEvents", new {
		//		MoveModule = new {
		//			package = "0x2",
		//			module = "pay_sui"
		//		}
		//	}, new { cursor.txDigest, cursor.eventSeq }, limit, false);
		//}

		public static Request GetLatestSuiSystemState() => new Request("suix_getLatestSuiSystemState");
		public static Request GetValidatorsApy() => new Request("suix_getValidatorsApy");

		public override string ToString()
		{
			return JsonSerializer.Serialize(this);
		}
	}
}
