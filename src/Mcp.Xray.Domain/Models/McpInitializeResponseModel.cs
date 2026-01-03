namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Represents the response returned by a Copilot/MCP server during the initialization phase.
    /// This response follows the JSON-RPC 2.0 format and contains information about the server,
    /// available capabilities, and supported protocol versions.
    /// </summary>
    public class McpInitializeResponseModel
    {
        /// <summary>
        /// Gets or sets the request identifier associated with this response.
        /// Returned exactly as provided in the initialization request.
        /// </summary>
        public object Id { get; set; }

        /// <summary>
        /// Gets or sets the JSON-RPC version used by the response.
        /// Expected to always be "2.0".
        /// </summary>
        public string Jsonrpc { get; set; }

        /// <summary>
        /// Gets or sets the result object containing server metadata and capabilities.
        /// </summary>
        public ResultModel Result { get; set; }

        /// <summary>
        /// Encapsulates the contents of the JSON-RPC "result" field from the initialization response.
        /// Includes protocol version, server capabilities, and server identification.
        /// </summary>
        public class ResultModel
        {
            /// <summary>
            /// Gets or sets the capabilities advertised by the server, including tool features.
            /// </summary>
            public Capabilities Capabilities { get; set; }

            /// <summary>
            /// Gets or sets the version of the MCP protocol supported by the server.
            /// </summary>
            public string ProtocolVersion { get; set; }

            /// <summary>
            /// Gets or sets general server information such as name and version.
            /// </summary>
            public ServerInformation ServerInfo { get; set; }
        }

        /// <summary>
        /// Describes the high-level capability groups supported by the MCP server.
        /// </summary>
        public class Capabilities
        {
            /// <summary>
            /// Gets or sets the server's tool-related capabilities.
            /// </summary>
            public ToolsCapabilities Tools { get; set; }
        }

        /// <summary>
        /// Represents capabilities specifically related to MCP tools exposed by the server.
        /// </summary>
        public class ToolsCapabilities
        {
            /// <summary>
            /// Gets or sets a value indicating whether the server can notify the client
            /// when its list of available tools changes dynamically.
            /// </summary>
            public bool ListChanged { get; set; }
        }

        /// <summary>
        /// Provides identifying details about the MCP server instance, such as
        /// its product name and version string.
        /// </summary>
        public class ServerInformation
        {
            /// <summary>
            /// Gets or sets the name of the server (e.g., "g4-mcp-server").
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the version of the server software.
            /// </summary>
            public string Version { get; set; }
        }
    }
}
