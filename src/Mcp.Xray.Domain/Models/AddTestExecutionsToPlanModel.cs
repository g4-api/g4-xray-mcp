namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Identifies an Xray Test Plan and the existing Test Executions associated with it.
    /// </summary>
    /// <remarks>
    /// Tool callers use Jira issue keys, while the repository resolves every key to the numeric identity required
    /// by Xray GraphQL before creating any association.
    /// </remarks>
    public class AddTestExecutionsToPlanModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Jira issue keys of the Test Executions added to the Test Plan.
        /// </summary>
        public string[] TestExecutionKeys { get; set; } = [];

        /// <summary>
        /// Gets or sets the Jira issue key of the Test Plan receiving the Test Executions.
        /// </summary>
        public string TestPlanKey { get; set; } = string.Empty;
        #endregion
    }
}
