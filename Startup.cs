using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NTH.Service;

namespace NTH
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();

            services.AddHostedService<BotService>();
            services.AddSingleton<DiscordSocketClient>();
            services.AddSingleton<GoogleSheetsService>();
            services.AddSingleton<NotificationService>();
            services.AddSingleton<LibraryService>();
            services.AddSingleton<UserService>();
            services.AddSingleton<TestService>();
            services.AddSingleton<ResultService>();
            services.AddSingleton<CacheService>();
            services.AddSingleton(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
                UseInteractionSnowflakeDate = false,
            });
            services.AddSingleton<GoogleSheetsService>();
        }
    }
}