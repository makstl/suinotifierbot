using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model;
using System.Globalization;
using Telegram.Bot;

CreateHostBuilder(args).Build().Run();

IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, builder) => {
                builder.AddJsonFile("Settings.json");
                builder.AddJsonFile($"Settings.{context.HostingEnvironment.EnvironmentName}.json", true);
                builder.AddJsonFile("Settings.Local.json", true);
                builder.AddEnvironmentVariables();
                builder.AddCommandLine(args);
            })
            .ConfigureServices((context, services) => {
                services.Configure<TelegramOptions>(context.Configuration.GetSection("Telegram"));
                {
                    var db = new Model.BotDbContext();
                    //db.Database.EnsureCreated();
                    db.Database.Migrate();

                    string namesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sui-names.txt");
                    if (File.Exists(namesFile))
                    {
                        var allNames = System.IO.File.ReadAllLines(namesFile);
                        Dictionary<string, PublicAddress> addrList = db.PublicAddress.ToDictionary(o => o.Address);
                        foreach (var addrName in allNames)
                        {
                            string addr = addrName.Substring(0, 55);
                            string title = addrName.Substring(56);
                            if (addrList.ContainsKey(addr))
                                addrList[addr].Title = title;
                            else
                                addrList[addr] = db.Add(new PublicAddress { Address = addr, Title = title }).Entity;
                        }
                        db.SaveChanges();
                        Console.WriteLine("Total public names: " + addrList.Count.ToString());
                    }
                }
                services.AddDbContext<BotDbContext>();
                services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole().AddFile("Logs/log-{Date}.txt"));
                services.AddSingleton<SuiNotifierBot.Validators>();
                services.AddSingleton<IFormatProvider>(CultureInfo.GetCultureInfo("en"));
                services.AddSingleton(provider => new TelegramBotClient(provider.GetRequiredService<IOptions<TelegramOptions>>().Value.BotSecret));
                services.AddSingleton<SuiNotifierBot.MessageSender>();
                services.AddSingleton<SuiNotifierBot.Rpc.RpcClient>(sp => new SuiNotifierBot.Rpc.RpcClient(context.Configuration.GetValue<string>("NodeUri"), sp.GetRequiredService<ILogger<SuiNotifierBot.Rpc.RpcClient>>()));
                services.AddScoped<SuiNotifierBot.ReleasesClient>();
                services.AddHostedService<SuiNotifierBot.ReleasesWorker>();
                services.AddHostedService<SuiNotifierBot.BotService>();
                services.AddTransient(sp =>
                        new SuiNotifierBot.CryptoCompare.CryptoCompareClient(context.Configuration.GetValue<string>("CryptoCompareToken"), new HttpClient(), sp.GetRequiredService<ILogger<SuiNotifierBot.CryptoCompare.CryptoCompareClient>>()));
                services.AddSingleton<SuiNotifierBot.SuiChartService>();
                services.AddHostedService<SuiNotifierBot.PinnedPriceWorker>();
            });

