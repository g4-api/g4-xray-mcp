namespace Xpandit.Client.Models
{
    /// <summary>
    /// Identifies one Xray Test Run through the Test and Test Execution issues that own it.
    /// </summary>
    /// <remarks>
    /// Xray resolves this composite identity into the opaque Test Run identifier required by result mutations.
    /// Both values are positive numeric Jira issue identifiers rather than human-readable issue keys.
    /// </remarks>
    public class GetTestRunRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the Test Execution that owns the Test Run.
        /// </summary>
        public string TestExecutionIssueId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the Test represented by the Test Run.
        /// </summary>
        public string TestIssueId { get; set; } = string.Empty;
        #endregion
    }
}
