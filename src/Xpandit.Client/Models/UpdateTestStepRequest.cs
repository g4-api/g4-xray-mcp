namespace Xpandit.Client.Models
{
    /// <summary>
    /// Carries the step identity and partial replacement values for Xray's update-test-step mutation.
    /// </summary>
    public class UpdateTestStepRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Xray step identifier returned when the manual step was created or queried.
        /// </summary>
        public string StepId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the subset of step values that Xray updates.
        /// </summary>
        public XrayTestStepUpdate Step { get; set; } = new();
        #endregion
    }
}
