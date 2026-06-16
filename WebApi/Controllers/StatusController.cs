using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StatusController(NpgsqlDataSource dataSource, IHostEnvironment environment)
        : ControllerBase
    {
        private static readonly DateTime StartedUtc = DateTime.UtcNow;

        private static readonly string? Version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0];

        private readonly NpgsqlDataSource dataSource = dataSource;
        private readonly IHostEnvironment environment = environment;

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var (databaseOk, latencyMs, databaseError) = await CheckDatabaseAsync(cancellationToken);

            var body = new
            {
                name = "Rux WebAPI",
                status = databaseOk ? "Healthy" : "Unhealthy",
                time = now,
                uptime = (now - StartedUtc).ToString(@"d\.hh\:mm\:ss"),
                version = Version,
                environment = environment.EnvironmentName,
                database = new
                {
                    connected = databaseOk,
                    latencyMs,
                    error = databaseError
                }
            };

            return StatusCode(
                databaseOk ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
                body);
        }

        private async Task<(bool Ok, long? LatencyMs, string? Error)> CheckDatabaseAsync(
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await using var command = dataSource.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync(cancellationToken);
                stopwatch.Stop();
                return (true, stopwatch.ElapsedMilliseconds, null);
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                stopwatch.Stop();
                return (false, null, ex.Message);
            }
        }
    }
}
