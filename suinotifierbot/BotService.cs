using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model;
using SuiNotifierBot.Rpc;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SuiNotifierBot
{
	public partial class BotService : BackgroundService
    {
        readonly IServiceProvider serviceProvider;
        readonly ILogger logger;
        readonly TelegramBotClient telegramBotClient;
        readonly IFormatProvider formatProvider;
        readonly MessageSender messageSender;
        readonly SuiChartService chartService;
        readonly long adminChannel;
        readonly long supportGroup;
        readonly Validators validators;

        public BotService(IServiceProvider serviceProvider, ILogger<BotService> logger, TelegramBotClient telegramBotClient, IFormatProvider formatProvider, MessageSender messageSender, SuiChartService chartService, Validators validators)
		{
			this.serviceProvider = serviceProvider;
			this.telegramBotClient = telegramBotClient;
			this.formatProvider = formatProvider;
			this.logger = logger;
			this.messageSender = messageSender;
			var to = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
			this.adminChannel = to.AdminChannel;
			this.supportGroup = to.SupportGroup;
			this.chartService = chartService;
			this.validators = validators;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            telegramBotClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, null, stoppingToken);

            while (stoppingToken.IsCancellationRequested is false)
            {
                try
                {
                    await Run(stoppingToken);
                }
				catch (TaskCanceledException tce)
				{
                    logger.LogError(tce, tce.Message);
				}
                catch (Exception e)
                {
                    logger.LogError(e, e.Message);
                    if (!e.Message.Contains("<h1>504 Gateway Time-out</h1>"))
                        await messageSender.SendAdminMessage("🛑 " + e.GetType().Name + ": " + e.Message);
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
                
        //DateTime lastMDreceived;

        string cursor;
        DateTime lastDateTime;
        async Task Run(CancellationToken stoppingToken)
        {
            using var scope = serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;
            using var db = provider.GetRequiredService<BotDbContext>();
            var rpc = provider.GetRequiredService<RpcClient>();

            if (DateTime.Now.Subtract(validators.DateReceived).TotalHours > 4)
                await validators.LoadValidators(rpc);

            ulong timestamp = 0;
            if (cursor == null)
			{
                var lt = db.LastTransaction.SingleOrDefault();
                if (lt == null)
				{
                    logger.LogInformation($"suix_queryTransactionBlocks");
                    var q_resp = await rpc.PostJson(Request.QueryTransactionBlocks());
                    var last_digest = (string)q_resp.result.data[0].digest;

                    lt = new LastTransaction { Digest = last_digest, Timestamp = DateTime.Now };
                    db.LastTransaction.Add(lt);
                    db.SaveChanges();
                }

                var bot = await telegramBotClient.GetMeAsync();
                await messageSender.SendAdminMessage($"@{bot.Username} v.{GetType().Assembly.GetName().Version?.ToString(3)} started from tx {lt.Digest} at {lt.Timestamp}");
                cursor = lt.Digest;
                timestamp = (ulong)lt.Timestamp.Subtract(DateTime.UnixEpoch).TotalMilliseconds;
            }
            //Thread.Sleep(1000000);
            //cursor = "FTdsVZgfwbWMS5hToZvwkDGk7VXcrRjWjrNEyWDUqA7v";// "4fHhXcE2yDMYNtSZ9nQ2jExvDmxJmgzKWn8xYpDqeZCv";
            int count = 0;
            List<dynamic> eventsToProcess = new List<dynamic>();
            for (int i = 0; i < 10; i++)
            {
                //logger.LogInformation($"QueryTransactionBlocks, cursor: {cursor}");
                var ev_resp = await rpc.PostJson(Request.QueryTransactionBlocks(cursor, 50), false);
                if (ev_resp?.result?.data != null)
                {
                    foreach (var tx in ev_resp.result.data)
                    {
                        if (tx.balanceChanges.Count > 0)
                            eventsToProcess.Add(tx);
                        count++;
                        if (tx.timestampMs != null)
                            timestamp = (ulong)tx.timestampMs;
                        cursor = tx.digest;
                    }
                    //logger.LogInformation($"Process {eventsToProcess.Count} events");
                    await ProcessTx(eventsToProcess, db, rpc);
                    eventsToProcess.Clear();
                }
                if (stoppingToken.IsCancellationRequested)
                    return;
            }
            if (count > 0)
            {
                db.LastTransaction.Remove(db.LastTransaction.Single());
                var lt1 = new LastTransaction(cursor, timestamp);
                db.LastTransaction.Add(lt1);
                db.SaveChanges();
                lastDateTime = lt1.Timestamp;
                logger.LogInformation($"Processed {count} ({eventsToProcess.Count}) txs, last tx {cursor} at {lt1.Timestamp.ToString("dd.MM.yyyy HH:mm:ss")}");
            }
            //Console.WriteLine($"Processed {count} ({eventsToProcess.Count}) txs, last tx {cursor} at {lastDateTime.ToString("dd.MM.yyyy HH:mm:ss")}   ");
            //Console.CursorTop--;
            //
        }
        
        async Task ProcessTx(List<dynamic> events, BotDbContext db, RpcClient rpc)
		{
            Dictionary<(string from, string to, string tx), decimal> txs = new Dictionary<(string from, string to, string tx), decimal>();
            foreach (var ev in events)
            {
                var tx = (string)ev.digest;
                List<(string, decimal)> receivers = new List<(string, decimal)>();
                string sender = "";
                decimal amount = 0;
                foreach (var bc in ev.balanceChanges)
                {
                    if ((string)bc.coinType != SuiConstants.SuiCoinType)
                        continue;
                    amount = ((long)bc.amount).ToString().ParseSui();
                    if (amount > 0)
                        receivers.Add((bc.owner.AddressOwner, amount));
                    else
                        sender = bc.owner.AddressOwner;
                }
                if (ev.events == null)
                    continue;
                foreach (var e in ev.events)
				{
                    if (e.type == "0x3::validator::StakingRequestEvent")
					{
                        var stakedAmount = ((long)e.parsedJson.amount).ToString().ParseSui();
                        var delegator = (string)e.parsedJson.staker_address;
                        var validator = validators[(string)e.parsedJson.validator_address];
                        var to_addr = db.UserAddress.Include(x => x.User).Where(x => x.Address == validator.Address && x.NotifyDelegations && !x.User.Inactive).ToList();
                        foreach (var ua in to_addr)
                        {
                            var fromName = (db.UserAddress.FirstOrDefault(o => o.Address == delegator && o.UserId == ua.UserId)?.Title ?? db.PublicAddress.SingleOrDefault(o => o.Address == delegator)?.Title);
                            if (fromName == null)
                                fromName = delegator.ShortAddr();
                            string result =
                                $"🤝 New <a href='{Explorer.FromId(ua.User.Explorer).op(tx)}'>delegation</a> of <b>{stakedAmount.SuiToString()}</b> to {ua.Link} from <a href='{Explorer.FromId(ua.User.Explorer).account(delegator)}'>{fromName}</a>";

                            if (!ua.User.HideHashTags)
                                result += "\n\n#delegation" + ua.HashTag();
                            await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                        }
                    }
				}
                if (receivers.Count == 0 && sender == "")
                    continue;
                if (receivers.Count == 0)
				{
                    txs.Add((sender, "", tx), amount);
                }                
                foreach (var receiver in receivers)
                {
                    if (!txs.ContainsKey((sender, receiver.Item1, tx)))
                        txs.Add((sender, receiver.Item1, tx), receiver.Item2);
                    else
                        txs[(sender, receiver.Item1, tx)] = (txs[(sender, receiver.Item1, tx)] + receiver.Item2);
                }
            }
            
            foreach(var tx in txs.Keys.ToList())
			{
                if (!db.UserAddress.Any(x => !x.User.Inactive && x.NotifyTransactions && (tx.from == x.Address || tx.to == x.Address)))
                    txs.Remove(tx);
            }

            if (txs.Count == 0)
                return;

            var rq1 = txs.Select(o => o.Key.to).Union(txs.Select(o => o.Key.from)).Distinct().Select(async o => { return await rpc.GetSuiBalance(o); }).Select(o => o.Result);
            foreach (var balance in rq1)
            {
                balances[balance.addr] = balance;
                Console.WriteLine($"{balance.addr}: {balance.balance} SUI");
            }            

            var fromList = txs.Where(o => o.Key.to != "").GroupBy(o => o.Key.to).ToList();
            foreach (var to in fromList)
            {
                var to_addr = db.UserAddress.Include(x => x.User).Where(x => x.Address == to.Key && x.NotifyTransactions && !x.User.Inactive).ToList();
                if (to_addr.Count == 0)
                    continue;

                foreach (var ua in to_addr)
                {
                    var items = new List<(string addr, string name, string hash, string tx, decimal value)>();
                    foreach (var from in to)
                    {
                        if (from.Key.from != "")
                        {
                            var fromName = (db.UserAddress.FirstOrDefault(o => o.Address == from.Key.from && o.UserId == ua.UserId)?.Title ?? db.PublicAddress.SingleOrDefault(o => o.Address == from.Key.from)?.Title);
                            string fromTag;
                            if (fromName == null)
                            {
                                fromName = from.Key.from.ShortAddr();
                                fromTag = from.Key.from.HashTag();
                            }
                            else
                                fromTag = " #" + System.Text.RegularExpressions.Regex.Replace(fromName.ToLower(), "[^a-zа-я0-9]", "");
                            items.Add((from.Key.from, fromName, fromTag, from.Key.tx, from.Value));
                        }
                        else
                            items.Add(("", "", "", from.Key.tx, from.Value));
					}
					foreach (var item in items.Where(o => o.addr != ""))
					{
                        string result =
                            $"✅ Incoming <a href='{Explorer.FromId(ua.User.Explorer).op(item.tx)}'>transaction</a> of <b>{item.value.SuiToString()}</b> to {ua.Link} from <a href='{Explorer.FromId(ua.User.Explorer).account(item.addr)}'>{item.name}</a>";

                        result += "\n\n<b>Balance</b>: " + balances[to.Key].balance.SuiToString();
                        if (!ua.User.HideHashTags)
                            result += "\n\n#incoming" + ua.HashTag() + item.hash;
                        await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                    }
                    
                    var depositAmount = items.Where(o => o.addr == "").Sum(o => o.value);
                    if (depositAmount != 0)
					{
                        string result = $"🔸 Incoming <a href='{Explorer.FromId(ua.User.Explorer).op(items[0].tx)}'>transaction</a> of {depositAmount.SuiToString()} to {ua.Link}";

                        result += "\n\n<b>Balance</b>: " + balances[to.Key].balance.SuiToString();
                        if (!ua.User.HideHashTags)
                            result += "\n\n#incoming" + ua.HashTag();
                        await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                    }
                }                
            }

            var toList = txs.Where(o => o.Key.from != null).GroupBy(o => o.Key.from).ToList();
            foreach (var from in toList)
            {
                var from_addr = db.UserAddress.Include(x => x.User).Where(x => x.Address == from.Key && x.NotifyTransactions && !x.User.Inactive).ToList();
                if (from_addr.Count == 0)
                    continue;

                foreach (var ua in from_addr)
                {
                    var items = new List<(string addr, string name, string hash, string tx, decimal value)>();
                    foreach (var to in from)
                    {
                        if (to.Key.to != "")
                        {
                            var toName = (db.UserAddress.FirstOrDefault(o => o.Address == to.Key.to && o.UserId == ua.UserId)?.Title ?? db.PublicAddress.SingleOrDefault(o => o.Address == to.Key.to)?.Title);
                            string toTag;
                            if (toName == null)
                            {
                                toName = to.Key.to.ShortAddr();
                                toTag = to.Key.to.HashTag();
                            }
                            else
                                toTag = " #" + System.Text.RegularExpressions.Regex.Replace(toName.ToLower(), "[^a-zа-я0-9]", "");
                            if (ua.User.HideHashTags)
                                toTag = "";
                            items.Add((to.Key.to, toName, toTag, to.Key.tx, to.Value));
                        }
                        else
                            items.Add(("", "", "", to.Key.tx, to.Value));
                    }
                    if (items.Count == 1 && items[0].addr != "")
                    {
                        string result = $"❎ Outgoing <a href='{Explorer.FromId(ua.User.Explorer).op(items[0].tx)}'>transaction</a> of <b>{items[0].value.SuiToString()}</b> from {ua.Link} to <a href='{Explorer.FromId(ua.User.Explorer).account(items[0].addr)}'>{items[0].name}</a>";
                        result += "\n\n<b>Balance</b>: " + balances[from.Key].balance.SuiToString();
                        if (!ua.User.HideHashTags)
                            result += "\n\n#outgoing" + ua.HashTag() + items[0].hash;
                        await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                    }
                    else if (items.Count(o => o.addr != "") > 0)
                    {
                        items.Sort((i1, i2) => i1.value < i2.value ? 1 : i1.value > i2.value ? -1 : 0);
                        string header = $"❎ Outgoing <a href='{Explorer.FromId(ua.User.Explorer).op(items.First().tx)}'>transactions</a> of <b>{items.Sum(i => i.value).SuiToString()}</b> from " + ua.Link + ":\n";
                        string itemsList = "";
                        string tagList = !ua.User.HideHashTags ? "\n\n#outgoing" + ua.HashTag() : "";
                        string balance = "\n<b>Balance</b>: " + balances[from.Key].balance.SuiToString();
                        while (items.Count(o => o.addr != "") > 0)
                        {
                            var item = $"▫️<b>{items[0].value.SuiToString()}</b> to <a href='{Explorer.FromId(ua.User.Explorer).account(items[0].addr)}'>{items[0].name}</a>\n";
                            if ((header + itemsList + balance + item + tagList).Length > 4000)
                            {
                                string result = header + itemsList + $"<i>and {items.Count} more transactions...</i>\n" + balance + tagList;
                                await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                                itemsList = "";
                                tagList = !ua.User.HideHashTags ? "\n\n#outgoing" + ua.HashTag() : "";
                                break;
                            }
                            itemsList += item;
                            //tagList += items[0].hash;
                            items.RemoveAt(0);
                        }
                        if (itemsList.Length > 0)
                        {
                            string result = header + itemsList + balance + tagList;
                            await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                        }
                    }
                    else if (items.Count(o => o.addr == "") > 0)
                    {
                        var withdrawalAmount = -items.Where(o => o.addr == "").Sum(o => o.value);
                        if (withdrawalAmount != 0)
                        {
                            string result = $"🔹 Outgoing <a href='{Explorer.FromId(ua.User.Explorer).op(items[0].tx)}'>transaction</a> of {withdrawalAmount.SuiToString()} from {ua.Link}";

                            result += "\n\n<b>Balance</b>: " + balances[from.Key].balance.SuiToString();
                            if (!ua.User.HideHashTags)
                                result += "\n\n#outgoing" + ua.HashTag();
                            await messageSender.SendMessage(ua.UserId, result, ReplyKeyboards.MainMenu);
                        }
                    }
                }
            }

        }
        
        async Task OnSql(System.Data.Common.DbConnection conn, string sql)
        {
            try
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var result = new List<string[]>();
                try
                {
                    conn.Open();
                    using var reader = cmd.ExecuteReader();

                    if (reader.HasRows is false)
                    {
                        result.Add(new[] { $"{reader.RecordsAffected} records affected" });
                    }
                    else
                    {
                        var data = new string[reader.FieldCount];
                        for (var i = 0; i < data.Length; i++)
                            data[i] = reader.GetName(i);

                        result.Add(data);
                        while (reader.Read())
                        {
                            data = new string[reader.FieldCount];
                            for (var i = 0; i < data.Length; i++)
                                data[i] = reader.GetValue(i)?.ToString() ?? "NULL";
                            result.Add(data);
                        }
                    }
                }
                finally
                {
                    conn.Close();
                }
                string allData = String.Join("\r\n", result.Select(o => String.Join(';', o)).ToArray());
                if (result[0].Length <= 3 && result.Count <= 20)
                    await messageSender.SendAdminMessage(allData.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`"));
                else
                {
                    string fileName = "result.txt";
                    if (allData.Length > 100000)
                    {
                        using (var zip = new MemoryStream())
                        {
                            using (var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, true))
                            {
                                var entry = archive.CreateEntry("result.txt");
                                using (StreamWriter writer = new StreamWriter(entry.Open()))
                                    writer.Write(allData);
                            }
                            zip.Seek(0, SeekOrigin.Begin);
                            var f = Telegram.Bot.Types.InputFile.FromStream(zip, fileName + ".zip");
                            await telegramBotClient.SendDocumentAsync(adminChannel, f);
                        }
                    }
					else
					{
                        var f = Telegram.Bot.Types.InputFile.FromStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(allData)), fileName);
                        await telegramBotClient.SendDocumentAsync(adminChannel, f);
                    }

                }
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                await messageSender.SendAdminMessage(e.Message);
            }
        }
        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type != UpdateType.CallbackQuery &&
                update.Type != UpdateType.ChannelPost &&
                update.Type != UpdateType.Message)
                return;
            try
            {
                using var scope = serviceProvider.CreateScope();
                var provider = scope.ServiceProvider;
                using var db = provider.GetRequiredService<BotDbContext>();
                var rpc = provider.GetRequiredService<RpcClient>();
                Model.User u;

                if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null && update.CallbackQuery.Message != null)
                {
                    u = db.User.Single(x => x.Id == update.CallbackQuery.From.Id);
                    var callbackData = update.CallbackQuery.Data ?? "";
                    logger.LogInformation("Callback from " + update.CallbackQuery.From.FirstName + " " + update.CallbackQuery.From.LastName + ": " + callbackData);
                    await HandleCallbackQuery(u, db, rpc, callbackData, update.CallbackQuery.Message.MessageId);
                    await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                    return;
                }

                if (update.Type == UpdateType.ChannelPost &&
                    update.ChannelPost != null &&
                    update.ChannelPost?.Chat.Id == adminChannel &&
                    update.ChannelPost.Text != null)
                {
                    var text = update.ChannelPost.Text;
                    if (text.StartsWith("/sql"))
                    {
                        await OnSql(db.Database.GetDbConnection(), text.Substring("/sql".Length));
                    }
                    if (text == "/tx")
                    {
                        var lt = db.LastTransaction.SingleOrDefault();
                        if (lt != null)
                        {
                            await messageSender.SendAdminMessage($"tx {lt.Digest} at {lt.Timestamp}");                            
                        }
                    }
     //               if (text.StartsWith("/setblock"))
					//{
     //                   var height = ulong.Parse(text.Substring("/setblock".Length).Trim());
     //                   var lb = db.LastBlock.SingleOrDefault();
     //                   if (lb != null)
     //                       db.LastBlock.Remove(lb);
     //                   lb = new LastBlock { Height = height - 1, ProcessedDate = DateTime.Now };
     //                   db.LastBlock.Add(lb);
     //                   db.SaveChanges();
     //                   lastBlockChanged = true;
     //                   await messageSender.SendAdminMessage($"Last processed block changed to {height - 1}");
     //               }
                    if (text == "/stat")
                    {
                        string result = "";
                        result += $"Total users: {db.User.Count()}\n";
                        result += $"Active users: {db.User.Count(o => !o.Inactive)}\n";
                        result += $"Monitored addresses: {db.UserAddress.Count(o => !o.User.Inactive)}\n";
                        await messageSender.SendAdminMessage(result);
                    }
                }

                if (update.Message?.Text == "/chatid")
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"id: {update.Message.Chat.Id}");

                if (update.Type != UpdateType.Message)
                    return;

                if (update.Message!.Type != MessageType.Text)
                    return;

                if (update.Message.Chat.Id == supportGroup &&
                    update.Message.ReplyToMessage != null &&
                    update.Message.ReplyToMessage.Entities?.Length > 0 &&
                    update.Message.ReplyToMessage.Entities[0].User != null &&
                    update.Message.Text != null)
				{
                    await messageSender.SendMessage(update.Message.ReplyToMessage.Entities[0].User?.Id ?? 0, "📩 Message from support:\n\n" + update.Message.Text, ReplyKeyboards.MainMenu);
                    await botClient.SendTextMessageAsync(supportGroup, "📤 Message delivered", parseMode: ParseMode.Markdown, disableWebPagePreview: true);
                }
                if (update.Message.Chat.Type != ChatType.Private || update.Message.From == null)
                    return;
                var chatId = update.Message.Chat.Id;
                var messageText = update.Message.Text ?? "";
                logger.LogInformation($"{update.Message.From.FirstName} {update.Message.From.LastName} {update.Message.From.Username}: {messageText}");
                u = db.User.SingleOrDefault(x => x.Id == chatId) ?? new Model.User {
                    CreateDate = DateTime.Now,
                    Firstname = update.Message.Chat.FirstName ?? "",
                    Lastname = update.Message.Chat.LastName ?? "",
                    Title = update.Message.Chat.Title ?? "",
                    Username = update.Message.Chat.Username ?? "",
                    Explorer = 1
                };

                async Task newAddr()
				{
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    string addr = Regex.Matches(messageText, SuiConstants.AddressPattern).First().Value;
                    var name = messageText.Replace(addr, "").Replace("/start", "").Replace("_", "").Trim();

                    if (name == String.Empty)
                    {
                        var v = validators[addr];
                        if (v != null)
                            name = v.Title;
                    }
                    if (name == String.Empty)
                    {
                        var p = db.PublicAddress.FirstOrDefault(o => o.Address.ToLower() == addr.ToLower());
                        if (p != null)
                            name = p.Title;
                        else
                            name = addr.ShortAddr();
                    }
                    var ua = db.UserAddress.SingleOrDefault(x => x.UserId == u.Id && x.Address == addr);
                    if (ua == null)
                    {
                        ua = new UserAddress { Address = addr, User = u, UserId = u.Id, NotifyDelegations = true, NotifyTransactions = true };
                        db.UserAddress.Add(ua);
                    }
                    ua.Title = name;
                    ua.IsDelegate = validators[addr] != null;
                    db.SaveChanges();
                    string text = "✅ New address added!\n" + getAddressText(ua, rpc, true, true) + "\n\nYou will receive notifications on any events.";
                    if (!u.HideHashTags)
                        text += "\n\n#added" + ua.HashTag();
                    await messageSender.SendMessage(chatId, text, ReplyKeyboards.ViewAddressMenu(ua));

                    await messageSender.SendAdminMessage($"➕ user {update.Message.From.Link()} added {ua.Address}");
                }

                bool start = false;
                if (u.Id == 0)
                {
                    u.Id = chatId;
                    db.User.Add(u);
                    db.SaveChanges();
                    await messageSender.SendAdminMessage("✨ New user " + update.Message.From.Link());
                    start = true;
                }
                if (messageText.StartsWith("/sql"))
                {
                    await OnSql(db.Database.GetDbConnection(), messageText.Substring("/sql".Length));
                    return;
                }
                else if (messageText == "/tx")
                {
                    var lt = db.LastTransaction.SingleOrDefault();
                    if (lt != null)
                    {
                        await messageSender.SendMessage(chatId, $"tx {lt.Digest} at {lt.Timestamp}", ReplyKeyboards.MainMenu);
                    }
                    return;
                }
                else if (messageText == "/stat")
                {
                    string result = "";
                    result += $"Total users: {db.User.Count()}\n";
                    result += $"Active users: {db.User.Count(o => !o.Inactive)}\n";
                    result += $"Monitored addresses: {db.UserAddress.Count(o => !o.User.Inactive)}\n";
                    await messageSender.SendMessage(chatId, result, ReplyKeyboards.MainMenu);
                    return;
                }

                if (u.UserState == UserState.SetName)
                {
                    if (messageText != ReplyKeyboards.CmdBack)
                    {
                        var ua = await db.UserAddress.SingleOrDefaultAsync(x => x.Id == u.EditUserAddressId);
                        if (ua != null)
                        {
                            ua.Title = messageText;
                            await messageSender.SendMessage(chatId, "Address renamed", ReplyKeyboards.MainMenu);
                            await messageSender.SendMessage(chatId, getAddressText(ua, rpc, false, false), ReplyKeyboards.ViewAddressMenu(ua));
                        }
                    }
                    else
                        await messageSender.SendMessage(chatId, "👋 Ok, next time", ReplyKeyboards.MainMenu);
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                }
                else if (start || messageText.StartsWith("/start"))
                {
                    if (start || !Regex.IsMatch(messageText, SuiConstants.AddressPatternStart))
                    {
                        u.UserState = UserState.Default;
                        await messageSender.SendMessage(chatId, @$"💚 Welcome {(u.Firstname + " " + u.Lastname).Trim()}!

With Sui Notifier Bot you can easily monitor various events in Sui blockchain, like transactions, delegations, governance, etc.


💡 <b>First steps</b>:
 - click the ✳️ <b>New address</b> button and type the Sui address you want to follow. Use the 💧 <b>My Addresses</b> button to manage address list and special settings",
         //- or simply do nothing and you will be notified about 🐋 <b>whale transactions</b>, which you can disable or configure using ⚙️ <b>Settings</b> button",
         ReplyKeyboards.MainMenu);

                        var message = await chartService.GetMessage();

                        var msg = await telegramBotClient.SendPhotoAsync(u.Id, Telegram.Bot.Types.InputFile.FromStream(message.Item1), caption: message.Item2, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                        await telegramBotClient.PinChatMessageAsync(u.Id, msg.MessageId);
                        u.PinnedMessageId = msg.MessageId;
                        logger.LogInformation($"PinnedMessageId: {msg.MessageId}");
                        db.SaveChanges();
                    }
                    if (Regex.IsMatch(messageText, SuiConstants.AddressPatternStart))
                    {
                        messageText = "0x" + messageText.Replace("/start ", "").Trim();
                        await newAddr();
                    }
				}
				else if (messageText == ReplyKeyboards.CmdNewAddress)
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    await messageSender.SendMessage(chatId, @"Send me your Sui address you want to monitor and the title for this address (optional). For example:

<i>" + SuiConstants.AddressSample + " Tom</i>", ReplyKeyboards.MainMenu);
                }
                else if (messageText == ReplyKeyboards.CmdMyAddresses)
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    var addresses = db.UserAddress.Where(x => x.UserId == u.Id).ToList();
                    if (addresses.Count == 0)
                        await messageSender.SendMessage(chatId, @$"You have no addresses", ReplyKeyboards.MainMenu);
                    else
                    {
                        await botClient.SendChatActionAsync(chatId, ChatAction.Typing);
                        foreach (var ua in addresses)
                            await messageSender.SendMessage(chatId, getAddressText(ua, rpc, false, false), ReplyKeyboards.ViewAddressMenu(ua));
                    }
                }
                else if (messageText == ReplyKeyboards.CmdContactUs)
                {
                    u.UserState = UserState.Support;
                    db.SaveChanges();
                    await messageSender.SendMessage(u.Id, "Please, write here your message", ReplyKeyboards.BackMenu);
                }
                else if (messageText == ReplyKeyboards.CmdSettings)
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    await botClient.SendTextMessageAsync(chatId, @$"Settings", parseMode: ParseMode.Html, replyMarkup: ReplyKeyboards.Settings(u), cancellationToken: cancellationToken);
                }
                else if (messageText.StartsWith("/public") && Regex.IsMatch(messageText, SuiConstants.AddressPattern))
				{
                    string addr = Regex.Matches(messageText, SuiConstants.AddressPattern).First().Value;
                    var name = messageText.Replace("/public", "").Replace(addr, "").Trim();
                    var p = db.PublicAddress.FirstOrDefault(o => o.Address.ToLower() == addr.ToLower());
                    if (p == null)
					{
                        p = new PublicAddress { Address = addr };
                        db.Add(p);
					}
                    p.Title = name;

                    db.SaveChanges();
                    await messageSender.SendMessage(chatId, $"Address {addr} saved as {name}", ReplyKeyboards.MainMenu);
                }
                else if (Regex.IsMatch(messageText, SuiConstants.AddressPattern))
                {
                    await newAddr();
                }
                else if (messageText == ReplyKeyboards.CmdBack)
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();
                    await messageSender.SendMessage(chatId, "🙋 Ok, see you later", ReplyKeyboards.MainMenu);
                }
                else if (u.UserState == UserState.Support)
                {
                    u.UserState = UserState.Default;
                    db.SaveChanges();

                    var message = "💌 Message from " + update.Message.From.Link() + ":\n" + messageText
                        .Replace("_", "__")
                        .Replace("`", "'").Replace("*", "**").Replace("[", "(").Replace("]", ")");

                    await botClient.SendTextMessageAsync(supportGroup, message, parseMode: ParseMode.Markdown, disableWebPagePreview: true);

                    await messageSender.SendMessage(chatId, "Message sent. Thanks for contacting 💛", ReplyKeyboards.MainMenu);
                }
                else if (messageText == "/releases_off")
                {
                    u.ReleaseNotify = false;
                    db.SaveChanges();
                    await messageSender.SendMessage(chatId, $"Software release notifications are turned off. You can turn it back on in the <b>{ReplyKeyboards.CmdSettings}</b> menu.", ReplyKeyboards.MainMenu);
                }
                else
                {
                    await messageSender.SendMessage(chatId, "🙈 Command not recognized", ReplyKeyboards.MainMenu);
                }
            }
            catch(Exception ex)
			{
                logger.LogError(ex, ex.Message);
                await messageSender.SendAdminMessage("🛑 " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        Dictionary<string, (decimal balance, string addr)> balances = new Dictionary<string, (decimal balance, string addr)>();
        string getAddressText(UserAddress ua, RpcClient rpc, bool disableHashTags, bool detailed)
        {          
            if (!balances.ContainsKey(ua.Address))
				balances[ua.Address] = rpc.GetSuiBalance(ua.Address).GetAwaiter().GetResult();
            string result = "";
            //if (a?.@delegate == ua.Address)
            //{
            //    result += "👑 ";
            //    ua.IsDelegate = true;
            //}
            var v = validators[ua.Address];
            if (v != null)
                result += $"👑";
            result += $"<a href='{Explorer.FromId(ua.User.Explorer).account(ua.Address)}'>{ua.Title}</a>\n";
            result += @$"<code>{ua.Address}</code>

<b>Spendable Balance</b>: {balances[ua.Address].balance.SuiToString()}";
            if (v != null)
			{
                result += $"\n<b>Staking Balance</b>: {v.Stake.SuiToString()}";
                result += $"\n<b>Validator APY</b>: {(v.APY * 100).ToString("0.00")}%";
            }
            else
			{
                var resp1 = rpc.PostJson(Request.GetStakes(ua.Address), false).GetAwaiter().GetResult();
                List<(decimal, decimal, string)> stakes = new List<(decimal, decimal, string)>();
                foreach (var s in resp1.result)
                {
                    decimal stake = 0, reward = 0;
                    foreach (var st in s.stakes)
                    { 
                        stake += ((string)st.principal).ParseSui();
                        if (st.estimatedReward != null)
                            reward += ((string)st.estimatedReward).ParseSui();
                    }
                    stakes.Add((stake, reward, s.validatorAddress));
                }
                if (stakes.Count > 0)
                {
                    result += $"\n\n<b>Total staked</b>: {stakes.Sum(o => o.Item1).SuiToString()}\n";
                    result += $"<b>Total earned</b>: {stakes.Sum(o => o.Item2).SuiToString()} ({(100 * stakes.Sum(o => o.Item2) / stakes.Sum(o => o.Item1)).ToString("0.0")}%)";
                    if (detailed)
                    {
                        result += $"\n";
                        foreach (var s in stakes)
                        {
                            if (validators[s.Item3] != null)
                            {
                                var sv = validators[s.Item3];
                                result += $"\n🗃 <a href='{Explorer.FromId(ua.User.Explorer).account(sv.Address)}'>{sv.Title}</a>: {s.Item1.SuiToString()} (earned {s.Item2.SuiToString()})";
                            }
                        }
                    }
                }
            }
            //if (a?.balance.lockedBalance != null)
            //    result += $"\n<b>Locked</b>: {a?.balance.lockedBalance.Value.MinaToString()}";
            //         if (a?.@delegate != ua.Address && a?.@delegate != null)
            //         {
            //             var d = me.GetAccount(a.@delegate) ?? new Account { username = a.@delegate };
            //             result += $"\n<b>Delegate:</b> <a href='{Explorer.FromId(ua.User.Explorer).account(a.@delegate)}'>{(d.username != null && d.username != "Unknown" ? d.username : a.@delegate.ShortAddr())}</a>";
            //         }
            //else if (a?.@delegate == ua.Address)
            //{
            //	result += $"\n<b>Staking Balance</b>: {a?.epochTotalStakingBalance.MinaToString()}";
            //             result += $"\n<b>Delegators</b>: {a?.epochDelegators?.Count}";
            //         }
            //         else if (a?.@delegate != ua.Address)
            //             result += "\n<b>Delegate:</b> not delegated";
            result += "\n\nEvents: ";
            string events = "";
            if (!detailed)
            {
                if (ua.NotifyTransactions)
                    events += "✅❎";
                if (ua.IsDelegate && ua.NotifyDelegations)
                    events += "🤝";
            }
			else
			{
                events += "\n✅❎ Transactions: " + (ua.NotifyTransactions ? "🔔 on" : "🔕 off");
                if (ua.IsDelegate)
                    events += "\n🤝 Delegations: " + (ua.NotifyDelegations ? "🔔 on" : "🔕 off");
            }
            if (events == "")
                events = "none";
            result += events;
            if (!disableHashTags && !ua.User.HideHashTags)
                result += "\n\n" + ua.HashTag();
            return result;
        }

        Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            logger.LogError(exception, ErrorMessage);
            return Task.CompletedTask;
        }
    }
}
