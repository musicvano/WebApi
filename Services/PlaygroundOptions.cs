namespace WebApi.Services
{
    public class PlaygroundOptions
    {
        public string Image { get; set; } = "rux";

        public string Command { get; set; } = "run /playground/Main.rux";

        public int TimeoutSeconds { get; set; } = 10;

        public string MemoryLimit { get; set; } = "256m";

        public double Cpus { get; set; } = 1.0;
    }
}
