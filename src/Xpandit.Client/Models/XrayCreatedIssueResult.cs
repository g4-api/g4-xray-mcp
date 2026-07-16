using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Returns the stable Jira identity and Xray warnings produced by an issue-creation mutation.
    /// </summary>
    /// <remarks>
    /// This partial response intentionally excludes expanded Jira fields because command callers need the
    /// numeric ID for later GraphQL commands and the key for human-facing diagnostics.
    /// </remarks>
    public class XrayCreatedIssueResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the numeric Jira issue identifier accepted by later Xray GraphQL commands.
        /// </summary>
        public string IssueId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable Jira issue key returned by Xray.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets non-fatal messages returned after Xray completed the mutation.
        /// </summary>
        public IReadOnlyCollection<string> Warnings { get; set; } = [];
        #endregion
    }
}
