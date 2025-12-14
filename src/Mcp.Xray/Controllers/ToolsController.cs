using Mcp.Xray.Domain;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Domain.Repositories;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mcp.Xray.Controllers
{
    [ApiController]
    [Route("/api/v4/mcp")]
    [SwaggerTag(description: "")]
    public class ToolsController(IDomain domain) : ControllerBase
    {
        // Dependency injection for domain services
        private readonly IDomain _domain = domain;

        [HttpGet]
        [SwaggerOperation(
            Summary = "Establish SSE stream",
            Description = "Opens a text/event-stream channel for real-time updates and heartbeats (n8n-compatible).")]
        [SwaggerResponse(StatusCodes.Status200OK, description: "SSE stream established.", contentTypes: "text/event-stream")]
        public async Task Get(CancellationToken token)
        {
            // Required SSE headers (n8n is strict about these)
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";
            Response.Headers["X-Accel-Buffering"] = "no";    // disable nginx buffering if present
            Response.Headers.ContentEncoding = "identity";   // prevent gzip on proxies

            // Start the response immediately so clients consider the stream "open"
            await Response.StartAsync(token);

            // Send an initial comment + heartbeat quickly so n8n marks it connected
            await Response.WriteAsync(": connected\n\n", token);
            await Response.Body.FlushAsync(token);

            // Periodic heartbeats (comments are valid SSE frames and cheaper than data events)
            // Keep them reasonably frequent to survive proxies/load balancers.
            var heartbeat = TimeSpan.FromSeconds(15);

            try
            {
                while (!token.IsCancellationRequested && !HttpContext.RequestAborted.IsCancellationRequested)
                {
                    await Task.Delay(heartbeat, token);

                    // Heartbeat
                    await Response.WriteAsync(": heartbeat\n\n", token);
                    await Response.Body.FlushAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected or server is shutting down – swallow gracefully.
            }
            finally
            {
                // Try to complete the response cleanly.
                if (!HttpContext.RequestAborted.IsCancellationRequested)
                {
                    await Response.CompleteAsync();
                }
            }
        }

        [HttpPost]
        #region *** OpenApi Documentation ***
        [SwaggerOperation(
            Summary = "Handle Copilot agent requests",
            Description = "Processes JSON-RPC methods for initializing, listing tools, invoking tools, and handling notifications."
        )]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Initialization result with context (CopilotInitializeResponseModel), list of available tools (CopilotListResponseModel), or result of tool invocation (object)",
            type: typeof(object),
            contentTypes: MediaTypeNames.Application.Json
        )]
        [SwaggerResponse(StatusCodes.Status202Accepted, description: "Initialization notification acknowledged.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
            description: "Invalid or unsupported method.",
            type: typeof(object),
            contentTypes: MediaTypeNames.Application.Json
        )]
        #endregion
        public IActionResult Post(
            [FromBody, Required]
            [SwaggerParameter(description:
                "The Copilot request payload following the JSON-RPC structure. " +
                "It contains the method to invoke (e.g., 'initialize', 'tools/list', 'tools/call'), " +
                "the request identifier, and any required parameters for the method execution."
            )] McpRequestModel copilotRequest)
        {
            // Dispatch based on the JSON-RPC method
            return copilotRequest.Method switch
            {
                "initialize" => NewContentResult(
                    StatusCodes.Status200OK,
                    value: _domain.Copilot.Initialize(copilotRequest.Id),
                    options: ICopilotRepository.JsonOptions),
                "notifications/initialized" => Accepted(),
                "tools/list" => NewContentResult(
                    StatusCodes.Status200OK,
                    value: _domain.Copilot.GetTools(copilotRequest.Id, intent: default, "system-tool")),
                "tools/call" => NewContentResult(
                    StatusCodes.Status200OK,
                    value: _domain.Copilot.InvokeTool(copilotRequest.Params, copilotRequest.Id)),
                _ => NewContentResult(
                    StatusCodes.Status400BadRequest,
                    value: new { error = $"Unknown method '{copilotRequest.Method}'" })
            };
        }

        #region *** Methods ***
        // Creates a new ContentResult with a JSON-formatted response body.
        private static ContentResult NewContentResult(int statusCode, object value)
        {
            return NewContentResult(statusCode, value, ICopilotRepository.JsonOptions);
        }

        // Creates a new ContentResult with a JSON-formatted response body.
        private static ContentResult NewContentResult(int statusCode, object value, JsonSerializerOptions options)
        {
            // Serialize the input object to JSON using the repository's predefined serializer options.
            var content = JsonSerializer.Serialize(value, options);

            // Construct and return a ContentResult with the provided status code,
            // serialized JSON content, and the correct content type.
            return new ContentResult
            {
                StatusCode = statusCode,
                Content = content,
                ContentType = MediaTypeNames.Application.Json
            };
        }
        #endregion
    }
}
