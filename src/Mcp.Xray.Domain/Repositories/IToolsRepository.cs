using G4.Converters;

using Mcp.Xray.Domain.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Xray.Domain.Repositories
{
    /// <summary>
    /// Defines methods for persisting and retrieving MCP JSON-RPC operations,
    /// including tool discovery, initialization, and invocation.
    /// </summary>
    public interface IToolsRepository
    {
        #region *** Properties ***
        /// <summary>
        /// Ges the JSON serialization options used for MCP operations.
        /// </summary>
        public static JsonSerializerOptions JsonOptions
        {
            get
            {
                // Create a fresh options instance.
                // If this is on a hot path, consider caching in a static readonly field to avoid per-call allocations.
                var options = new JsonSerializerOptions
                {
                    // Do not write properties whose value is null.
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                    // Serialize dictionary keys in snake_case (e.g., "error_code").
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,

                    // Ignore case when matching JSON property names to CLR properties.
                    PropertyNameCaseInsensitive = true,

                    // Serialize CLR property names in snake_case (e.g., "request_id").
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

                    // Compact output optimized for transport/log size. Set true for dev readability.
                    WriteIndented = false
                };

                // Serialize System.Type in a stable, readable format.
                options.Converters.Add(new TypeConverter());

                // Normalize exception payloads (type, message, stack trace, etc.).
                options.Converters.Add(new ExceptionConverter());

                // Enforce ISO-8601 DateTime text to avoid locale/round-trip issues.
                options.Converters.Add(new DateTimeIso8601Converter());

                // Provide a readable/portable representation for MethodBase (useful in logs/telemetry).
                options.Converters.Add(new MethodBaseConverter());

                // Return the configured options.
                return options;
            }
        }
        #endregion

        #region *** Methods    ***
        /// <summary>
        /// Retrieves a collection of available tools, optionally filtered by intent and/or type(s).  
        /// If neither filter is applied, all tools are returned.
        /// </summary>
        /// <param name="id">The JSON-RPC request identifier to correlate response.</param>
        /// <param name="intent">Optional intent filter. If provided, only tools relevant to the specified intent are returned. If <c>null</c> or empty, no intent-based filtering is applied.</param>
        /// <param name="types">Optional tool type filters. If provided, only tools matching one of the given types are returned. If none are provided, no type-based filtering is applied.</param>
        /// <returns>A dictionary mapping tool names (<see cref="string"/>) to their corresponding <see cref="McpToolModel"/> definitions. If both filters are omitted, all available tools are returned.</returns>
        ToolOutputSchema GetTools(object id, string intent, params string[] types);

        /// <summary>
        /// Handles the "initialize" JSON-RPC method, returning protocol capabilities
        /// and server information.
        /// </summary>
        /// <param name="id">The JSON-RPC request identifier to correlate response.</param>
        /// <returns>A <see cref="McpInitializeResponseModel"/> containing protocol version,supported features, and server details.</returns>
        McpInitializeResponseModel Initialize(object id);

        /// <summary>
        /// Invokes the specified tool with the provided parameters and returns a JSON-RPC response containing the tool's result.
        /// This method handles both system tools (built-in) and plugin-based tools (via action rules).
        /// </summary>
        /// <param name="parameters">The JSON parameters for invoking the tool, including tool name and arguments.</param>
        /// <param name="id">The request ID to correlate the response with the request.</param>
        /// <returns>An object containing the result of the tool execution.</returns>
        ToolOutputSchema InvokeTool(JsonElement parameters, object id);
        #endregion
    }
}
