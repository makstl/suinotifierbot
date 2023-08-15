using SuiNotifierBot;
using SuiNotifierBot.Rpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiNotifierBot
{
	public class Validators
	{
		Dictionary<string, Validator> validators = new Dictionary<string, Validator>();
		DateTime dateReceived;

		public async Task LoadValidators(RpcClient rpc)
		{
			var resp1 = await rpc.PostJson(Request.GetLatestSuiSystemState());
			var resp2 = await rpc.PostJson(Request.GetValidatorsApy());

			var result = new Dictionary<string, Validator>();
			var resultApy = new Dictionary<string, decimal>();
			foreach (var apy in resp2.result.apys)
				resultApy.Add((string)apy.address, (decimal)apy.apy);

			foreach (var v in resp1.result.activeValidators)
			{
				var validator = new Validator {
					Address = v.suiAddress,
					Title = v.name,
					Stake = ((string)v.nextEpochStake).ParseSui(),
					APY = resultApy[v.suiAddress]
				};
				result[v.suiAddress] = validator;
			}

			validators = result;
			dateReceived = DateTime.Now;
		}

		public Validator? this[string address] => validators.ContainsKey(address) ? validators[address] : null;
		public DateTime DateReceived => dateReceived;
	}

	public class Validator
	{
		public string Address { get; set; } = "";
		public string Title { get; set; } = "";
		public decimal Stake { get; set; }
		public decimal APY { get; set; }
	}
}
