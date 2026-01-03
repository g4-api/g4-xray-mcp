using Mcp.Xray.Domain.Clients;
using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Domain.Repositories;
using Mcp.Xray.Settings;

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
        IToolsRepository Copilot { get; }

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
        public static void SetDependencies(WebApplicationBuilder builder)
        {
            // Registers the ASP adapter as a singleton.
            // This adapter is stateless and reused across the application lifetime.
            builder.Services.AddSingleton<AspAdapter>();

            // Build the Jira authentication model from application configuration.
            // This object encapsulates all credentials and connection details
            // required to communicate with Jira and downstream Xray services.
            var jiraAuthentication = new JiraAuthenticationModel
            {
                // Base Jira URL or collection endpoint.
                Collection = AppSettings.JiraOptions.BaseUrl,

                // API key or token used for authenticating requests.
                Password = AppSettings.JiraOptions.ApiKey,

                // Username associated with the API credentials.
                Username = AppSettings.JiraOptions.Username
            };

            // Register the authentication model as a singleton so the same
            // immutable configuration instance is reused across the application.
            builder.Services.AddSingleton(provider => jiraAuthentication);

            // Register the Jira client as a transient service.
            // A new instance is created for each resolution to ensure that
            // request-scoped behavior and underlying HTTP usage remain isolated.
            builder.Services.AddTransient((_) => new JiraClient(authentication: jiraAuthentication));

            // Register the Xray repository as a transient service.
            // The concrete implementation is selected dynamically based on
            // whether the configured Jira instance is running in Cloud mode.
            builder.Services.AddTransient<IXrayRepository>((_) =>
            {
                return AppSettings.JiraOptions?.IsCloud == true
                    ? new XrayXpandRepository(jiraAuthentication)
                    : new XrayRavenRepository();
            });

            // Register the tools repository as a transient service.
            // This repository is responsible for resolving, dispatching,
            // and executing system tools during a single request lifecycle.
            builder.Services.AddTransient<IToolsRepository, ToolsRepository>();

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
