using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Reports Test Execution identifiers accepted by an Xray Test Plan association mutation.
    /// </summary>
    public class XrayTestExecutionsAssociationResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets numeric Jira issue identifiers of Test Executions added by Xray.
        /// </summary>
        public IReadOnlyCollection<string> AddedTestExecutionIssueIds { get; set; } = [];

        /// <summary>
        /// Gets or sets non-fatal association messages returned by Xray.
        /// </summary>
        public IReadOnlyCollection<string> Warnings { get; set; } = [];
        #endregion
    }
}
