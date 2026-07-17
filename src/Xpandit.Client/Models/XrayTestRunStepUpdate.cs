namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines a sparse outcome update for one manual step inside an Xray Test Run.
    /// </summary>
    /// <remarks>
    /// Null properties are omitted and preserve remote values. Empty actual-result and comment strings are
    /// serialized so callers can explicitly clear those values, while status requires a non-empty Xray name or ID.
    /// </remarks>
    public class XrayTestRunStepUpdate
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the observed outcome, or null to preserve the current actual result.
        /// </summary>
        public string ActualResult { get; set; }

        /// <summary>
        /// Gets or sets the execution note, or null to preserve the current comment.
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// Gets or sets the Xray step-status name or identifier, or null to preserve the current status.
        /// </summary>
        public string Status { get; set; }
        #endregion
    }
}
