using Mcp.Xray.Domain.Models;

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Mcp.Xray.Domain.Framework
{
    /// <summary>
    /// Provides factory methods for constructing HTTP commands used with the Xray and Xpandit
    /// internal APIs. This class centralizes the creation of request payloads, headers, and routes
    /// for test management operations such as linking issues, loading test runs, updating steps,
    /// and managing test plans and executions.
    /// </summary>
    internal static class XpandCommands
    {
        #region *** Methods ***
        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that associates a defect with a specific Xray test execution.
        /// The method prepares the request body using the issue identifier and key, then sends the request
        /// to the internal Xray API endpoint for attaching defects to a test run.
        /// </summary>
        /// <param name="idAndKey">A tuple containing both the numeric Jira issue identifier and the textual issue key.</param>
        /// <param name="testRunId">The identifier of the Xray test execution to which the defect will be added.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that performs the defect attachment operation.</returns>
        public static HttpCommand AddDefectToTestRun((string Id, string Key) idAndKey, string testRunId)
        {
            // Builds the payload using the provided tuple fields. 
            // Xray requires both the issue ID and the issue key.
            var data = new
            {
                idAndKey.Id,
                idAndKey.Key
            };

            // Returns a POST command with the constructed body, 
            // required headers for Xray, and the execution-specific route.
            return new HttpCommand
            {
                Data = data,
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/testrun/{testRunId}/defects"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that attaches a test execution to an Xray test plan.
        /// The method prepares the request body using the execution identifier and sends the command
        /// to the Xray endpoint responsible for linking executions to a test plan.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test plan identifier and its corresponding issue key.</param>
        /// <param name="executionId">The identifier of the test execution that will be added to the test plan.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that performs the operation of adding the execution to the specified test plan.</returns>
        public static HttpCommand AddExecutionToPlan((string Id, string Key) idAndKey, string executionId)
        {
            // The request body contains the execution identifier inside an array,
            // which matches the structure expected by the Xray API.
            return new HttpCommand
            {
                Data = new[] { executionId },
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/testplan/{idAndKey.Id}/addTestExecs"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that links one or more preconditions to an Xray test issue.
        /// The method prepares the request body using the supplied precondition identifiers and sends the
        /// command to the Xray endpoint that manages precondition associations.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test issue identifier and its corresponding issue key.</param>
        /// <param name="preconditionsIds">One or more Jira issue identifiers representing the preconditions to associate with the test.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that adds the specified preconditions to the given test issue.</returns>
        public static HttpCommand AddPrecondition((string Id, string Key) idAndKey, params string[] preconditionsIds)
        {
            // The request body contains the list of precondition identifiers.
            // The issue key is included in the request headers for Xray authentication and tracking.
            return new HttpCommand
            {
                Data = preconditionsIds,
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/issuelinks/test/{idAndKey.Id}/preConditions"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that adds one or more test issues to an Xray test execution.
        /// The method prepares the request body using the supplied test identifiers and sends the command
        /// to the internal Xray endpoint responsible for linking tests to a test execution.
        /// </summary>
        /// <param name="executionIdAndKey">A tuple containing the execution issue identifier and its corresponding issue key.</param>
        /// <param name="testsIds">One or more Jira issue identifiers representing the tests that should be added to the execution.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that performs the operation of adding tests to the specified test execution.</returns>
        public static HttpCommand AddTestToExecution((string Id, string Key) executionIdAndKey, params string[] testsIds)
        {
            // The body contains the list of test issue identifiers that will be linked to the execution.
            // The headers include the issue key for authentication and tracking.
            return new HttpCommand
            {
                Data = testsIds,
                Headers = NewHeaders(issueKey: executionIdAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/issuelinks/testexec/{executionIdAndKey.Id}/tests"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that adds one or more test issues to an Xray test set.
        /// The method prepares the request body using the provided test identifiers and sends the request
        /// to the relevant Xray endpoint responsible for managing test set contents.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test set identifier and its corresponding Jira issue key.</param>
        /// <param name="testsIds">One or more Jira issue identifiers representing the tests that should be added to the test set.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that performs the operation of adding the specified tests to the given Xray test set.</returns>
        public static HttpCommand AddTestsToSet((string Id, string Key) idAndKey, params string[] testsIds)
        {
            // The payload consists of the test identifiers. 
            // The issue key is added to the headers for Xray authentication or routing logic.
            return new HttpCommand
            {
                Data = testsIds,
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/issuelinks/testset/{idAndKey.Id}/tests"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves metadata for the Xray execution details page.
        /// The method constructs the querystring parameters expected by the Xray servlet endpoint and 
        /// returns a GET command targeting that resource.
        /// </summary>
        /// <param name="executionKey">The key of the test execution whose details should be retrieved.</param>
        /// <param name="testKey">The key of the test associated with the execution metadata request.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that fetches the execution detail metadata from Xray.</returns>
        public static HttpCommand GetExecutionDetailsMeta(string executionKey, string testKey)
        {
            // This format string represents the Xray servlet endpoint used for retrieving JSON metadata
            // for the execution page when viewed in the Xray UI.
            const string Format = "/plugins/servlet/ac/com.xpandit.plugins.xray/execution-page" +
                "?classifier=json" +
                "&ac.testExecIssueKey={0}" +
                "&ac.testIssueKey={1}";

            // Returns a GET command with no request body and a formatted route containing both keys.
            return new HttpCommand
            {
                Method = HttpMethod.Get,
                Route = string.Format(Format, testKey, executionKey)
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves an Xray test run resource based on
        /// the given test key and execution key. The method constructs the querystring format used
        /// by the Xray internal API and prepares the command for a GET request.
        /// </summary>
        /// <param name="executionKey">The key of the test execution whose run data should be loaded.</param>
        /// <param name="testKey">The key of the test whose run details are being requested.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that loads the test run information for the specified execution and test.</returns>
        public static HttpCommand GetLoadTestRun(string executionKey, string testKey)
        {
            // Defines the Xray internal endpoint used to load the test run data.
            // The placeholders correspond to the test key and execution key.
            const string Format = "/api/internal/load-test-run?testIssueKey={0}&testExecIssueKey={1}";

            // Returns a GET command that includes the issue key header and targets the formatted route.
            return new HttpCommand
            {
                Headers = NewHeaders(issueKey: testKey),
                Method = HttpMethod.Get,
                Route = string.Format(Format, testKey, executionKey)
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves all Xray test plans
        /// associated with the specified test issue. The method prepares a GET request 
        /// that queries inbound links pointing from test plans to the given test.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test issue identifier and its corresponding Jira issue key.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that returns the test plans linked to the specified test.</returns>
        public static HttpCommand GetPlansByTest((string Id, string Key) idAndKey)
        {
            // The command includes the issue key in the headers and targets the Xray internal
            // endpoint that lists inbound test plan links for the given test.
            return new HttpCommand
            {
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Get,
                Route = $"/api/internal/issuelinks/testPlan/{idAndKey.Id}/tests?direction=inward"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves all Xray preconditions
        /// linked to the specified test issue. The method prepares a GET request 
        /// that targets the internal Xray endpoint responsible for listing preconditions.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test issue identifier and its corresponding Jira issue key.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that fetches the preconditions associated with the given test.</returns>
        public static HttpCommand GetPreconditionsByTest((string Id, string Key) idAndKey)
        {
            // The command includes the issue key in the headers for authentication and tracking,
            // and the route targets the preconditions linked to the test.
            return new HttpCommand
            {
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Get,
                Route = $"/api/internal/issuelinks/test/{idAndKey.Id}/preConditions"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves the test runs associated
        /// with a specific Xray test execution. The method prepares a request body that
        /// specifies which fields should be returned and sends a POST request to the
        /// internal Xray endpoint responsible for listing test runs.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the numeric execution identifier and its corresponding Jira issue key.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that fetches the test runs belonging to the given execution.</returns>
        public static HttpCommand GetRunsByExecution((string Id, string Key) idAndKey)
        {
            // The request body indicates which fields should be returned in the response.
            // Xray expects a fields object, and the values below specify status and key.
            var data = new
            {
                Fields = new[]
                {
                    "status",
                    "key"
                }
            };

            // Returns a POST command using the defined request body and headers.
            // The execution identifier is placed inside the query parameter as required by Xray.
            return new HttpCommand
            {
                Data = data,
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/testruns?testExecIssueId={idAndKey.Id}"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves all Xray test sets
        /// associated with the specified test issue. The method prepares a GET request
        /// that queries inbound links from test sets pointing to the given test.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test issue identifier and its corresponding Jira issue key.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that fetches the test sets linked to the specified test.</returns>
        public static HttpCommand GetSetsByTest((string Id, string Key) idAndKey)
        {
            // Returns a GET command including issue key headers. The route targets the internal
            // Xray endpoint that lists test sets referencing the test through inbound links.
            return new HttpCommand
            {
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Get,
                Route = $"/api/internal/issuelinks/testset/{idAndKey.Id}/tests?direction=inward"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves the steps belonging to an Xray test.
        /// The method prepares a GET request targeting the internal Xray endpoint that returns 
        /// the ordered step definitions for the given test issue.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the numeric test identifier and the corresponding Jira issue key.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that loads the steps for the specified Xray test.</returns>
        public static HttpCommand GetSteps((string Id, string Key) idAndKey)
        {
            // Returns a GET command with the issue key included in the headers.
            // The route requests all steps for the test, using a high maxResults value
            // to ensure the full list is returned.
            return new HttpCommand
            {
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Get,
                Route = $"/api/internal/test/{idAndKey.Id}/steps?startAt=0&maxResults=1000"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves the tests associated with
        /// a specific Xray test plan. The method prepares a POST request containing the
        /// required body for returning finalized test data.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test plan identifier and its corresponding Jira issue key.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that obtains the tests linked to the specified test plan.</returns>
        public static HttpCommand GetTestsByPlan((string Id, string Key) idAndKey)
        {
            // The request body indicates that only finalized test data should be returned.
            // Xray expects this value when loading tests from a test plan.
            return new HttpCommand
            {
                Data = new { IsFinal = true },
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/testplan/{idAndKey.Id}/tests"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves all Xray test issues 
        /// associated with the specified test set. The method prepares a GET request 
        /// targeting the internal Xray endpoint that exposes the tests contained in a test set.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test set identifier and its corresponding Jira issue key.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that returns the tests belonging to the specified test set.</returns>
        public static HttpCommand GetTestsBySet((string Id, string Key) idAndKey)
        {
            // Returns a GET command including the issue key header. 
            // The route points to the testset endpoint that lists associated tests.
            return new HttpCommand
            {
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Get,
                Route = $"/api/internal/issuelinks/testset/{idAndKey.Id}/tests"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that posts a new comment to an Xray test execution.
        /// The method prepares the required request body and sends the comment to the
        /// internal Xray endpoint responsible for handling test run comments.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the execution identifier and its corresponding Jira issue key.</param>
        /// <param name="comment">The text of the comment that should be added to the test execution.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that submits the provided comment to the specified test execution.</returns>
        public static HttpCommand NewCommentOnExecution((string Id, string Key) idAndKey, string comment)
        {
            // Returns a POST command that contains the comment in the request body.
            // The issue key is passed to the headers for authentication and routing.
            return new HttpCommand
            {
                Data = new { Comment = comment },
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/testRun/{idAndKey.Id}/comment"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that adds a new step to an Xray test issue.
        /// The method prepares the request body using the supplied action, result, and index,
        /// then sends a POST request to the internal Xray endpoint that manages test steps.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test issue identifier and its corresponding Jira issue key.</param>
        /// <param name="action">The action text that describes what should be performed in the step.</param>
        /// <param name="result">The expected result text associated with the step.</param>
        /// <param name="index">The position at which the step should be inserted into the test.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that creates a new test step in the specified Xray test.</returns>
        public static HttpCommand NewTestStep((string Id, string Key) idAndKey, string action, string result, int index)
        {
            // Returns a POST command containing the step definition.
            // The issue key is included in the headers for authentication and routing.
            return new HttpCommand
            {
                Data = new { action, result, index },
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/test/{idAndKey.Id}/step"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that removes a specific test step from an Xray test issue.
        /// The method constructs the route using the test identifier, the step identifier, and a flag
        /// that determines whether the step should also be removed from Jira.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test issue identifier and its corresponding Jira issue key.</param>
        /// <param name="stepId">The identifier of the step that should be removed.</param>
        /// <param name="removeFromJira">Indicates whether the step should also be deleted from Jira's representation of the test.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that deletes the specified step from the Xray test issue.</returns>
        public static HttpCommand RemoveTestStep((string Id, string Key) idAndKey, string stepId, bool removeFromJira)
        {
            // Converts the boolean flag into the lowercase string format expected by the Xray API.
            const string Format = "/api/internal/test/{0}/step/{1}?removeFromJira={2}";
            var removeFromJiraValue = removeFromJira ? "true" : "false";

            // Returns a DELETE command with the formatted route and appropriate headers.
            return new HttpCommand
            {
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Delete,
                Route = string.Format(Format, idAndKey.Id, stepId, removeFromJiraValue)
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that updates the actual result of a specific step
        /// within an Xray test execution. The method prepares the request body using the provided
        /// actual result text and sends a POST request to the Xray endpoint responsible for step updates.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the execution identifier and its corresponding Jira issue key.</param>
        /// <param name="step">A tuple containing the step identifier and the actual result that should be recorded.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that updates the actual result of the specified test step.</returns>
        public static HttpCommand UpdateStepActual((string Id, string Key) idAndKey, (string Id, string Actual) step)
        {
            // The request body contains only the actual result value for the step update.
            var data = new
            {
                ActualResult = step.Actual
            };

            // Returns a POST command that includes the step update payload and the required issue key header.
            // The route targets the Xray endpoint that stores actual step results for the execution.
            return new HttpCommand
            {
                Data = data,
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/testRun/{idAndKey.Id}/step/{step.Id}/actualresult"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that updates the execution status of a specific step
        /// within an Xray test run. The method prepares the request body using the supplied status value
        /// and sends a POST request to the Xray endpoint responsible for updating step statuses.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the execution identifier and its corresponding Jira issue key.</param>
        /// <param name="step">A tuple containing the step identifier and the new status value that should be applied.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that updates the status of the specified test step.</returns>
        public static HttpCommand UpdateStepStatus((string Id, string Key) idAndKey, (string Id, string status) step)
        {
            // The request body contains the updated step status.
            // The status value is normalized to uppercase, matching Xray expectations.
            var data = new
            {
                Status = step.status.ToUpper()
            };

            // Returns a POST command targeting the Xray endpoint for step status updates.
            // The issue key is added to the headers for authentication and routing.
            return new HttpCommand
            {
                Data = data,
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/testRun/{idAndKey.Id}/step/{step.Id}/status"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that updates the overall status of an Xray test run.
        /// The method prepares the request body using the project identifier and the new status,
        /// then sends a POST request to the Xray endpoint responsible for updating test run status.
        /// </summary>
        /// <param name="idAndKey">A tuple containing the test run identifier and its corresponding Jira issue key.</param>
        /// <param name="projectId">The Jira project identifier associated with the test run.</param>
        /// <param name="status">The new status value that should be applied to the test run.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that updates the status of the specified test run.</returns>
        public static HttpCommand UpdateTestRunStatus((string Id, string Key) idAndKey, string projectId, string status)
        {
            // The request body contains the project identifier and the updated status.
            // The status is uppercased to match the expected format used by Xray.
            var data = new
            {
                ProjectId = projectId,
                Status = status.ToUpper()
            };

            // Returns a POST command that includes the update payload, 
            // uses the execution issue key for headers, 
            // and targets the Xray test run status endpoint.
            return new HttpCommand
            {
                Data = data,
                Headers = NewHeaders(issueKey: idAndKey.Key),
                Method = HttpMethod.Post,
                Route = $"/api/internal/testrun/{idAndKey.Id}/status"
            };
        }

        // Creates a new dictionary containing the Xray header required for issue-context requests.
        // The header value is set using the provided issue key and is configured to use 
        // case-insensitive key comparison.
        private static Dictionary<string, string> NewHeaders(string issueKey) => new(StringComparer.OrdinalIgnoreCase)
        {
            // Xray uses the X-acpt header to associate the request with the given issue key.
            ["X-acpt"] = issueKey
        };
        #endregion
    }
}
