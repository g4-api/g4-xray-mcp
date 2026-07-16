using System;
using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Describes the Jira fields shared by Xray Test Set, Test Plan, and Test Execution creation commands.
    /// </summary>
    /// <remarks>
    /// Xray determines the issue type from the selected mutation, so this partial Jira model contains only
    /// the required project and summary fields. Additional fields preserve access to Jira custom fields and
    /// Atlassian Document Format values without coupling the client to a Jira SDK.
    /// </remarks>
    public class XrayJiraIssue
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets extra Jira field values forwarded inside the mutation's <c>jira.fields</c> object.
        /// </summary>
        /// <remarks>
        /// An empty dictionary sends only project and summary. The reserved keys <c>project</c> and
        /// <c>summary</c> are rejected so strongly typed values remain authoritative.
        /// </remarks>
        public IDictionary<string, object> AdditionalFields { get; set; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the Jira project key where Xray creates the issue.
        /// </summary>
        public string ProjectKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable Jira summary assigned to the new Xray issue.
        /// </summary>
        public string Summary { get; set; } = string.Empty;
        #endregion
    }
}
