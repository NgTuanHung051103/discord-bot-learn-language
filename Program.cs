using Microsoft.Extensions.Hosting;

namespace NTH
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var startup = new Startup(context.Configuration);
                startup.ConfigureServices(services);
            })
            .Build();

            await host.RunAsync();
        }
    }
}