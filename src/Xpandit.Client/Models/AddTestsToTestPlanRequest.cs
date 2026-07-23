using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines Test associations added to an existing Xray Test Plan.
    /// </summary>
    /// <remarks>
    /// The request models only numeric Jira issue identities because the association mutation does not update
    /// Jira fields or create Test Executions.
    /// </remarks>
    public class AddTestsToTestPlanRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets numeric Jira issue identifiers of Tests added to the Test Plan.
        /// </summary>
        public IReadOnlyCollection<string> TestIssueIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the owning Test Plan.
        /// </summary>
        public string TestPlanIssueId { get; set; } = string.Empty;
        #endregion
    }
}
