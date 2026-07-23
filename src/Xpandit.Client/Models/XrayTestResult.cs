using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Returns one Xray Test identity, type-specific definition, Jira key, and ordered manual steps.
    /// </summary>
    /// <remarks>
    /// This partial external model contains the public GraphQL fields needed by test-management consumers.
    /// Associations, execution history, attachments, and repository placement remain outside this retrieval contract.
    /// </remarks>
    public class XrayTestResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Gherkin definition returned for Cucumber Test types.
        /// </summary>
        /// <remarks>
        /// Null when the Test uses a Manual or Unstructured definition.
        /// </remarks>
        public string Gherkin { get; set; }

        /// <summary>
        /// Gets or sets the numeric Jira issue identifier registered as an Xray Test.
        /// </summary>
        public string IssueId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable Jira issue key returned by Xray.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric Jira project identifier that owns the Test.
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ordered manual step definition returned by Xray.
        /// </summary>
        /// <remarks>
        /// Empty when the Test type uses an Unstructured or Gherkin definition instead of manual steps.
        /// </remarks>
        public IReadOnlyCollection<XrayTestStepResult> Steps { get; set; } = [];

        /// <summary>
        /// Gets or sets the Xray Test type kind used to select the applicable definition shape.
        /// </summary>
        public string TestTypeKind { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the configured Xray Test type name.
        /// </summary>
        public string TestTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the free-form definition returned for Unstructured Test types.
        /// </summary>
        /// <remarks>
        /// Null when the Test uses a Manual or Gherkin definition.
        /// </remarks>
        public string Unstructured { get; set; }
        #endregion
    }
}
