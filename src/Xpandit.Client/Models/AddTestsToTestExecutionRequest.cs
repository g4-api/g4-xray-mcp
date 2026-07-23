using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines Test associations added to an existing Xray Test Execution.
    /// </summary>
    /// <remarks>
    /// Xray creates or exposes one Test Run for each associated Test. This request selects default Test versions;
    /// explicit Test-version associations remain outside the initial manual-execution workflow.
    /// </remarks>
    public class AddTestsToTestExecutionRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the owning Test Execution.
        /// </summary>
        public string TestExecutionIssueId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets numeric Jira issue identifiers of Tests added to the Test Execution.
        /// </summary>
        public IReadOnlyCollection<string> TestIssueIds { get; set; } = [];
        #endregion
    }
}
