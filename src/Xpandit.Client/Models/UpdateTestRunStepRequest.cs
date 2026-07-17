namespace Xpandit.Client.Models
{
    /// <summary>
    /// Carries the Test Run identity, run-step identity, and sparse manual execution values sent to Xray.
    /// </summary>
    public class UpdateTestRunStepRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the optional data-set iteration rank that owns the run step.
        /// </summary>
        /// <remarks>
        /// Null omits the GraphQL variable for an ordinary manual Test Run. Data-driven callers provide the
        /// Xray iteration rank so the mutation changes the matching iteration rather than the base run step.
        /// </remarks>
        public string IterationRank { get; set; }

        /// <summary>
        /// Gets or sets the opaque Xray identifier of the Test Run Step being updated.
        /// </summary>
        public string StepId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the opaque Xray identifier of the Test Run that owns the step.
        /// </summary>
        public string TestRunId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the subset of manual execution values that Xray applies to the run step.
        /// </summary>
        public XrayTestRunStepUpdate Update { get; set; } = new();
        #endregion
    }
}
