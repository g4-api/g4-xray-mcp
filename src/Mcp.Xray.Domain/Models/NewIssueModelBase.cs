using System;
using System.Collections.Concurrent;

namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Base model for creating new Jira or Xray issues.
    /// This base type defines common fields and contextual data used
    /// when constructing new issues in external systems such as
    /// Jira or Xray. It is intended to be extended by concrete
    /// issue models (e.g. Test, Test Plan, Test Execution).
    /// </summary>
    public abstract class NewIssueModelBase
    {
        #region *** Constants  ***
        // Case-insensitive string comparer used for contextual keys.
        // Context values may originate from multiple layers
        // (runtime, environment, overrides), so key comparison
        // is intentionally case-insensitive.
        private static readonly StringComparer _comparer =
            StringComparer.OrdinalIgnoreCase;
        #endregion

        #region *** Properties ***
        /// <summary>
        /// Gets or sets contextual key-value data associated with this issue.
        /// Context values may include runtime parameters, environment-specific
        /// values, or dynamic overrides that influence test behavior,
        /// execution, or external system integration.
        /// </summary>
        public ConcurrentDictionary<string, object> Context { get; set; } =
            new ConcurrentDictionary<string, object>(_comparer);

        /// <summary>
        /// Gets or sets the custom field values associated with this issue.
        /// Custom fields are typically used for mapping domain data
        /// to external systems such as Jira or Xray and may represent
        /// both standard and project-specific fields.
        /// </summary>
        public CustomFieldModel[] CustomFields { get; set; } = [];

        /// <summary>
        /// Gets or sets the detailed description of the issue.
        /// This value is typically rendered as the main body content
        /// of the issue in Jira or Xray.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the short summary or title of the issue.
        /// The summary is used as the primary identifier text
        /// when listing or referencing the issue.
        /// </summary>
        public string Summary { get; set; }
        #endregion
    }
}
