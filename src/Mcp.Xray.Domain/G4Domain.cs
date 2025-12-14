using Mcp.Xray.Domain.Repositories;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using System.Text.Json;

namespace Mcp.Xray.Domain
{
    /// <summary>
    /// Represents the core domain implementation of the G4 application.
    /// This class acts as the main orchestration layer, coordinating between
    /// ASP-specific infrastructure (via <see cref="AspAdapter"/>) and
    /// Copilot/MCP data access (via <see cref="ICopilotRepository"/>).
    /// </summary>
    /// <param name="aspAdapter">Adapter that provides access to ASP.NET Core infrastructure concerns (environment, logging, JSON options, etc.) in a domain-friendly manner.</param>
    /// <param name="copilot">Repository responsible for interacting with Copilot/MCP services, including tool discovery and invocation.</param>
    internal class G4Domain(AspAdapter aspAdapter, ICopilotRepository copilot) : IDomain
    {
        /// <inheritdoc />
        public ICopilotRepository Copilot { get; } = copilot;

        /// <inheritdoc />
        public IWebHostEnvironment Environment { get; } = aspAdapter.Environment;

        /// <inheritdoc />
        public JsonSerializerOptions JsonOptions { get; } = aspAdapter.JsonOptions;

        /// <inheritdoc />
        public ILogger Logger { get; } = aspAdapter.Logger;
    }

    /// <summary>
    /// Adapter class responsible for managing essential services related to the ASP.NET Core application,
    /// including JSON serialization options, logging, and the web host environment.
    /// This class centralizes the access to these services for use in different parts of the application.
    /// </summary>
    internal class AspAdapter(
        JsonSerializerOptions jsonOptions,
        ILogger logger,
        IWebHostEnvironment environment)
    {
        /// <summary>
        /// The environment settings for the current ASP.NET Core web host.
        /// Provides information about the hosting environment, such as whether the application
        /// is running in development, staging, or production.
        /// </summary>
        public IWebHostEnvironment Environment { get; set; } = environment;

        /// <summary>
        /// The JSON serializer settings that are used for serializing and deserializing objects.
        /// This is useful for customizing JSON handling (e.g., formatting, converters).
        /// </summary>
        public JsonSerializerOptions JsonOptions { get; set; } = jsonOptions;

        /// <summary>
        /// The logger instance used for logging events in the application.
        /// This is used for capturing and outputting logs for debugging and monitoring.
        /// </summary>
        public ILogger Logger { get; set; } = logger;
    }
}
