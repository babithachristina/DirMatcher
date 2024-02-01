using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DirWatcher
{
    public class Program
    {
        private static readonly List<string> _files = new List<string>();
        private static readonly object _lock = new object();
        private static string _magicString = "magic";
        private static string _directory = @"C:\Users\bhabi\Desktop\dirwatcher";
        private static int _interval = 10000; // 10 seconds
        private static Timer _timer;

        public static void Main(string[] args)
        {
            
            CreateHostBuilder(args).Build().Run();

            var fileSystemWatcher = new FileSystemWatcher(@"C:\Users\bhabi\Desktop\dirwatcher")

            {
                Filter = "*.txt",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.Attributes,
                EnableRaisingEvents = true
            };

            fileSystemWatcher.Changed += OnActionOccurOnFolderPath;

            fileSystemWatcher.Created += OnActionOccurOnFolderPath;

            fileSystemWatcher.Deleted += OnActionOccurOnFolderPath;

            fileSystemWatcher.Renamed += OnFileRenameOccur;

            Console.WriteLine("Press any key to exit.");

            Console.ReadLine();


        }
         public static void OnActionOccurOnFolderPath(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("=== Some file change occur ==");

            Console.WriteLine(e.ChangeType);

            Console.WriteLine(e.Name);
        }

        public static void OnFileRenameOccur(object sender, RenamedEventArgs e)
        {

            Console.WriteLine("= file name changed ===");

            Console.WriteLine($"Old file name => {e.OldName}");

            Console.WriteLine($"New file name => (e.Name)");
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/", async context =>
                            {
                                await context.Response.WriteAsync("DirWatcher is running!");
                            });

                            endpoints.MapGet("/config", async context =>
                            {
                                await context.Response.WriteAsync($"Directory: {_directory}\nInterval: {_interval}\nMagic String: {_magicString}");
                            });

                            endpoints.MapPost("/config", async context =>
                            {
                                var directory = context.Request.Form["directory"];
                                var interval = context.Request.Form["interval"];
                                var magicString = context.Request.Form["magicString"];

                                if (!string.IsNullOrEmpty(directory))
                                {
                                    _directory = directory;
                                }

                                if (!string.IsNullOrEmpty(interval) && int.TryParse(interval, out var parsedInterval))
                                {
                                    _interval = parsedInterval;
                                }

                                if (!string.IsNullOrEmpty(magicString))
                                {
                                    _magicString = magicString;
                                }

                                await context.Response.WriteAsync("Configuration updated successfully!");
                            });

                            endpoints.MapGet("/results", async context =>
                            {
                                var results = new List<string>();

                                lock (_lock)
                                {
                                    foreach (var file in _files)
                                    {
                                        var count = File.ReadAllText(file).Split(_magicString).Length - 1;
                                        results.Add($"{file}: {count}");
                                    }
                                }

                                await context.Response.WriteAsync(string.Join("\n", results));
                            });
                        });
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<WatcherService>();
                });

        private class WatcherService : IHostedService
        {
            private readonly ILogger<WatcherService> _logger;

            public WatcherService(ILogger<WatcherService> logger)
            {
                _logger = logger;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_interval));
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                _timer?.Change(Timeout.Infinite, 0);
                return Task.CompletedTask;
            }

            private void DoWork(object state)
            {
                _logger.LogInformation("WatcherService running at: {time}", DateTimeOffset.Now);

                var files = Directory.GetFiles(_directory);

                lock (_lock)
                {
                    foreach (var file in files.Except(_files))
                    {
                        _files.Add(file);
                    }

                    foreach (var file in _files.Except(files))
                    {
                        _files.Remove(file);
                    }
                }

                foreach (var file in files)
                {
                    var count = File.ReadAllText(file).Split(_magicString).Length - 1;
                    _logger.LogInformation($"{file}: {count}");
                }
            }
        }
    }
}
