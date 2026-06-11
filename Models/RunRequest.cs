using System.ComponentModel.DataAnnotations;

namespace WebApi.Models
{
    public class RunRequest
    {
        [Required]
        [MaxLength(4096)]
        public required string Code { get; set; }
    }
}
