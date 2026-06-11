namespace WebApi.Models
{
    public class Package
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string Description { get; set; }

        public required string Repository { get; set; }

        public required string License { get; set; }

        public DateTime Created { get; set; }
    }
}
