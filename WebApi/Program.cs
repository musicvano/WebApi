using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using WebApi.Repositories;
using WebApi.Services;

namespace WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
                });
            });
            var connectionString = builder.Configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
            builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
            builder.Services.AddSingleton<PackageRepository>();
            builder.Services.AddSingleton<WorkflowRepository>();
            builder.Services.Configure<GitHubWebhookOptions>(
                builder.Configuration.GetSection("GitHubWebhook"));
            builder.Services.AddSingleton<GitHubWebhookService>();
            builder.Services.AddHttpClient<RepositoryService>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RuxPackageRegistry/1.0");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            builder.Services.Configure<TurnstileOptions>(builder.Configuration.GetSection("Turnstile"));
            builder.Services.AddHttpClient<TurnstileService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            builder.Services.Configure<PlaygroundOptions>(builder.Configuration.GetSection("Playground"));
            builder.Services.AddSingleton<PlaygroundService>();
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // The app sits behind a single trusted nginx reverse proxy on localhost.
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
            var app = builder.Build();
            app.UseForwardedHeaders();
            app.UseHttpsRedirection();
            app.UseCors();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
