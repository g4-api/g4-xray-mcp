using System.Text.Json;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Represents an Xray Test Repository folder and the raw recursive child tree returned by GraphQL.
    /// </summary>
    /// <remarks>
    /// Xray exposes child folders as a GraphQL JSON scalar rather than a stable object schema. The client
    /// strongly types the documented folder metadata and preserves the child JSON without losing fields that
    /// may vary between Xray versions.
    /// </remarks>
    public class XrayFolder
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the recursive child-folder JSON supplied by Xray.
        /// </summary>
        /// <remarks>
        /// <see cref="JsonValueKind.Undefined"/> represents a response without child-folder data. A root query
        /// normally returns an array or object that callers can inspect with <see cref="JsonElement"/> APIs.
        /// </remarks>
        public JsonElement Folders { get; set; }

        /// <summary>
        /// Gets or sets the number of all folder issues reported by Xray.
        /// </summary>
        public int IssuesCount { get; set; }

        /// <summary>
        /// Gets or sets the final segment name of the repository folder.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized absolute path of the repository folder.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of Preconditions assigned beneath the folder.
        /// </summary>
        public int PreconditionsCount { get; set; }

        /// <summary>
        /// Gets or sets the number of Tests assigned beneath the folder.
        /// </summary>
        public int TestsCount { get; set; }
        #endregion
    }
}
