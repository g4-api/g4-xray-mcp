using Mcp.Xray.Domain.Models;

namespace Mcp.Xray.Domain.Repositories
{
    public interface IXrayRepository
    {
        /// <summary>
        /// Creates a new Xray test case in Jira and populates it with the provided test steps.
        /// </summary>
        /// <param name="project">The Jira project key under which the test case will be created.</param>
        /// <param name="testCase">The test case definition containing scenario details and execution steps.</param>
        /// <returns>An object containing the created test issue identifier, key, and a direct link in Jira.</returns>
        object NewTest(string project, TestCaseModel testCase);
    }
}
