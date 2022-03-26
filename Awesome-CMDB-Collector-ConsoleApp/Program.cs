using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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

                //var dataAccess = new DataAccess("https://localhost:5001", "mysuperclient", "myverysecret");
                var client = new AwesomeClient("https://localhost:6001", new HttpClient());
                //var accounts = await client.AccountsAllAsync(cancellationToken);
                var accounts = new List<Account>
                {
                    new Account
                    {
                        AccountName = "account-foo",
                        DatacenterType = DatacenterType._1,
                        Id = "000000001",
                        ServerGroups = new List<ServerGroup>
                    {
                        new ServerGroup
                        {
                            GroupId = "eu-west-1",
                            GroupName = "eu-west-1",
                            Region = "eu-west-1",
                            Servers = new List<ServerDetails>
                            {
                                new ServerDetails
                                {
                                    Id = "i-00000001",
                                    Cpu = 4,
                                    Created = DateTimeOffset.Now,
                                    Name = "test box",
                                    Ram = 3
                                }, new ServerDetails
                                {
                                    Id = "i-00000002",
                                    Cpu = 2,
                                    Created = DateTimeOffset.Now,
                                    Name = "test box2",
                                    Ram = 4
                                }, new ServerDetails
                                {
                                    Id = "i-00000003",
                                    Cpu = 2,
                                    Created = DateTimeOffset.Now,
                                    Name = "test box3",
                                    Ram = 8
                                }, new ServerDetails
                                {
                                    Id = "i-00000004",
                                    Cpu = 4,
                                    Created = DateTimeOffset.Now,
                                    Name = "test box4",
                                    Ram = 12,
                                    Tags = new Dictionary<string, string>
                                    {
                                        {"Department", "CEAT"}
                                    }
                                }
                            }
                        }
                    }
                    }
                };

                foreach (var account in accounts)
                {
                    account.Dump();
                    await client.AccountsAsync(account, cancellationToken);
                }


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
