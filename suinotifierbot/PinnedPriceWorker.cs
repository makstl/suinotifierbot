using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SuiNotifierBot
{
	internal class PinnedPriceWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PinnedPriceWorker> _logger;
        private readonly TelegramBotClient _telegramBotClient;
        private readonly SuiChartService _chartService;

        public PinnedPriceWorker(
            IServiceProvider serviceProvider,
            ILogger<PinnedPriceWorker> logger,
            TelegramBotClient telegramBotClient,
            SuiChartService chartService
        )
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _telegramBotClient = telegramBotClient;
            _chartService = chartService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(1000 * 60 * 5, stoppingToken);
            while (stoppingToken.IsCancellationRequested is false)
            {
                using var scope = _serviceProvider.CreateScope();
                await using var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

                try
                {
                    var users = db.User.Where(o => o.PinnedMessageId > 0 && !o.Inactive).ToList();
                    var md = _serviceProvider.GetRequiredService<CryptoCompare.CryptoCompareClient>().GetMarketData();

                    var msg = await _chartService.GetMessage();
                    var media = new InputMediaPhoto(InputFile.FromStream(msg.Item1, "SUIUSD.png"));
                    media.Caption = msg.Item2;
                    media.ParseMode = Telegram.Bot.Types.Enums.ParseMode.Html;

                    foreach(var u in users)
					{
                        try
                        {
                            msg.Item1.Seek(0, SeekOrigin.Begin);
                            _logger.LogInformation($"Update price for {u.Firstname} {u.Lastname} @{u.Username}: " + msg.Item2);
                            await _telegramBotClient.EditMessageMediaAsync(u.Id, u.PinnedMessageId, media, cancellationToken: stoppingToken);
                        }
                        catch (Telegram.Bot.Exceptions.ApiRequestException e)
                        {
                            _logger.LogError(e, e.Message);
                            if (e.Message.Contains("chat not found") ||
                                e.Message.Contains("bot was blocked by the user"))
                            {
                                u.PinnedMessageId = 0;
                                u.Inactive = true;
                                await db.SaveChangesAsync();
                            }
                            if (e.Message.Contains("message to edit not found"))
							{
                                u.PinnedMessageId = 0;
                                await db.SaveChangesAsync();
                            }
                        }
                        await Task.Delay(1000 * 3, stoppingToken);
                        if (stoppingToken.IsCancellationRequested)
                            return;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "PinnedPriceWorker failure");
                }            
            }
        }
    }
}
