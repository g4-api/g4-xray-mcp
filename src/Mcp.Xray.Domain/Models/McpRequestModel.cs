using System.Text.Json;

namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Represents a generic Model Context Protocol (MCP) request using the JSON-RPC 2.0 format.
    /// This model is used when communicating with MCP servers and tools, encapsulating the request
    /// identifier, method name, and a flexible parameters payload.
    /// </summary>
    public class McpRequestModel
    {
        /// <summary>
        /// Gets or sets the unique identifier of the request.
        /// JSON-RPC does not enforce a specific type, so this may be a number, string, GUID, or null
        /// (for notifications). This ID allows correlating responses with requests.
        /// </summary>
        public object Id { get; set; }

        /// <summary>
        /// Gets or sets the JSON-RPC version.
        /// Always set to "2.0" according to the JSON-RPC specification.
        /// </summary>
        public string JsonRpc { get; set; } = "2.0";

        /// <summary>
        /// Gets or sets the MCP method being invoked.
        /// This corresponds to a command or tool name exposed by the remote MCP server.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the parameters for the invoked MCP method.
        /// Stored as <see cref="JsonElement"/> to support dynamic and schema-less payloads.
        /// </summary>
        public JsonElement Params { get; set; }
    }
}
