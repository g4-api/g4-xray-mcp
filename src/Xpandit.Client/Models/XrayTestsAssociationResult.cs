using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Reports Test identifiers accepted by an Xray Test Plan or Test Execution association mutation.
    /// </summary>
    public class XrayTestsAssociationResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets numeric Jira issue identifiers of Tests added by Xray.
        /// </summary>
        public IReadOnlyCollection<string> AddedTestIssueIds { get; set; } = [];

        /// <summary>
        /// Gets or sets non-fatal association messages returned by Xray.
        /// </summary>
        public IReadOnlyCollection<string> Warnings { get; set; } = [];
        #endregion
    }
}
