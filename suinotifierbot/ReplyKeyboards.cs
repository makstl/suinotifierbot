using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace SuiNotifierBot
{
	internal static class ReplyKeyboards
	{
        public static string CmdNewAddress = "✳️ New Address";
        public static string CmdMyAddresses = "💧 My Addresses";
        public static string CmdSettings = "⚙️ Settings";
        public static string CmdContactUs = "✉️ Contact us";
        public static string CmdBack = "⬅️ Go back";

        public static ReplyKeyboardMarkup MainMenu => GetMarkup(CmdNewAddress, CmdMyAddresses).AddRow(CmdContactUs, CmdSettings);
        public static ReplyKeyboardMarkup BackMenu => GetMarkup(CmdBack);

        static ReplyKeyboardMarkup GetMarkup(params string[] buttons)
        {
            return new ReplyKeyboardMarkup(new[] { buttons.Select(o => new KeyboardButton(o)).ToArray() }) {
                ResizeKeyboard = true
            };
        }

        static ReplyKeyboardMarkup AddRow(this ReplyKeyboardMarkup m, params string[] buttons)
        {
            var l = m.Keyboard.ToList();
            l.Add(buttons.Select(o => new KeyboardButton(o)).ToArray());
            m.Keyboard = l.ToArray();
            return m;
        }

        public static InlineKeyboardMarkup Settings(User user)
        {
            var buttons = new List<InlineKeyboardButton[]>();
            Action<string, string> add = (text, data) => buttons.Add(new[]
            {
                new InlineKeyboardButton(text) { CallbackData = data }
            });
            Action<string, string, string, string> add2 = (text1, data1, text2, data2) => buttons.Add(new[]
            {
                new InlineKeyboardButton(text1) { CallbackData = data1 },
                new InlineKeyboardButton(text2) { CallbackData = data2 }
            });

            //add(resMgr.Get(Res.UserCurrency, user), "change_currency");
            add("🌐 Explorer: " + Explorer.FromId(user.Explorer).name, "set_explorer");
            add($"#️⃣ Hashtags: {(user.HideHashTags ? "Off" : "On")}", "togglehashtags");
            add($"😺 Software releases: {(user.ReleaseNotify ? "On" : "Off")}", "togglereleases");
            add($"📍 Pin price info", "pinprice");

            //var wa = user.WhaleAlertThreshold > 0 ? $"{(user.WhaleAlertThreshold / 1000)}K" : "off";
            //add("🐋 Whale alerts: " + wa, "set_whalealert");
            //add(resMgr.Get(Res.NetworkIssueAlerts, user), "set_nialert");
            //if (user.VotingNotify)
            //    add(resMgr.Get(Res.VotingNotify, user), "hidevotingnotify");
            //else
            //    add(resMgr.Get(Res.VotingNotify, user), "showvotingnotify");

            //if (user.ReleaseNotify)
            //    add(resMgr.Get(Res.ReleaseNotify, user), "tezos_release_off");
            //else
            //    add(resMgr.Get(Res.ReleaseNotify, user), "tezos_release_on");
                        
            return new InlineKeyboardMarkup(buttons.ToArray());
        }
        public static InlineKeyboardMarkup WhaleAlertSettings(User u)
        {
            var buttons = new List<InlineKeyboardButton[]>();
            Action<string, string> add = (text, data) => buttons.Add(new[]
            {
                new InlineKeyboardButton(text) { CallbackData = data }
            });
            Action<string, string, string, string> add2 = (text1, data1, text2, data2) => buttons.Add(new[]
            {
                new InlineKeyboardButton(text1) { CallbackData = data1 },
                new InlineKeyboardButton(text2) { CallbackData = data2 }
            });
            //add((u.WhaleAlertThreshold == 0 ? "☑️" : "") + " Off", "set_wa_0");
            //add2((u.WhaleAlertThreshold == 250000 ? "☑️" : "") + " 250 000 MINA", "set_wa_250",
            //    (u.WhaleAlertThreshold == 500000 ? "☑️" : "") + " 500 000 MINA", "set_wa_500");
            //add2((u.WhaleAlertThreshold == 750000 ? "☑️" : "") + " 750 000 MINA", "set_wa_750",
            //    (u.WhaleAlertThreshold == 1000000 ? "☑️" : "") + " 1 000 000 MINA", "set_wa_1000");
            add("🔙 Go back", "set_wa_");

            return new InlineKeyboardMarkup(buttons.ToArray());
        }

        public static InlineKeyboardMarkup ViewAddressMenu(UserAddress ua)
        {
            var buttons = new List<InlineKeyboardButton[]>();
            Action<string, string> add = (text, data) => buttons.Add(new[]
            {
                new InlineKeyboardButton(text) { CallbackData = data }
            });
            Action<string, string, string, string> add2 = (text1, data1, text2, data2) => buttons.Add(new[]
            {
                new InlineKeyboardButton(text1) { CallbackData = data1 },
                new InlineKeyboardButton(text2) { CallbackData = data2 }
            });
            add2("🛠 Tune", $"tuneaddress {ua.Id}", "🗑 Delete", $"deleteaddress {ua.Id}");

            return new InlineKeyboardMarkup(buttons);
        }

        public static InlineKeyboardMarkup AddressMenu(UserAddress ua)
        {
            var buttons = new List<InlineKeyboardButton[]>();
            Action<string, string> add = (text, data) => buttons.Add(new[]
            {
                new InlineKeyboardButton(text) { CallbackData = data }
            });
            Action<string, string, string, string> add2 = (text1, data1, text2, data2) => buttons.Add(new[]
            {
                new InlineKeyboardButton(text1) { CallbackData = data1 },
                new InlineKeyboardButton(text2) { CallbackData = data2 }
            });
            if (ua.IsDelegate)
                add2($"{(ua.NotifyTransactions ? "☑️" : "🔲")} Transactions", (ua.NotifyTransactions ? "tranoff" : "tranon") + $" {ua.Id}",
                     $"{(ua.NotifyDelegations ? "☑️" : "🔲")} Delegations", (ua.NotifyDelegations ? "dlgoff" : "dlgon") + $" {ua.Id}");
            else
                add($"{(ua.NotifyTransactions ? "☑️" : "🔲")} Transactions", (ua.NotifyTransactions ? "tranoff" : "tranon") + $" {ua.Id}");
            add2("📝 Rename", $"setname {ua.Id}", "🗑 Delete", $"deleteaddress {ua.Id}");
            
            return new InlineKeyboardMarkup(buttons);
        }
        
        public static InlineKeyboardMarkup ExplorerSettings(User u)
        {
            var buttons = new List<InlineKeyboardButton[]>();
            Action<string, string> add = (text, data) => buttons.Add(new[]
            {
                new InlineKeyboardButton(text) { CallbackData = data }
            });
            foreach(var exp in Explorer.All)
                add((u.Explorer == exp.id ? "☑️" : "") + " " + exp.name, "set_explorer_" + exp.id);

            return new InlineKeyboardMarkup(buttons.ToArray());
        }
    }
}
