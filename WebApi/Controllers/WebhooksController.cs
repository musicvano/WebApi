using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Repositories;
using WebApi.Services;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WebhooksController(
        WorkflowRepository repository,
        GitHubWebhookService webhookService) : ControllerBase
    {
        private const string WorkflowJobEventName = "workflow_job";
        private const string CompletedStatus = "completed";
        private const string MainBranch = "main";

        private readonly WorkflowRepository repository = repository;
        private readonly GitHubWebhookService webhookService = webhookService;

        [HttpPost("github")]
        public async Task<IActionResult> GitHub(CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await Request.Body.CopyToAsync(memory, cancellationToken);
            var payload = memory.ToArray();

            if (!webhookService.VerifySignature(payload, Request.Headers["X-Hub-Signature-256"]))
            {
                return Unauthorized();
            }

            if (Request.Headers["X-GitHub-Event"] != WorkflowJobEventName)
            {
                return Ok();
            }

            WorkflowJobEvent? payloadEvent;
            try
            {
                payloadEvent = JsonSerializer.Deserialize<WorkflowJobEvent>(payload);
            }
            catch (JsonException)
            {
                return BadRequest();
            }

            var job = payloadEvent?.WorkflowJob;
            if (job is null
                || job.Status != CompletedStatus
                || job.HeadBranch != MainBranch
                || string.IsNullOrEmpty(job.WorkflowName)
                || string.IsNullOrEmpty(job.Name))
            {
                return Ok();
            }

            await repository.UpsertJobAsync(
                job.WorkflowName, job.Name, job.Conclusion, job.CompletedAt);
            return Ok();
        }
    }
}
