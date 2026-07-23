namespace Xpandit.Client.Models
{
    /// <summary>
    /// Represents one manual step snapshot and its recorded outcome inside an Xray Test Run.
    /// </summary>
    /// <remarks>
    /// The model separates the expected result copied from the Test definition from the actual result recorded
    /// during execution. Attachments, evidence, defects, and custom fields remain outside the initial scope.
    /// </remarks>
    public class XrayTestRunStepResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the action copied into the Test Run from the Test definition.
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets the actual result recorded while executing this run step.
        /// </summary>
        public string ActualResult { get; set; }

        /// <summary>
        /// Gets or sets the execution comment recorded on this run step.
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// Gets or sets the test data copied into the Test Run from the Test definition.
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Gets or sets the expected result copied into the Test Run from the Test definition.
        /// </summary>
        public string ExpectedResult { get; set; }

        /// <summary>
        /// Gets or sets the opaque Xray Test Run Step identifier accepted by result mutations.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current Xray status name of this Test Run Step.
        /// </summary>
        public string Status { get; set; } = string.Empty;
        #endregion
    }
}
