namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Defines an Xray Test Execution issue together with its initial Tests and execution environments.
    /// </summary>
    /// <remarks>
    /// Jira issue fields are inherited from <see cref="NewIssueModelBase"/>. Test keys are resolved to numeric
    /// Jira issue identifiers before the Xray mutation begins so a failed lookup cannot create a partial execution.
    /// </remarks>
    public class NewExecutionModel : NewIssueModelBase
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the environment names associated with the new Test Execution.
        /// </summary>
        /// <remarks>
        /// An empty collection creates the Test Execution without environment associations.
        /// </remarks>
        public string[] TestEnvironments { get; set; } = [];

        /// <summary>
        /// Gets or sets the Jira issue keys of the Tests included in the new Test Execution.
        /// </summary>
        public string[] TestKeys { get; set; } = [];
        #endregion
    }
}
