namespace Xpandit.Client.Models
{
    /// <summary>
    /// Represents one Xray test-step custom field supplied to add or update mutations.
    /// </summary>
    /// <remarks>
    /// The value remains object-based because Xray exposes it as a GraphQL JSON scalar and individual
    /// custom fields can require strings, numbers, arrays, or structured JSON objects.
    /// </remarks>
    public class XrayCustomStepField
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Xray custom field identifier configured for manual test steps.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JSON-compatible field value forwarded without schema-specific conversion.
        /// </summary>
        /// <remarks>
        /// A null value is transmitted as JSON null, allowing an update request to clear a field when Xray
        /// permits that operation.
        /// </remarks>
        public object Value { get; set; }
        #endregion
    }
}
