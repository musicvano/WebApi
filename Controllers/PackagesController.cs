using Microsoft.AspNetCore.Mvc;
using Npgsql;
using WebApi.Models;
using WebApi.Repositories;
using WebApi.Services;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PackagesController(
        PackageRepository repository,
        RepositoryService repositoryService,
        TurnstileService turnstileService) : ControllerBase
    {
        private readonly PackageRepository repository = repository;
        private readonly RepositoryService repositoryService = repositoryService;
        private readonly TurnstileService turnstileService = turnstileService;

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 50;

        [HttpGet]
        public async Task<ActionResult<PagedResult<Package>>> GetAll(
            int page = 1, int pageSize = DefaultPageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            var total = await repository.CountAsync();
            var items = await repository.GetPageAsync(pageSize, (page - 1) * pageSize);
            return new PagedResult<Package>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Package>> GetById(Guid id)
        {
            var package = await repository.GetByIdAsync(id);
            if (package is null)
            {
                return NotFound();
            }
            return package;
        }

        [HttpPost]
        public async Task<ActionResult<Package>> Create(
            PackageRequest request, CancellationToken cancellationToken)
        {
            if (!await VerifyTurnstileAsync(request.TurnstileToken, cancellationToken))
            {
                return BadRequest("Human verification failed. Refresh the page and try again.");
            }

            PackageMetadata metadata;
            try
            {
                metadata = await repositoryService.GetMetadataAsync(
                    request.Repository, cancellationToken);
            }
            catch (RepositoryException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (HttpRequestException)
            {
                return BadRequest("Failed to reach GitHub. Try again later.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return BadRequest("GitHub request timed out. Try again later.");
            }

            var package = new Package
            {
                Id = Guid.CreateVersion7(),
                Name = metadata.Name,
                Description = metadata.Description,
                Repository = metadata.Repository,
                License = metadata.License,
                Created = DateTime.UtcNow
            };
            try
            {
                await repository.CreateAsync(package);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Conflict("A package with the same name or repository already exists.");
            }
            return CreatedAtAction(nameof(GetById), new { id = package.Id }, package);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id, PackageRequest request, CancellationToken cancellationToken)
        {
            if (!await VerifyTurnstileAsync(request.TurnstileToken, cancellationToken))
            {
                return BadRequest("Human verification failed. Refresh the page and try again.");
            }

            PackageMetadata metadata;
            try
            {
                metadata = await repositoryService.GetMetadataAsync(
                    request.Repository, cancellationToken);
            }
            catch (RepositoryException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (HttpRequestException)
            {
                return BadRequest("Failed to reach GitHub. Try again later.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return BadRequest("GitHub request timed out. Try again later.");
            }

            var package = new Package
            {
                Id = id,
                Name = metadata.Name,
                Description = metadata.Description,
                Repository = metadata.Repository,
                License = metadata.License
            };
            bool updated;
            try
            {
                updated = await repository.UpdateAsync(package);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Conflict("A package with the same name or repository already exists.");
            }

            if (!updated)
            {
                return NotFound();
            }
            return NoContent();
        }

        private async Task<bool> VerifyTurnstileAsync(
            string token, CancellationToken cancellationToken)
        {
            try
            {
                return await turnstileService.VerifyAsync(
                    token,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!await repository.DeleteAsync(id))
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}
