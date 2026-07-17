namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines the final or intermediate Xray status assigned to one Test Run.
    /// </summary>
    /// <remarks>
    /// A Test Execution can contain multiple Test Runs with different outcomes. This request therefore targets
    /// the opaque Test Run identity rather than assigning one aggregate result to the Test Execution issue.
    /// </remarks>
    public class UpdateTestRunStatusRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Xray Test Run status name or identifier.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the opaque Xray identifier of the Test Run receiving the status.
        /// </summary>
        public string TestRunId { get; set; } = string.Empty;
        #endregion
    }
}
