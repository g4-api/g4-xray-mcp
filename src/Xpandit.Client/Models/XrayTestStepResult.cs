using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Returns the persisted identity and editable content of an Xray manual test step.
    /// </summary>
    /// <remarks>
    /// This response models only the fields selected by the add-step mutation. Attachments and called-Test
    /// metadata remain outside the initial client scope.
    /// </remarks>
    public class XrayTestStepResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the action text persisted by Xray.
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets the custom step fields returned by Xray after persistence.
        /// </summary>
        public IReadOnlyCollection<XrayCustomStepField> CustomFields { get; set; } = [];

        /// <summary>
        /// Gets or sets the test data persisted by Xray.
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Gets or sets the stable Xray step identifier required for later updates.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the expected-result text persisted by Xray.
        /// </summary>
        public string Result { get; set; }
        #endregion
    }
}
