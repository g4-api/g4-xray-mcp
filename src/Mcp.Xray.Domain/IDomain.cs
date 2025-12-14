using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Repositories;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Text.Json;

namespace Mcp.Xray.Domain
{
    /// <summary>
    /// Exposes core application dependencies required by the domain layer.
    /// This interface acts as a lightweight service locator for shared
    /// infrastructure components used across domain services.
    /// </summary>
    public interface IDomain
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets the Copilot repository used for interacting with MCP/Copilot services.
        /// </summary>
        ICopilotRepository Copilot { get; }

        /// <summary>
        /// Gets the current web hosting environment.
        /// Provides access to environment name, content root, and environment-specific behavior.
        /// </summary>
        IWebHostEnvironment Environment { get; }

        /// <summary>
        /// Gets the JSON serialization options used throughout the application.
        /// Ensures consistent serialization and deserialization behavior.
        /// </summary>
        JsonSerializerOptions JsonOptions { get; }

        /// <summary>
        /// Gets the logger instance associated with the domain layer.
        /// Used for diagnostics, tracing, and error reporting.
        /// </summary>
        ILogger Logger { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Registers all application dependencies and infrastructure services
        /// into the dependency injection container.
        /// This method centralizes service wiring to ensure consistent lifetime
        /// management and clean startup configuration.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> used to configure services for the ASP.NET Core application.</param>
        static void SetDependencies(WebApplicationBuilder builder)
        {
            // Registers the ASP adapter as a singleton.
            // This adapter is stateless and reused across the application lifetime.
            builder.Services.AddSingleton<AspAdapter>();

            // Registers the Copilot repository abstraction with its concrete implementation.
            // Singleton lifetime is appropriate as it holds no per-request state.
            builder.Services.AddSingleton<ICopilotRepository, CopilotRepository>();

            // Registers the shared System.Text.Json serializer options used by ASP.NET Core.
            // These options are resolved from IOptions<JsonOptions> and reused application-wide
            // to ensure consistent serialization behavior across controllers and services.
            builder
                .Services
                .AddSingleton(implementationFactory: provider =>
                    provider
                        .GetRequiredService<IOptions<JsonOptions>>()
                        .Value
                        .JsonSerializerOptions);

            // Registers a default logger instance created via ILoggerFactory.
            // Transient lifetime ensures a fresh logger per resolution while
            // still relying on the shared logging infrastructure.
            builder
                .Services
                .AddTransient(implementationFactory: provider =>
                    provider
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Default"));

            // Registers the core domain service.
            // Transient lifetime is used to avoid shared mutable state between executions.
            builder.Services.AddTransient<IDomain, G4Domain>();
        }
        #endregion
    }
}
