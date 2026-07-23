namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Identifies an Xray Test Execution and the existing Tests associated with it.
    /// </summary>
    /// <remarks>
    /// Tool callers use Jira issue keys. The repository resolves numeric identities, creates Xray associations,
    /// and waits for every resulting Test Run before reporting success.
    /// </remarks>
    public class AddTestsToExecutionModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Jira issue key of the Test Execution receiving the Tests.
        /// </summary>
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Jira issue keys of the Tests added to the Test Execution.
        /// </summary>
        public string[] TestKeys { get; set; } = [];
        #endregion
    }
}
