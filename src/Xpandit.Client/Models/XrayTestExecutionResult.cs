using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Returns the new Test Execution identity, created environments, and non-fatal Xray warnings.
    /// </summary>
    public class XrayTestExecutionResult : XrayCreatedIssueResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets environment names that Xray created while creating the Test Execution.
        /// </summary>
        public IReadOnlyCollection<string> CreatedTestEnvironments { get; set; } = [];
        #endregion
    }
}
