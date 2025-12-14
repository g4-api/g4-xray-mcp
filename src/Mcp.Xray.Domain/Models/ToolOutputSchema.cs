using Swashbuckle.AspNetCore.Annotations;

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Generic response model for a "list tools" JSON-RPC method.
    /// Encapsulates the request ID, protocol version, and the result payload
    /// containing available tools.
    /// </summary>
    [SwaggerSchema(description: "Generic JSON-RPC response wrapper for listing available tools.")]
    public class ToolOutputSchema
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the error information if the request failed.
        /// This property is optional and will be <c>null</c> if the request succeeded.
        /// </summary>
        [SwaggerSchema(description: "Error details if the request failed; null if the request succeeded.")]
        public ToolsErrorModel Error { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the JSON-RPC request.
        /// Used to correlate the response with the original request.
        /// </summary>
        [SwaggerSchema(description: "The request identifier, echoed back to correlate with the original JSON-RPC request.")]
        public object Id { get; set; }

        /// <summary>
        /// Gets or sets the JSON-RPC protocol version.
        /// Defaults to "2.0" in compliance with the specification.
        /// </summary>
        [SwaggerSchema(description: "The JSON-RPC protocol version (defaults to '2.0').")]
        public string Jsonrpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the result wrapper containing the list of available tools.
        /// </summary>
        [SwaggerSchema(description: "The result object containing the list of available tools.")]
        public object Result { get; set; } = new();
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents the result object returned by a tool invocation.
        /// Contains raw and structured output content.
        /// </summary>
        [SwaggerSchema(description: "Result object returned by a tool invocation, containing raw and structured content.")]
        public class ToolOutputResultSchema
        {
            /// <summary>
            /// Gets or sets the raw content output by the tool.
            /// This may be a string, number, or other arbitrary object.
            /// </summary>
            [SwaggerSchema(description: "Raw content returned by the tool (may be text, number, or any object).")]
            public object Content { get; set; }

            /// <summary>
            /// Gets or sets the structured content output by the tool.
            /// This provides a machine-readable representation of the tool's result.
            /// </summary>
            [SwaggerSchema(description: "Structured machine-readable content returned by the tool.")]
            [JsonPropertyName(name: "structuredContent")]
            [Newtonsoft.Json.JsonProperty(propertyName: "structuredContent")]
            public object StructuredContent { get; set; }
        }

        /// <summary>
        /// Wraps the payload returned by the "list tools" method.
        /// Contains the collection of <see cref="McpToolModel"/> instances.
        /// </summary>
        [SwaggerSchema(description: "Result payload wrapping the list of available tools.")]
        public class ToolsResultSchema
        {
            /// <summary>
            /// Gets or sets the list of tools available in the registry.
            /// </summary>
            [SwaggerSchema(description: "Collection of available tools in the registry.")]
            public IEnumerable<McpToolModel> Tools { get; set; } = [];
        }
        #endregion
    }
}
