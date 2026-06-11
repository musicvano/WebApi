namespace WebApi.Models
{
    public record PackageMetadata(
        string Name,
        string Description,
        string Repository,
        string License);
}
