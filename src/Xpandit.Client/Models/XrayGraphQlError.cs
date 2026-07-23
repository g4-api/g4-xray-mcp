using System.Collections.Generic;
using System.Text.Json;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Captures the stable portions of one GraphQL error returned by Xray.
    /// </summary>
    /// <remarks>
    /// GraphQL extensions are provider-defined, so the client preserves them as JSON while exposing the
    /// standard message and path used for diagnostics and automated error handling.
    /// </remarks>
    public class XrayGraphQlError
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets provider-specific GraphQL error metadata.
        /// </summary>
        /// <remarks>
        /// <see cref="JsonValueKind.Undefined"/> indicates that Xray returned no extensions object.
        /// </remarks>
        public JsonElement Extensions { get; set; }

        /// <summary>
        /// Gets or sets the human-readable GraphQL failure message returned by Xray.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the GraphQL response path identifying the field that failed.
        /// </summary>
        /// <remarks>
        /// An empty collection indicates that Xray did not associate the error with a response field.
        /// </remarks>
        public IReadOnlyCollection<string> Path { get; set; } = [];
        #endregion
    }
}
