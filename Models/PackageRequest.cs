using System.ComponentModel.DataAnnotations;

namespace WebApi.Models
{
    public class PackageRequest
    {
        [Required]
        [Url]
        public required string Repository { get; set; }

        [Required]
        public required string TurnstileToken { get; set; }
    }
}
