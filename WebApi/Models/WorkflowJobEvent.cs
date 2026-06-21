using System.Text.Json.Serialization;

namespace WebApi.Models
{
    public class WorkflowJobEvent
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("workflow_job")]
        public WorkflowJob? WorkflowJob { get; set; }
    }

    public class WorkflowJob
    {
        [JsonPropertyName("head_branch")]
        public string? HeadBranch { get; set; }

        [JsonPropertyName("workflow_name")]
        public string? WorkflowName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("conclusion")]
        public string? Conclusion { get; set; }

        [JsonPropertyName("completed_at")]
        public DateTime? CompletedAt { get; set; }
    }
}
