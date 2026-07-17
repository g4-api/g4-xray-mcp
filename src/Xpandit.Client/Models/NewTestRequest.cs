using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines an Xray Test created with its Jira fields, Test type, and ordered manual steps in one mutation.
    /// </summary>
    /// <remarks>
    /// Keeping issue and Test-definition creation in one Xray command removes the registration gap that exists
    /// when Jira creates an issue before Xray receives its manual step definition.
    /// </remarks>
    public class NewTestRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Jira fields used by Xray to create the Test issue.
        /// </summary>
        public XrayJiraIssue Jira { get; set; } = new();

        /// <summary>
        /// Gets or sets the ordered manual steps created with the Test.
        /// </summary>
        /// <remarks>
        /// An empty collection creates a Manual Test without an initial step definition.
        /// </remarks>
        public IReadOnlyCollection<XrayTestStepInput> Steps { get; set; } = [];

        /// <summary>
        /// Gets or sets the Xray Test type name assigned during creation.
        /// </summary>
        /// <remarks>
        /// The default targets Xray's Manual Test type and is independent from the Jira issue type named Test.
        /// </remarks>
        public string TestTypeName { get; set; } = "Manual";
        #endregion
    }
}
