using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines a new Xray Test Plan and the optional Tests associated during creation.
    /// </summary>
    public class NewTestPlanRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Jira fields used to create the Test Plan issue.
        /// </summary>
        public XrayJiraIssue Jira { get; set; } = new();

        /// <summary>
        /// Gets or sets numeric Jira issue identifiers of Tests associated with the new Test Plan.
        /// </summary>
        /// <remarks>
        /// An empty collection creates the Test Plan without initial Test associations.
        /// </remarks>
        public IReadOnlyCollection<string> TestIssueIds { get; set; } = [];
        #endregion
    }
}
