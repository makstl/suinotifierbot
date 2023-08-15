using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;

namespace SuiNotifierBot
{
	public class MessageSender
	{
		TelegramBotClient botClient;
		IServiceProvider serviceProvider;
		long adminChannel;
		DateTime lastSend = DateTime.MinValue;
		int countSent = 0;
		object locker = new object();
		ILogger<MessageSender> logger;
		public MessageSender(TelegramBotClient botClient, IServiceProvider serviceProvider, ILogger<MessageSender> logger)
		{
			this.botClient = botClient;
			this.serviceProvider = serviceProvider;
			adminChannel = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value.AdminChannel;
			this.logger = logger;
		}

		public async Task EditMessage(long userId, int messageId, string text, InlineKeyboardMarkup? keyboard)
		{
			logger.LogInformation($"{userId}: -> ({messageId}) " + text);
			var msg = await botClient.EditMessageTextAsync(userId, messageId, text, Telegram.Bot.Types.Enums.ParseMode.Html,
				disableWebPagePreview: true, replyMarkup: keyboard);

			using var scope = serviceProvider.CreateScope();
			var provider = scope.ServiceProvider;
			using var db = provider.GetRequiredService<BotDbContext>();
			db.Add(new UserMessage {
				UserId = userId,
				CreateDate = msg.EditDate ?? msg.Date,
				Text = text,
				TelegramMessageId = msg.MessageId
			});
			await db.SaveChangesAsync();
		}
		public async Task DeleteMessage(long userId, int messageId)
		{
			logger.LogInformation($"{userId}: X ({messageId})");
			await botClient.DeleteMessageAsync(userId, messageId);
		}
		public async Task SendMessage(long userId, string text, IReplyMarkup keyboard)
		{
			using var scope = serviceProvider.CreateScope();
			var provider = scope.ServiceProvider;
			using var db = provider.GetRequiredService<BotDbContext>();

			lock (locker)
			{
				if (DateTime.Now.Subtract(lastSend).TotalSeconds < 1 && countSent > 20)
					db.UserMessageQueue.Add(new UserMessageQueue {
						UserId = userId,
						CreateDate = DateTime.Now,
						Text = text,
						Keyboard = keyboard.AsString()
					});
				else
				{
					logger.LogInformation($"{userId}: -> " + text);
					try
					{
						var msg = botClient.SendTextMessageAsync(userId, text, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, disableWebPagePreview: true, replyMarkup: keyboard).ConfigureAwait(true).GetAwaiter().GetResult();
						db.UserMessage.Add(new UserMessage {
							UserId = userId,
							CreateDate = DateTime.Now,
							Text = text,
							TelegramMessageId = msg.MessageId
						});
						if (DateTime.Now.Subtract(lastSend).TotalSeconds < 1)
							countSent++;
						else
						{
							lastSend = DateTime.Now;
							countSent = 0;
						}
					}
					catch(ApiRequestException are)
					{
						logger.LogError(are, are.Message);
						if (are.Message == "Bad Request: chat not found" ||
							are.Message.StartsWith("Forbidden"))
						{
							var u = db.User.Single(o => o.Id == userId);
							u.Inactive = true;
							db.SaveChanges();
						}
					}
				}
			}
			await db.SaveChangesAsync();
		}

		public async Task<Telegram.Bot.Types.Message> SendAdminMessage(string text, Telegram.Bot.Types.Enums.ParseMode parseMode = Telegram.Bot.Types.Enums.ParseMode.Markdown)
		{
			logger.LogInformation($"adminChannel: -> " + text);
			return await botClient.SendTextMessageAsync(adminChannel, text, parseMode: parseMode, disableWebPagePreview: true);
		}
		public async Task<Telegram.Bot.Types.Message> EditAdminMessage(int messageId, string text, Telegram.Bot.Types.Enums.ParseMode parseMode = Telegram.Bot.Types.Enums.ParseMode.Markdown)
		{
			logger.LogInformation($"adminChannel: -> ({messageId}) " + text);
			return await botClient.EditMessageTextAsync(adminChannel, messageId, text, parseMode, disableWebPagePreview: true);
		}

		public void ProcessQueue()
		{
			using var scope = serviceProvider.CreateScope();
			var provider = scope.ServiceProvider;
			using var db = provider.GetRequiredService<BotDbContext>();
			var msgList = db.UserMessageQueue.Where(o => !o.User.Inactive).OrderBy(q => q.CreateDate).Take(100).ToList();
			foreach (var q in msgList)
			{
				lock(locker)
				{
					if (DateTime.Now.Subtract(lastSend).TotalSeconds < 1 && countSent > 20)
					{
						while (DateTime.Now.Subtract(lastSend).TotalSeconds < 1)
						{
							logger.LogInformation($"wait 300");
							Thread.Sleep(300);
						}
					}
					logger.LogInformation($"{q.UserId}: (q)-> " + q.Text);
					try
					{
						var msg = botClient.SendTextMessageAsync(q.UserId, q.Text, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, disableWebPagePreview: true, replyMarkup: Extensions.ReplyMarkupFromString(q.Keyboard)).ConfigureAwait(true).GetAwaiter().GetResult();
						db.UserMessage.Add(new UserMessage {
							UserId = q.UserId,
							CreateDate = msg.Date,
							Text = q.Text,
							TelegramMessageId = msg.MessageId
						});
						db.UserMessageQueue.Remove(q);
						if (DateTime.Now.Subtract(lastSend).TotalSeconds < 1)
							countSent++;
						else
						{
							lastSend = DateTime.Now;
							countSent = 0;
						}
						db.SaveChanges();
					}
					catch (ApiRequestException are)
					{
						logger.LogError(are, are.Message);
						if (are.Message == "Bad Request: chat not found" ||
							are.Message.StartsWith("Forbidden"))
						{
							var u = db.User.Single(o => o.Id == q.UserId);
							u.Inactive = true;
							db.SaveChanges();
						}
					}
			}
			}
		}
	}
}
