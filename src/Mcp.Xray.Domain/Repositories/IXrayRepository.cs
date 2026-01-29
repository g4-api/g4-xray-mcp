using Mcp.Xray.Domain.Models;

namespace Mcp.Xray.Domain.Repositories
{
    /// <summary>
    /// Defines methods for interacting with the Xray Test Repository in Jira
    /// including test case creation, folder management, and test organization.
    /// </summary>
    public interface IXrayRepository
    {
        /// <summary>
        /// Moves Jira issues selected by a JQL query into a folder in the Xray Test Repository.
        /// </summary>
        /// <param name="idOrKey">The Jira project identifier or project key that defines the repository scope.</param>
        /// <param name="path">The repository folder path that will receive the selected tests.</param>
        /// <param name="jql">The JQL query used to select the issues to move into the folder.</param>
        /// <returns>
        /// An object containing the number of moved issues, the resolved folder identifier,
        /// the resolved path, and the raw response returned by the Xray internal endpoint.
        /// </returns>
        object AddTestsToFolder(string idOrKey, string path, string jql);

        /// <summary>
        /// Gets an existing Xray test case by its Jira issue identifier or key.
        /// </summary>
        /// <param name="idOrKey">The Jira issue identifier or key.</param>
        /// <returns>An object representing the existing Xray test case.</returns>
        object GetTest(string idOrKey);

        /// <summary>
        /// Creates a new Xray test case in Jira and populates it with the provided test steps.
        /// </summary>
        /// <param name="project">The Jira project key under which the test case will be created.</param>
        /// <param name="testCase">The test case definition containing scenario details and execution steps.</param>
        /// <returns>An object containing the created test issue identifier, key, and a direct link in Jira.</returns>
        object NewTest(string project, TestCaseModel testCase);

        /// <summary>
        /// Creates a new folder in the Xray Test Repository under the specified project
        /// and parent path.
        /// </summary>
        /// <param name="idOrKey">The Jira project identifier or project key that defines the repository scope.</param>
        /// <param name="name">The display name of the folder to be created.</param>
        /// <param name="path">The parent folder path within the Xray Test Repository. An empty value indicates creation at the repository root.</param>
        /// <returns>An object containing the created folder identifier and its resolved repository path.</returns>
        object NewTestRepositoryFolder(string idOrKey, string name, string path);

        /// <summary>
        /// Resolves a folder path within the Xray Test Repository to its corresponding folder identifier.
        /// </summary>
        /// <param name="idOrKey">The Jira project identifier or project key that defines the repository scope.</param>
        /// <param name="path">The hierarchical folder path within the Xray Test Repository to resolve.</param>
        /// <returns>The resolved folder identifier corresponding to the specified path.</returns>
        string ResolveFolderPath(string idOrKey, string path);

        /// <summary>
        /// Updates an existing Xray test case by first resolving its Jira project
        /// and then delegating to the project-scoped update operation.
        /// </summary>
        /// <param name="key">The Jira issue key that identifies the existing Xray test.</param>
        /// <param name="testCase">The updated test case definition containing the new values to apply.</param>
        /// <returns>The updated Xray test entity as returned by the underlying update operation.</returns>
        object UpdateTest(string key, TestCaseModel testCase);
    }
}
