namespace Xpandit.Client.Models
{
    /// <summary>
    /// Carries the Test identity and step definition required by Xray's add-test-step mutation.
    /// </summary>
    public class AddTestStepRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the existing Xray Test.
        /// </summary>
        public string IssueId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the manual test-step content sent to Xray.
        /// </summary>
        public XrayTestStepInput Step { get; set; } = new();

        /// <summary>
        /// Gets or sets the Xray Test version identifier targeted by the mutation.
        /// </summary>
        /// <remarks>
        /// Zero targets the default Test version and omits the version variable. Positive values target an
        /// explicit Xray Test version; negative values are rejected before any remote request is sent.
        /// </remarks>
        public int VersionId { get; set; }
        #endregion
    }
}
