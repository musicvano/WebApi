using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using WebApi.Models;

namespace WebApi.Services
{
    public class PlaygroundService(IOptions<PlaygroundOptions> options)
    {
        private readonly PlaygroundOptions options = options.Value;

        public async Task<RunResult> RunAsync(string code, CancellationToken cancellationToken)
        {
            var workDir = Path.Combine(Path.GetTempPath(), "rux-playground", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(workDir, "Main.rux"), code, cancellationToken);
                return await RunContainerAsync(workDir, cancellationToken);
            }
            finally
            {
                try
                {
                    Directory.Delete(workDir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private async Task<RunResult> RunContainerAsync(string workDir, CancellationToken cancellationToken)
        {
            var containerName = $"rux-playground-{Guid.NewGuid():N}";
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var arguments = new[]
            {
                "run", "--rm",
                "--name", containerName,
                "--network", "none",
                "--memory", options.MemoryLimit,
                "--cpus", options.Cpus.ToString(CultureInfo.InvariantCulture),
                "-v", $"{workDir}:/playground:ro",
                options.Image
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            foreach (var argument in options.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                startInfo.ArgumentList.Add(argument);
            }
            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the docker process.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            var timedOut = false;
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                await KillContainerAsync(containerName);
                KillProcess(process);
                await process.WaitForExitAsync(CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
                timedOut = true;
            }
            stopwatch.Stop();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (timedOut)
            {
                stderr = $"Execution timed out after {options.TimeoutSeconds} seconds.";
            }
            return new RunResult
            {
                Stdout = stdout,
                Stderr = stderr,
                ExitCode = timedOut ? -1 : process.ExitCode,
                TimedOut = timedOut,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }

        private static void KillProcess(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static async Task KillContainerAsync(string containerName)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("kill");
            startInfo.ArgumentList.Add(containerName);
            using var process = Process.Start(startInfo);
            if (process is not null)
            {
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
