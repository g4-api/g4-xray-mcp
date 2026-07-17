using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Returns the registered Xray Test identity, type, persisted steps, and creation warnings.
    /// </summary>
    /// <remarks>
    /// The result extends the shared Jira identity because Test creation also verifies the Xray-side definition
    /// returned by the same mutation.
    /// </remarks>
    public class XrayCreatedTestResult : XrayCreatedIssueResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the ordered manual steps returned after Test creation.
        /// </summary>
        public IReadOnlyCollection<XrayTestStepResult> Steps { get; set; } = [];

        /// <summary>
        /// Gets or sets the Xray Test type name confirmed by the creation response.
        /// </summary>
        public string TestTypeName { get; set; } = string.Empty;
        #endregion
    }
}
