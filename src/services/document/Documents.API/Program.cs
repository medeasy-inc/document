using Documents.DataStore.SqlServer;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Serilog;
using System;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Documents.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
           IWebHost host = CreateWebHostBuilder(args).Build();

            using IServiceScope scope = host.Services.CreateScope();
            IServiceProvider services = scope.ServiceProvider;
            ILogger<Program> logger = services.GetRequiredService<ILogger<Program>>();
            DocumentsStore context = services.GetRequiredService<DocumentsStore>();
            IHostingEnvironment hostingEnvironment = services.GetRequiredService<IHostingEnvironment>();
            logger?.LogInformation("Starting {ApplicationContext}", hostingEnvironment.ApplicationName);

            try
            {
                if (!context.Database.IsInMemory())
                {
                    logger?.LogInformation("Upgrading {ApplicationContext}'s store", hostingEnvironment.ApplicationName);
                    // Forces database migrations on startup
                    RetryPolicy policy = Policy
                        .Handle<SqlException>(sql => sql.Message.Like("*Login failed*", ignoreCase: true))
                        .WaitAndRetryAsync(
                            retryCount: 5,
                            sleepDurationProvider: (retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))),
                            onRetry: (exception, timeSpan, attempt, pollyContext) =>
                                logger?.LogError(exception, $"Error while upgrading database (Attempt {attempt})")
                            );
                    logger?.LogInformation("Starting {ApplicationContext} database migration", hostingEnvironment.ApplicationName);

                    // Forces datastore migration on startup
                    await policy.ExecuteAsync(async () => await context.Database.MigrateAsync().ConfigureAwait(false))
                        .ConfigureAwait(false);

                    logger?.LogInformation("{ApplicationContext} store updated", hostingEnvironment.ApplicationName);
                }
                await host.RunAsync()
                    .ConfigureAwait(false);

                logger?.LogInformation($"Identity.API started");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "An error occurred on startup.");
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) 
            => WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseSerilog((hosting, loggerConfig) => loggerConfig
                    .MinimumLevel.Verbose()
                    .Enrich.WithProperty("ApplicationContext", hosting.HostingEnvironment.ApplicationName)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .ReadFrom.Configuration(hosting.Configuration)
                )
                .ConfigureLogging((options) => {
                    options.ClearProviders() // removes all default providers
                        .AddSerilog()
                        .AddConsole();
                })
                .ConfigureAppConfiguration((context, builder) =>

                    builder
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .AddCommandLine(args)
                );
    }
}