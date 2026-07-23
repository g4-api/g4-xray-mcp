using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines a new Xray Test Execution, its initial Tests, and its execution environments.
    /// </summary>
    public class NewTestExecutionRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Jira fields used to create the Test Execution issue.
        /// </summary>
        public XrayJiraIssue Jira { get; set; } = new();

        /// <summary>
        /// Gets or sets environment names created or associated with the new Test Execution.
        /// </summary>
        /// <remarks>
        /// An empty collection leaves the Test Execution without initial Xray environments.
        /// </remarks>
        public IReadOnlyCollection<string> TestEnvironments { get; set; } = [];

        /// <summary>
        /// Gets or sets numeric Jira issue identifiers of Tests associated with the new Test Execution.
        /// </summary>
        /// <remarks>
        /// An empty collection creates the Test Execution without initial Test associations.
        /// </remarks>
        public IReadOnlyCollection<string> TestIssueIds { get; set; } = [];
        #endregion
    }
}
