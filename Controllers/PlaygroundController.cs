using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PlaygroundController(PlaygroundService playground) : ControllerBase
    {
        private readonly PlaygroundService playground = playground;

        [HttpPost("run")]
        public async Task<ActionResult<RunResult>> Run(RunRequest request, CancellationToken cancellationToken)
        {
            return await playground.RunAsync(request.Code, cancellationToken);
        }
    }
}
