namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Represents an error returned from an MCP tool execution.
    /// This model captures the error code and the descriptive message
    /// associated with a failure that occurred during tool invocation.
    /// </summary>
    public class ToolsErrorModel
    {
        /// <summary>
        /// Gets or sets the numeric error code returned by the tool.
        /// This value typically follows MCP or tool-specific error conventions,
        /// allowing callers to distinguish between different failure types.
        /// </summary>
        public long Code { get; set; }

        /// <summary>
        /// Gets or sets the descriptive error message associated with the failure.
        /// Contains human-readable details about what went wrong.
        /// Defaults to an empty string to avoid null handling by consumers.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
