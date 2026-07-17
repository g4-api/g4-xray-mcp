using System;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines bounded logical polling for a Test Run created by an Xray Test-to-execution association.
    /// </summary>
    /// <remarks>
    /// HTTP retry settings handle transport failures, while this request handles successful GraphQL responses
    /// that temporarily return no Test Run while Xray finishes registering the association.
    /// </remarks>
    public class WaitForTestRunRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the maximum number of Test Run queries issued before the operation reports not-found state.
        /// </summary>
        public int MaxAttempts { get; set; } = 7;

        /// <summary>
        /// Gets or sets the delay between successful queries that have not exposed the Test Run yet.
        /// </summary>
        public TimeSpan PollingDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the Test Execution that owns the expected Test Run.
        /// </summary>
        public string TestExecutionIssueId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the Test represented by the expected Test Run.
        /// </summary>
        public string TestIssueId { get; set; } = string.Empty;
        #endregion
    }
}
