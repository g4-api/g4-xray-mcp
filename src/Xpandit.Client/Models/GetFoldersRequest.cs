namespace Xpandit.Client.Models
{
    /// <summary>
    /// Identifies a project Test Repository folder returned by Xray's folder query.
    /// </summary>
    public class GetFoldersRequest
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the Test Repository path to retrieve.
        /// </summary>
        /// <remarks>
        /// The root path <c>/</c> returns the complete project testing tree. Relative input is normalized to
        /// an absolute repository path before sending the query.
        /// </remarks>
        public string Path { get; set; } = "/";

        /// <summary>
        /// Gets or sets the numeric Jira project identifier that owns the Test Repository.
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;
        #endregion
    }
}
