using Mcp.Xray.Domain.Clients;
using Mcp.Xray.Domain.Repositories;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using System.Text.Json;

namespace Mcp.Xray.Domain
{
    /// <summary>
    /// Represents the core domain implementation of the G4 application.
    /// </summary>
    /// <param name="aspAdapter">Adapter that provides access to ASP.NET Core infrastructure concerns (environment, logging, JSON options, etc.) in a domain-friendly manner.</param>
    /// <param name="atlassian">Adapter that provides access to Atlassian services such as Xray and Jira.</param>
    /// <param name="copilot">Repository responsible for interacting with Copilot/MCP services, including tool discovery and invocation.</param>
    internal class G4Domain(
        AspAdapter aspAdapter,
        IToolsRepository copilot) : IDomain
    {
        /// <inheritdoc />
        public IToolsRepository Copilot { get; } = copilot;

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

    /// <summary>
    /// This adapter provides a stable boundary around Jira and Xray integrations,
    /// allowing orchestration logic to interact with Atlassian services without
    /// being coupled to individual client or repository implementations.
    /// </summary>
    internal class AtlassianAdapter()
    {
        /// <summary>
        /// Gets or sets the Jira client used to communicate with Jira APIs.
        /// </summary>
        /// <remarks>
        /// The client instance is expected to be fully initialized and authenticated
        /// before being injected into this adapter.
        /// </remarks>
        public JiraClient JiraClient { get; set; }

        /// <summary>
        /// Gets or sets the Xray repository responsible for Xray test management operations.
        /// </summary>
        /// <remarks>
        /// This repository encapsulates Xray-specific behavior and provides a consistent
        /// interface for working with tests, executions, and related metadata.
        /// </remarks>
        public IXrayRepository Xray { get; set; }
    }
}
