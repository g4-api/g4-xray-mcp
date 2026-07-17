namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Identifies one manual Test Run Step and the sparse execution values to apply to it.
    /// </summary>
    /// <remarks>
    /// The step number is one-based for tool callers. Null outcome properties preserve their remote values, while
    /// empty actual-result and comment strings explicitly clear those fields in Xray.
    /// </remarks>
    public class UpdateExecutionModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the observed result, or null to preserve the current value.
        /// </summary>
        public string ActualResult { get; set; }

        /// <summary>
        /// Gets or sets the execution comment, or null to preserve the current value.
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// Gets or sets the Jira issue key of the Test Execution that owns the Test Run.
        /// </summary>
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional Xray data-set iteration rank, or null for an ordinary manual Test Run.
        /// </summary>
        public string IterationRank { get; set; }

        /// <summary>
        /// Gets or sets the one-based manual step number selected from the Test Run snapshot.
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// Gets or sets the Xray step-status name or identifier, or null to preserve the current status.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the Jira issue key of the Test represented by the Test Run.
        /// </summary>
        public string TestKey { get; set; } = string.Empty;
        #endregion
    }
}
