using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines a partial update for an existing Xray manual test step.
    /// </summary>
    /// <remarks>
    /// Nullable properties are deliberate: null omits a field and preserves its remote value, while an
    /// empty string or empty custom-field collection explicitly replaces the remote value.
    /// </remarks>
    public class XrayTestStepUpdate
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets replacement action text, or null to leave the current action unchanged.
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets replacement custom fields, or null to leave all current custom fields unchanged.
        /// </summary>
        /// <remarks>
        /// An empty collection is serialized and requests removal of all values when Xray supports clearing
        /// the configured custom fields.
        /// </remarks>
        public IReadOnlyCollection<XrayCustomStepField> CustomFields { get; set; }

        /// <summary>
        /// Gets or sets replacement test data, or null to leave the current data unchanged.
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Gets or sets replacement expected-result text, or null to leave the current result unchanged.
        /// </summary>
        public string Result { get; set; }
        #endregion
    }
}
