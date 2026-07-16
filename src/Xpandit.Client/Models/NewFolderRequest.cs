namespace Xpandit.Client.Models
{
    /// <summary>
    /// Identifies a Test Repository path that the client ensures exists from root to leaf.
    /// </summary>
    public class NewFolderRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the complete folder path to create beneath the repository root.
        /// </summary>
        /// <remarks>
        /// Every missing cumulative segment is created in order. Existing segments remain unchanged.
        /// </remarks>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric Jira project identifier that owns the Test Repository.
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;
        #endregion
    }
}
