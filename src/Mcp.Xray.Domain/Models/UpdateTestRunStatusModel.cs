namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Identifies one Xray Test Run and the final or intermediate status applied to it.
    /// </summary>
    /// <remarks>
    /// A Test Run is selected by its owning Test Execution and Test Jira keys. The repository waits for Xray
    /// registration before resolving the opaque Test Run identifier consumed by the status mutation.
    /// </remarks>
    public class UpdateTestRunStatusModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Jira issue key of the Test Execution that owns the Test Run.
        /// </summary>
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Xray Test Run status name or identifier.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Jira issue key of the Test represented by the Test Run.
        /// </summary>
        public string TestKey { get; set; } = string.Empty;
        #endregion
    }
}
