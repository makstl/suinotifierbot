using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SuiNotifierBot
{
	public class ReleasesWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReleasesWorker> _logger;
        private readonly MessageSender _messageSender;

        public ReleasesWorker(
            IServiceProvider serviceProvider,
            ILogger<ReleasesWorker> logger,
            MessageSender messageSender
        )
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _messageSender = messageSender;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (stoppingToken.IsCancellationRequested is false)
            {
                using var scope = _serviceProvider.CreateScope();
                await using var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

                
                try
                {
                    using var client = scope.ServiceProvider.GetRequiredService<ReleasesClient>();
                    var releases = await client.GetAll();
                    if (releases != null)
                        foreach (var release in releases)
                        {
                            if (release.draft || release.prerelease)
                                continue;
                            var exists = await db.Release.AnyAsync(x => x.Id == release.id);
                            if (!exists)
                            {
                                var r = new Release {
                                    Id = release.id,
                                    Name = release.name,
                                    ReleasedAt = release.published_at,
                                    Url = release.html_url,
                                    Tag = release.tag_name,
                                    Description = release.body.Substring(0, release.body.IndexOfAny(new char[] { '\r', '\n', '\\' }))
                                };
                                await db.Release.AddAsync(r);

                                await BroadcastRelease(db, r);
                                _logger.LogInformation("New release: " + release.name);
                            }
                            else
                                _logger.LogInformation("Last release: " + release.name);
                            break;
                        }

                    await db.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to fetch releases");
                }

                await Task.Delay(1000 * 60 * 15);
                
                async Task BroadcastRelease(BotDbContext db, Release r)
                {
                    var subscribers = await db.Set<User>().AsNoTracking()
                        .Where(x => x.ReleaseNotify)
                        .ToArrayAsync();

                    var text = @$"😺 Sui software update <a href='{r.Url}'>{r.Tag}</a> released.

Check out the <a href='{r.Url}'>announcement</a>.

Turn off these notifications: /releases_off";
                    foreach (var s in subscribers)
                        await _messageSender.SendMessage(s.Id, text + (!s.HideHashTags ? "\r\n\r\n#release" : ""), ReplyKeyboards.MainMenu);
                }
            }
        }
    }

    public class ReleasesClient : IDisposable
    {
        private readonly HttpClient _client;

        public ReleasesClient()//HttpClient client)
        {
            _client = new HttpClient();// client;
            _client.BaseAddress = new Uri("https://api.github.com");
            _client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue(new System.Net.Http.Headers.ProductHeaderValue("request")));
        }

		public void Dispose()
		{
			_client.Dispose();
		}

		public async Task<GithubRelease[]?> GetAll()
        {
            var responseStream = await _client.GetStreamAsync($"/repos/MystenLabs/sui/releases");

            return await JsonSerializer.DeserializeAsync<GithubRelease[]>(responseStream);
        }
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public class GithubRelease
    {
		public string url { get; set; }
		public string assets_url { get; set; }
        public string upload_url { get; set; }
        public string html_url { get; set; }
        public int id { get; set; }
        public string node_id { get; set; }
        public string tag_name { get; set; }
        public string target_commitish { get; set; }
        public string name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public DateTime created_at { get; set; }
        public DateTime published_at { get; set; }
        public string tarball_url { get; set; }
        public string zipball_url { get; set; }
        public string body { get; set; }
        public string discussion_url { get; set; }
        public int? mentions_count { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
