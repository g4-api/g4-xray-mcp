using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System.Net.Mime;

namespace Mcp.Xray.Controllers
{
    [ApiController]
    [Route("/api/v4/[controller]")]
    [SwaggerTag(description: "This controller provides a simple health check endpoint to ensure the service is running and responsive.")]
    public class PingController : ControllerBase
    {
        [HttpGet]
        #region *** OpenApi Documentation ***
        [SwaggerOperation(
            summary: "Health Check Endpoint",
            description: "Returns a simple `Pong` message to confirm that the service is running.")]
        [SwaggerResponse(
            statusCode: StatusCodes.Status200OK,
            description: "Service is active and responding.",
            type: typeof(string), contentTypes: MediaTypeNames.Text.Plain)]
        #endregion
        public IActionResult TestConnection() => Ok("Pong");
    }
}
