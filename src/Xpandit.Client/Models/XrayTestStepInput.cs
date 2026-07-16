using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines the editable content used when adding a manual step to an existing Xray Test.
    /// </summary>
    /// <remarks>
    /// Attachments are intentionally excluded from this initial standalone contract. The selected fields
    /// cover Xray's textual step definition and custom-field values without introducing binary ownership.
    /// </remarks>
    public class XrayTestStepInput
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the instruction performed by the tester for this step.
        /// </summary>
        /// <remarks>
        /// Null omits the action from the GraphQL input when the installed Xray configuration permits a
        /// step driven by another field.
        /// </remarks>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets custom values configured for manual test steps in the target Xray project.
        /// </summary>
        public IReadOnlyCollection<XrayCustomStepField> CustomFields { get; set; } = [];

        /// <summary>
        /// Gets or sets the test data consumed while performing the action.
        /// </summary>
        /// <remarks>
        /// Null omits test data so Xray retains the semantic distinction between absent and empty content.
        /// </remarks>
        public string Data { get; set; }

        /// <summary>
        /// Gets or sets the expected result used to evaluate the step.
        /// </summary>
        /// <remarks>
        /// Null omits the expected result when the caller has no result text to persist.
        /// </remarks>
        public string Result { get; set; }
        #endregion
    }
}
