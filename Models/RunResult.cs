namespace WebApi.Models
{
    public class RunResult
    {
        public required string Stdout { get; set; }

        public required string Stderr { get; set; }

        public int ExitCode { get; set; }

        public bool TimedOut { get; set; }

        public long DurationMs { get; set; }
    }
}
