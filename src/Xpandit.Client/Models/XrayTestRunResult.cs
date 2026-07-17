using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Represents the partial Xray Test Run state required to execute and update a manual Test.
    /// </summary>
    /// <remarks>
    /// This model intentionally contains identity, status, and ordered manual steps only. Run evidence,
    /// defects, assignees, timing, parameters, and iterations remain outside the initial execution cycle.
    /// </remarks>
    public class XrayTestRunResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the opaque Xray Test Run identifier accepted by result mutations.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current Xray Test Run status name.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ordered manual steps captured by this Test Run.
        /// </summary>
        public IReadOnlyCollection<XrayTestRunStepResult> Steps { get; set; } = [];

        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the Test Execution that owns this run.
        /// </summary>
        public string TestExecutionIssueId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the Test represented by this run.
        /// </summary>
        public string TestIssueId { get; set; } = string.Empty;
        #endregion
    }
}
