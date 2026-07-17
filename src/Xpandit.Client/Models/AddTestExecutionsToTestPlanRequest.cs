using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines Test Execution associations added to an existing Xray Test Plan.
    /// </summary>
    /// <remarks>
    /// Xray accepts numeric Jira issue identifiers for both entities. The request intentionally models only
    /// association data because Jira fields and execution results remain unchanged by this mutation.
    /// </remarks>
    public class AddTestExecutionsToTestPlanRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets numeric Jira issue identifiers of Test Executions added to the Test Plan.
        /// </summary>
        public IReadOnlyCollection<string> TestExecutionIssueIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the owning Test Plan.
        /// </summary>
        public string TestPlanIssueId { get; set; } = string.Empty;
        #endregion
    }
}
