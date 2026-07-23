namespace Xpandit.Client.Models
{
    /// <summary>
    /// Identifies an existing Xray Test and the existing Test Repository folder that will contain it.
    /// </summary>
    public class MoveTestToFolderRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the numeric Jira issue identifier of the Xray Test to move.
        /// </summary>
        public string IssueId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the existing destination path in the Test Repository.
        /// </summary>
        /// <remarks>
        /// The repository normalizes the path but does not create it; callers use <c>NewFolderAsync</c>
        /// when the destination may be absent.
        /// </remarks>
        public string Path { get; set; } = string.Empty;
        #endregion
    }
}
