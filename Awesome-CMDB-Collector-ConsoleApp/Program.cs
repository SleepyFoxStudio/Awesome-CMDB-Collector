using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Awesome_CMDB_DataAccess;
using Awesome_CMDB_DataAccess.Providers;
using ConsoleDump;
using Figgle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Awesome_CMDB_Collector_ConsoleApp
{

    class MainService : IHostedService
    {
        private readonly IHostApplicationLifetime _appLifetime;


        public MainService(IHostApplicationLifetime appLifetime)
        {
            _appLifetime = appLifetime;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {

            await Console.Out.WriteLineAsync(FiggleFonts.Slant.Render("Awesome CMDB")).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Awesome CMDB Collector {Assembly.GetExecutingAssembly().GetName().Version}").ConfigureAwait(false);

            try
            {
                var dataCenter = new DummyDataCenter();
                var serverGroups = await dataCenter.GetServerGroupsAsync();

                var dataAccess = new DataAccess("https://localhost:5001", "mysuperclient", "myverysecret");
                var accounts = await dataAccess.GetAccountSummary();
                foreach (var account in accounts)
                {
                    account.Dump();
                }
                //await dataAccess.PostAccountServerGroups(serverGroups);


                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            if (Debugger.IsAttached)
            {
                await Console.Out.WriteLineAsync("press any key to exit.");
                Console.ReadKey();
            }
            _appLifetime.StopApplication();
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    class Program
    {
        private const string AppSettings = "appsettings.json";
        private const string HostSettings = "hostsettings.json";

        static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.SetBasePath(Directory.GetCurrentDirectory());
                    configHost.AddJsonFile(HostSettings, optional: true);
                    configHost.AddCommandLine(args);
                })
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.SetBasePath(Directory.GetCurrentDirectory());
                    configApp.AddJsonFile(AppSettings, optional: true);
                    configApp.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true);
                    configApp.AddEnvironmentVariables(prefix: "Awesome_");
                    configApp.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<ConsoleLifetimeOptions>(options =>
                    {
                        options.SuppressStatusMessages = true;
                    });

                    services.AddHttpClient("JsonClient", client =>
                    {
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                    });

                    services.AddSingleton<IHostedService, MainService>();

                });

            await builder.RunConsoleAsync();
        }
    }


}
