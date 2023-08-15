using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiNotifierBot
{
	internal class Explorer
	{
		//public string block(int blocknumber) => string.Format(blockurl, blocknumber);
		public string account(string addr) => string.Format(accounturl, addr);
		public string op(string ophash) => string.Format(opurl, ophash);
		
		public override string ToString() => name;

		public int id { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public string name { get; set; }
		public string buttonprefix { get; set; }
		//public string blockurl { get; set; }
		public string accounturl { get; set; }
		public string opurl { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		static Dictionary<int, Explorer> explorers;
		static Explorer()
		{
			explorers = JsonConvert.DeserializeObject<Explorer[]>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "explorer.json"))).ToDictionary(o => o.id, o => o);
			//explorers = new Dictionary<int, Explorer>();
			//explorers.Add(0, new Explorer {
			//	accounturl = "https://explorer.sui.io/address/{0}?network=devnet",
			//	opurl = "https://explorer.sui.io/transaction/{0}/?network=devnet"
			//});
		}

		public static Explorer FromId(int id)
		{
			return explorers[id];
		}

		public static Explorer FromStart(string message)
		{
			return explorers.Values.FirstOrDefault(o => message.ToLower().Contains(o.buttonprefix)) ?? explorers[0];
		}

		public static IEnumerable<Explorer> All => explorers.Values;
	}
}
