/*
using System.Linq;
using suinotifierbot.Rpc;

var baseUri = "https://fullnode.devnet.sui.io:443";

var rpc = new RpcClient(baseUri);

var tn_resp = await rpc.PostJson(Request.GetTotalTransactionNumber());

ulong tn_max = (ulong)tn_resp.result;
Console.WriteLine($"Tx count: {tn_max}");

List<string> txlist = new List<string>() { "34JicC4EyQKK9iy64JNVt8v9ZjCpKvDtesT5UZqwa5hd" };
ulong tn_last = tn_max;
ulong tn_start = tn_max;
uint p = 0;
do
{
	List<object> rq = new List<object>();
	rq.Add(Request.GetTotalTransactionNumber());
	var range = tn_max - tn_last;
	if (range > 1024)
		range = 1024;
	rq.Add(Request.GetTransactionsInRange(tn_last, tn_last + range + 1));
	if (txlist != null)
		foreach (var tx in txlist)
			rq.Add(Request.GetTransaction(tx));

	var resp = await rpc.PostJson(rq);
	int count = resp.Count;
	
	tn_max = (ulong)resp[0].result;
	txlist = (List<string>)resp[1].result;
	tn_last = tn_last + range;
	
	for(int i = 2; i < count; i++)
	{
		p++;
		Dictionary<(string from, string to), decimal> txs = new Dictionary<(string from, string to), decimal>();
		foreach (var e in resp[i].result.effects.events)
		{
			if (e.coinBalanceChange?.transactionModule == "pay_sui" &&
				e.coinBalanceChange?.changeType == "Receive" &&
				e.coinBalanceChange?.coinType == "0x2::sui::SUI")
			{
				string sender = e.coinBalanceChange.sender;
				ulong amount_ul = e.coinBalanceChange.amount;
				decimal sui = amount_ul / 1000000000M;
				string receiver = e.coinBalanceChange.owner.AddressOwner;
				if (!txs.ContainsKey((sender, receiver)))
					txs.Add((sender, receiver), sui);
				else
					txs[(sender, receiver)] = txs[(sender, receiver)] + sui;
			}
		}
		if (txs.Count > 0)
		{
			var rq1 = txs.Select(o => o.Key.to).Distinct().Select(o => { var r = Request.GetAllBalances(o); r.id = o; return r; }).ToArray();
			var resp1 = await rpc.PostJson(rq1);
			Dictionary<string, decimal> balances = new Dictionary<string, decimal>();
			foreach (var balance in resp1)
			{
				foreach (var coin in balance.result)
					if (coin.coinType == "0x2::sui::SUI")
						balances[(string)balance.id] = (ulong)coin.totalBalance / 1000000000M;
			}
			foreach (var tx in txs)
				Console.WriteLine($"Tx of {tx.Value} SUI from {tx.Key.from} to {tx.Key.to} ({balances[tx.Key.to]} SUI)");
		}		
	}

	Console.Write($"TxCount: {tn_max}, TxLast: {tn_last}, Processed: {p}, Delta: {tn_last - tn_start - p}");
	Console.CursorLeft = 0;
} while(true);
*/