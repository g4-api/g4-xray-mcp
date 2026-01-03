using Mcp.Xray.Domain.Clients;
using Mcp.Xray.Domain.Models;

namespace Mcp.Xray.Domain.Repositories
{
    public class XrayXpandRepository(JiraAuthenticationModel jiraAuthentication) : IXrayRepository
    {
        private readonly JiraClient _jiraClient = new(jiraAuthentication);

        private readonly XpandClient _xpandClient = new(jiraAuthentication);

        // TODO: Handle exceptions and errors from Jira and Xray clients.
        // TODO: Handle custom fields and additional metadata for test cases.
        // TODO: Handle Issue Types other than "Test" if needed.
        /// <inheritdoc />
        public object NewTest(string project, TestCaseModel testCase)
        {
            // Construct the Jira issue payload representing an Xray Test issue.
            // This payload defines the project, issue type, summary, and description.
            var testIssue = new
            {
                Fields = new
                {
                    Project = new
                    {
                        Key = project
                    },
                    Summary = testCase.Scenario,
                    Description = testCase.Actual,
                    Issuetype = new
                    {
                        Name = "Test"
                    }
                }
            };

            // Create the test issue in Jira and capture the raw response.
            // The response is expected to contain both an internal ID and a human-readable key.
            var jiraResponse = _jiraClient.NewIssue(testIssue);

            // Extract the issue identifier and key from the Jira response.
            var id = jiraResponse.GetProperty("id").GetString();
            var key = jiraResponse.GetProperty("key").GetString();

            // Build a direct browser link to the newly created test issue.
            var link = $"{jiraAuthentication.Collection}/browse/{key}";

            // Iterate through the defined test steps and register each one with Xray.
            // Steps are created in sequence to preserve their execution order.
            for (int i = 0; i < testCase.Steps.Length; i++)
            {
                var step = testCase.Steps[i];

                // Extract the action text and merge all expected results
                // into a single newline-delimited string.
                var action = step.Action;
                var result = string.Join('\n', step.ExpectedResults);

                // Create the test step in Xray, associating it with the Jira test issue.
                _xpandClient.NewTestStep(
                    test: (id, key),
                    action,
                    result,
                    index: i);
            }

            // Return a minimal, consumer-friendly representation of the created test.
            // This allows callers to reference the test without exposing raw Jira responses.
            return new
            {
                Id = id,
                Key = key,
                Link = link
            };
        }
    }
}
