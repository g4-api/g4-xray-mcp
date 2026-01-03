using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Framework;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mcp.Xray.Domain.Clients
{
    /// <summary>
    /// Provides a high-level wrapper around Xray Cloud operations. The client exposes
    /// methods for managing executions, test runs, steps, evidence, defects, and 
    /// relationships between Xray and Jira entities.
    /// </summary>
    public class XpandClient
    {
        #region *** Fields       ***
        // Bucket size used for batched operations, read from application settings.
        private static readonly int _bucketSize = AppSettings.JiraOptions.BucketSize;

        // Shared HttpClient instance from application settings.
        private static readonly HttpClient _httpClient = AppSettings.HttpClient;

        // The base URL for communicating with the Xray Cloud API. This value is taken
        // from the configured application settings and represents the root endpoint
        // for all Xray HTTP requests.
        public static readonly string _xpandBaseUrl = AppSettings.JiraOptions.XrayOptions.BaseUrl;

        // The logger instance assigned to this client. The logger is optional and is
        // used to record execution details and error information during Xray operations.
        private readonly ILogger _logger;

        // Controls the level of parallelism used by Xray batch operations such as bulk
        // test retrievals or multi-issue updates. The configuration is typically based
        // on the bucket size defined in the Jira and Xray options.
        private readonly ParallelOptions _paralleloptions;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new <see cref="XpandClient"/> instance using the provided Jira authentication model.
        /// This overload delegates to the full constructor and uses the default logger.
        /// </summary>
        /// <param name="authentication">The authentication model used to configure Jira access for this client.</param>
        public XpandClient(JiraAuthenticationModel authentication)
            : this(authentication, logger: default)
        { }

        /// <summary>
        /// Initializes a new <see cref="XpandClient"/> instance using the provided Jira authentication model
        /// and logger. The method configures parallel execution settings and creates the internal Jira client
        /// that is used by Xpand operations.
        /// </summary>
        /// <param name="authentication">The authentication model used to authenticate requests against Jira.</param>
        /// <param name="logger">The logger instance used for diagnostic output. A null value disables logging.</param>
        public XpandClient(JiraAuthenticationModel authentication, ILogger logger)
        {
            // Stores the logger for use throughout the client.
            _logger = logger;

            // Configures the parallel execution settings using the bucket size from application settings.
            _paralleloptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _bucketSize
            };

            // Creates the internal Jira client using the same authentication model and logger.
            JiraClient = new JiraClient(authentication, _logger);
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the internal JiraClient instance used for communicating with the Jira API.
        /// This client is initialized during construction and provides all Jira-related operations
        /// required by the Xpand client.
        /// </summary>
        public JiraClient JiraClient { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Adds a defect issue to a specific Xray test run. The method wraps the Xray command
        /// invocation and returns the parsed JSON response produced by the API.
        /// </summary>
        /// <param name="defect">A tuple containing the Jira issue identifier and key of the defect that should be linked to the test run. The id refers to the Jira internal issue id of the defect.</param>
        /// <param name="testRunId">The Xray internal identifier of the test run to which the defect should be added.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after the defect is associated with the specified test run.</returns>
        public JsonElement AddDefectToTestRun((string Id, string Key) defect, string testRunId)
        {
            // Sends the Xray command that links the defect to the given test run
            // and returns the parsed JSON root element.
            return XpandCommands
                .AddDefectToTestRun(defect, testRunId)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Adds an existing Xray test execution to a specific Xray test plan.
        /// The method sends the appropriate Xray command and returns the parsed JSON response.
        /// </summary>
        /// <param name="plan">A tuple containing the test plan identifier and its corresponding Jira issue key. The Id refers to the Jira internal issue id of the test plan.</param>
        /// <param name="executionId">The Xray test execution identifier that should be added to the test plan.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after linking the execution to the test plan.</returns>
        public JsonElement AddExecutionToPlan((string Id, string Key) plan, string executionId)
        {
            // Sends the Xray linking command and returns the parsed JSON response.
            return XpandCommands
                .AddExecutionToPlan(plan, executionId)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Adds a precondition to a specific Xray test. The method resolves both the
        /// precondition issue and the test issue from Jira, validates them against the
        /// provided identifiers or keys, and then sends the Xray command that performs
        /// the link operation.
        /// </summary>
        /// <param name="preconditionIdOrKey">The Jira issue identifier or key representing the precondition that should be added.</param>
        /// <param name="testIdOrKey">The Jira issue identifier or key representing the test that will receive the precondition.</param>
        /// <returns>A <see cref="JsonElement"/> containing the Xray response after the precondition is linked to the test. An empty object is returned when no result is available.</returns>
        public JsonElement AddPrecondition(string preconditionIdOrKey, string testIdOrKey)
        {
            // Normalizes values for consistent case-insensitive matching.
            preconditionIdOrKey = preconditionIdOrKey.ToUpper();
            testIdOrKey = testIdOrKey.ToUpper();

            // Loads both Jira issues in a single request.
            var issues = JiraClient.GetIssues(preconditionIdOrKey, testIdOrKey);

            // Resolves the precondition issue.
            var precondition = issues.FirstOrDefault(i => AssertRequestEntity(i, preconditionIdOrKey));

            // Resolves the test issue.
            var onTest = issues.FirstOrDefault(i => AssertRequestEntity(i, testIdOrKey));

            // Extracts the id and key of the test issue.
            var id = $"{onTest.GetProperty("id")}";
            var key = $"{onTest.GetProperty("key")}";

            // Extracts the precondition id to send to Xray.
            var preconditionId = $"{precondition.GetProperty("id")}";

            // Sends the Xray link command and converts the response to JSON.
            return XpandCommands
                .AddPrecondition((id, key), preconditionId)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Adds multiple test issues to a specific Xray test execution. The method splits
        /// the incoming test identifiers into smaller batches to improve performance and
        /// reduce payload size, then processes each batch in parallel.
        /// </summary>
        /// <param name="executionIdOrKey">The Jira issue identifier or key of the test execution to which the tests will be added.</param>
        /// <param name="idsOrKeys">One or more Jira issue identifiers or keys representing the tests to add.</param>
        public void AddTestsToExecution(string executionIdOrKey, params string[] idsOrKeys)
        {
            // Adds one or more tests to a specific Xray test execution. The method resolves the
            // execution issue from Jira, collects the internal identifiers of the supplied test keys,
            // and sends the Xray command that performs the association.
            static void AddTests(JiraClient jiraClient, string executionIdOrKey, params string[] idsOrKeys)
            {
                // Loads the execution issue to extract its internal id and key.
                var execution = jiraClient.GetIssue(executionIdOrKey);
                var id = $"{execution.GetProperty("id")}";
                var key = $"{execution.GetProperty("key")}";

                // Aborts when the execution could not be resolved.
                if (string.IsNullOrEmpty(id))
                {
                    return;
                }

                // Retrieves the Jira issues matching the provided test keys
                // and extracts their internal numeric ids.
                var testCases = jiraClient
                    .GetIssues(jql: $"key in ({string.Join(",", idsOrKeys)})")
                    .Select(i => $"{i.GetProperty("id")}")
                    .Where(i => i != default)
                    .Distinct()
                    .ToArray();

                // Sends the Xray command to add the resolved test ids to the execution.
                XpandCommands
                    .AddTestToExecution((id, key), testsIds: testCases)
                    .Send(jiraClient.Invoker);
            }

            // Splits the incoming test IDs into batches of 49 elements each.
            // Xray and Jira endpoints often enforce limits on array sizes.
            var batches = idsOrKeys.Split(49);

            // Processes each batch in parallel using the configured parallel options.
            Parallel.ForEach(batches, _paralleloptions, batch =>
                AddTests(JiraClient, executionIdOrKey, idsOrKeys: [.. batch])
            );
        }

        /// <summary>
        /// Adds one or more Xray test issues to an existing Xray test set.
        /// The method forwards the request to the Xray command layer and returns
        /// the parsed JSON response produced by the API.
        /// </summary>
        /// <param name="testSet">A tuple containing the test set identifier and its corresponding Jira issue key. The id is the Jira internal issue id of the test set.</param>
        /// <param name="testIds">One or more Jira issue identifiers representing the tests that should be added to the specified test set.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after adding the tests to the test set.</returns>
        public JsonElement AddTestsToSet((string Id, string Key) testSet, params string[] testIds)
        {
            // Sends the Xray command to add the tests to the test set
            // and returns the parsed JSON root element.
            return XpandCommands
                .AddTestsToSet(testSet, testIds)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Retrieves the detailed Xray execution information for a specific test within a given execution.
        /// The method resolves both entities from Jira, validates them, loads the execution run data
        /// from Xray, and returns the testRun JSON block.
        /// </summary>
        /// <param name="executionIdOrKey">The Jira issue identifier or key representing the test execution.</param>
        /// <param name="testIdOrKey">The Jira issue identifier or key representing the test inside the execution.</param>
        /// <returns>A <see cref="JsonElement"/> containing the Xray testRun details. An empty JSON object is returned when the entities cannot be resolved.</returns>
        public JsonElement GetExecutionDetails(string executionIdOrKey, string testIdOrKey)
        {
            // Normalizes both inputs to uppercase for consistent comparison.
            executionIdOrKey = executionIdOrKey.ToUpper();
            testIdOrKey = testIdOrKey.ToUpper();

            // Loads both Jira issues (execution and test) in a single request.
            var issues = JiraClient.GetIssues(executionIdOrKey, testIdOrKey);

            // Retrieves the execution and test entities using ID or key matching.
            var onExecution = issues.FirstOrDefault(i => AssertRequestEntity(i, executionIdOrKey));
            var onTest = issues.FirstOrDefault(i => AssertRequestEntity(i, testIdOrKey));

            // Extracts the keys needed for the Xray API requests.
            var executionKey = $"{onExecution.GetProperty("key")}";
            var testKey = $"{onTest.GetProperty("key")}";

            // Aborts early when either key is missing or invalid.
            if (string.IsNullOrEmpty(executionKey) || string.IsNullOrEmpty(testKey))
            {
                return default;
            }

            // Loads the execution run details from Xray.
            var response = XpandCommands
                .GetLoadTestRun(executionKey, testKey)
                .Send(JiraClient.Invoker);

            // Returns an empty JSON object when no response body was received.
            return string.IsNullOrEmpty(response)
                ? JsonElement.Parse("{}")
                : response.ConvertToJsonDocument().RootElement.GetProperty("testRun");
        }

        /// <summary>
        /// Retrieves the Xray test plans that are linked to a specific test issue.
        /// The method loads the inbound test plan links from Xray and returns their identifiers.
        /// </summary>
        /// <param name="id">The numeric Jira issue identifier of the test.</param>
        /// <param name="key">The Jira issue key of the test.</param>
        /// <returns>An enumerable sequence of test plan identifiers associated with the specified test. Empty results are returned when no test plans are linked.</returns>
        public IEnumerable<string> GetPlansByTest(string id, string key)
        {
            // Queries Xray for all test plans associated with the specified test issue.
            var testPlans = XpandCommands
                .GetPlansByTest((id, key))
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement
                .EnumerateArray();

            // Extracts the id of each returned test plan and filters out empty values.
            return testPlans
                .Select(i => $"{i.GetProperty("id")}")
                .Where(i => !string.IsNullOrEmpty(i));
        }

        /// <summary>
        /// Retrieves the Xray preconditions that are linked to a specific test issue.
        /// The method loads all inbound precondition links from Xray and returns their identifiers.
        /// </summary>
        /// <param name="id">The numeric Jira issue identifier of the test.</param>
        /// <param name="key">The Jira issue key of the test.</param>
        /// <returns>An enumerable sequence of precondition identifiers associated with the specified test. Empty results are returned when no preconditions are linked.</returns>
        public IEnumerable<string> GetPreconditionsByTest(string id, string key)
        {
            // Queries Xray for all preconditions associated with the given test issue.
            var preconditions = XpandCommands
                .GetPreconditionsByTest((id, key))
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement
                .EnumerateArray();

            // Extracts each precondition id and filters out empty values.
            return preconditions
                .Select(i => $"{i.GetProperty("id")}")
                .Where(i => !string.IsNullOrEmpty(i));
        }

        /// <summary>
        /// Retrieves all Xray test runs that belong to a specific test execution.
        /// The method sends the appropriate Xray command, converts the response,
        /// and returns the root JSON element containing the run details.
        /// </summary>
        /// <param name="id">The numeric Jira issue identifier of the test execution.</param>
        /// <param name="key">The Jira issue key of the test execution.</param>
        /// <returns>A <see cref="JsonElement"/> representing the collection of test runs associated with the specified execution.</returns>
        public JsonElement GetRunsByExecution(string id, string key)
        {
            // Sends the Xray command, parses the response body, and returns the root element.
            return XpandCommands
                .GetRunsByExecution((id, key))
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Retrieves the Xray test sets that are linked to a specific test issue.
        /// The method loads the inbound test set links from Xray and returns their identifiers.
        /// </summary>
        /// <param name="id">The numeric Jira issue identifier of the test.</param>
        /// <param name="key">The Jira issue key of the test.</param>
        /// <returns>An enumerable sequence of test set identifiers associated with the specified test. Empty results are returned when no test sets are linked.</returns>
        public IEnumerable<string> GetSetsByTest(string id, string key)
        {
            // Queries Xray for all test sets associated with the given test issue.
            var testSets = XpandCommands
                .GetSetsByTest((id, key))
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement
                .EnumerateArray();

            // Extracts the id of each returned test set and filters out empty values.
            return testSets
                .Select(i => $"{i.GetProperty("id")}")
                .Where(i => !string.IsNullOrEmpty(i));
        }

        /// <summary>
        /// Retrieves a single expanded test case by loading the Jira issue and enriching it
        /// with its Xray step information. The method delegates the work to the internal
        /// bulk expansion method and returns the first expanded result.
        /// </summary>
        /// <param name="idOrKey">The Jira issue identifier or key representing the test case to load.</param>
        /// <returns>A <see cref="JsonElement"/> containing the expanded test case. If the issue does not exist or cannot be expanded, an empty result is returned.</returns>
        public JsonElement GetTestCase(string idOrKey)
        {
            // Calls the bulk test case loader and retrieves the first expanded item.
            return GetTestCases(JiraClient, _paralleloptions, idOrKey).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves a collection of expanded test cases by loading each Jira issue
        /// and enriching it with its Xray step information. The method forwards
        /// the request to the internal expansion routine and returns the results
        /// as an enumerable sequence.
        /// </summary>
        /// <param name="idsOrKeys">One or more Jira issue identifiers or keys representing the test cases to load.</param>
        /// <returns>An enumerable sequence of <see cref="JsonElement"/> values, each containing the expanded structure of a test case.</returns>
        public IEnumerable<JsonElement> GetTestCases(params string[] idsOrKeys)
        {
            // Delegates to the bulk loader which performs the expansion in parallel.
            return GetTestCases(JiraClient, _paralleloptions, idsOrKeys);
        }

        /// <summary>
        /// Retrieves all test cases that belong to one or more Xray test executions.
        /// The method resolves each execution issue, loads its test runs from Xray,
        /// collects the related test case identifiers, and expands them into full test case representations.
        /// </summary>
        /// <param name="idsOrKeys">One or more Jira issue identifiers or keys representing the test executions to query.</param>
        /// <returns>An enumerable sequence of <see cref="JsonElement"/> values that contain the expanded test cases associated with the specified executions.</returns>
        public IEnumerable<JsonElement> GetTestsByExecution(params string[] idsOrKeys)
        {
            // Shared collection to accumulate test case identifiers from all executions.
            var testCases = new ConcurrentBag<string>();

            // For each execution key or id, resolve the execution issue, load its runs,
            // and collect the associated test issue identifiers.
            Parallel.ForEach(idsOrKeys, _paralleloptions, idOrKey =>
            {
                // Loads the execution issue from Jira.
                var execution = JiraClient.GetIssue(idOrKey);
                var id = execution.GetProperty("id").GetString();
                var key = execution.GetProperty("key").GetString();

                // Loads the test runs for this execution from Xray.
                var runs = XpandCommands
                    .GetRunsByExecution((id, key))
                    .Send(JiraClient.Invoker)
                    .ConvertToJsonDocument()
                    .RootElement
                    .EnumerateArray();

                // Extracts the test issue identifiers from each run and adds them to the shared bag.
                var range = runs.Select(i => $"{i.GetProperty("testIssueId")}");
                testCases.AddRange(range);
            });

            // Expands all collected test case identifiers into full test case structures.
            return GetTestCases(idsOrKeys: [.. testCases]);
        }

        /// <summary>
        /// Retrieves all test cases that belong to one or more Xray test sets.
        /// The method resolves the test set issues from Jira, loads their associated
        /// test links from Xray, collects all referenced test case identifiers, and
        /// finally expands them into full test case representations.
        /// </summary>
        /// <param name="idsOrKeys">One or more Jira issue identifiers or keys representing the test sets to query.</param>
        /// <returns>An enumerable sequence of <see cref="JsonElement"/> objects containing the expanded test cases associated with the specified test sets.</returns>
        public IEnumerable<JsonElement> GetTestsBySets(params string[] idsOrKeys)
        {
            // Queries Jira for the test set issues by building a JQL expression.
            var testSets = JiraCommands
                .FindIssues(jql: $"key in ({string.Join(",", idsOrKeys)})")
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement
                .GetProperty("issues")
                .EnumerateArray();

            // Returns an empty result when no test sets were found.
            if (!testSets.Any())
            {
                return [];
            }

            // Prepares the Xray commands that will load the tests inside each test set.
            var commands = testSets.Select(i =>
            {
                var id = i.GetProperty("id").GetString();
                var key = i.GetProperty("key").GetString();
                return XpandCommands.GetTestsBySet((id, key));
            });

            // Collects all test case identifiers from the loaded test sets.
            var testCases = new ConcurrentBag<string>();

            // Parallel extraction of test case identifiers.
            Parallel.ForEach(commands, _paralleloptions, command =>
            {
                var ids = command
                    .Send(JiraClient.Invoker)
                    .ConvertToJsonDocument()
                    .RootElement
                    .EnumerateArray()
                    .Select(i => $"{i.GetProperty("id")}");

                testCases.AddRange(ids);
            });

            // Expands all collected test case identifiers into full test case structures.
            return GetTestCases(JiraClient, _paralleloptions, idsOrKeys);
        }

        /// <summary>
        /// Retrieves all test cases that belong to one or more Xray test plans.
        /// The method resolves the test plan issues from Jira, loads the associated tests
        /// from Xray, collects their identifiers, and expands them into full test case representations.
        /// </summary>
        /// <param name="idsOrKeys">One or more Jira issue identifiers or keys representing the test plans to query.</param>
        /// <returns>An enumerable sequence of <see cref="JsonElement"/> values containing the expanded test cases that are associated with the specified test plans.</returns>
        public IEnumerable<JsonElement> GetTestsByPlans(params string[] idsOrKeys)
        {
            // Queries Jira for the test plan issues by building a JQL expression.
            var testPlans = JiraCommands
                .FindIssues(jql: $"key in ({string.Join(",", idsOrKeys)})")
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement
                .GetProperty("issues")
                .EnumerateArray();

            // Returns an empty result when no test plans were found.
            if (!testPlans.Any())
            {
                return [];
            }

            // Prepares the Xray commands that will load the tests contained in each test plan.
            var commands = testPlans.Select(i =>
            {
                // Extracts the ID and key of the test plan issue.
                var id = i.GetProperty("id").GetString();
                var key = i.GetProperty("key").GetString();

                // Creates the command to load tests by plan.
                return XpandCommands.GetTestsByPlan((id, key));
            });

            // Collects all test case identifiers from the loaded test plans.
            var testCases = new ConcurrentBag<string>();

            // Parallel extraction of test case identifiers.
            Parallel.ForEach(commands, _paralleloptions, command =>
            {
                var ids = command
                    .Send(JiraClient.Invoker)
                    .ConvertToJsonDocument()
                    .RootElement
                    .EnumerateArray()
                    .Select(i => $"{i.GetProperty("issueId")}");

                testCases.AddRange(ids);
            });


            // Expands all collected test case identifiers into full test case structures.
            // Distinct is applied to avoid processing the same test multiple times.
            var testCaseIds = testCases.Distinct().ToArray();

            // Delegates to the bulk loader which performs the expansion in parallel.
            return GetTestCases(JiraClient, _paralleloptions, testCaseIds);
        }

        /// <summary>
        /// Adds a comment to a specific Xray test execution. The method forwards the request
        /// to the Xray command layer and returns the parsed JSON response from the API.
        /// </summary>
        /// <param name="execution">A tuple containing the Xray test run identifier and the Jira issue key of the execution that should receive the comment. The Id is the Xray internal test run id, not a Jira numeric id.</param>
        /// <param name="comment">The comment text that should be added to the execution.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after adding the comment to the execution.</returns>
        public JsonElement NewCommentOnExecution((string Id, string Key) execution, string comment)
        {
            // Sends the Xray command to add the comment to the execution
            // and returns the parsed JSON root element.
            return XpandCommands
                .NewCommentOnExecution(execution, comment)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Uploads a file as evidence to an Xray test run. The file is first uploaded as an attachment
        /// to Xray, then the returned attachment descriptor is submitted as evidence. Evidence may be
        /// associated with the entire test run or with a specific test step.
        /// </summary>
        /// <param name="testRun">A tuple containing the internal Xray test run id and the Jira issue key that owns the test run. The id represents the Xray-side identifier, and the key represents the Jira issue key.</param>
        /// <param name="testStep">The internal Xray step identifier. An empty value attaches the evidence at the test run level.</param>
        /// <param name="file">The full file path of the evidence file to upload.</param>
        public void NewEvidence((string Id, string Key) testRun, string testStep, string file)
        {
            // Creates a new multipart HTTP request for uploading an attachment to an Xray test run.
            // The method prepares the request message with the correct headers, authentication,
            // Atlassian token configuration, and file content.
            static HttpRequestMessage NewAttachmentRequest(JiraClient jiraClient, string token, string testRun, string file)
            {
                // Builds the target Xray endpoint for the attachment request.
                var urlPath = $"{_xpandBaseUrl}/api/internal/attachments?testRunId={testRun}";
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, urlPath);

                // Disables the Expect: 100-continue handshake to reduce latency.
                requestMessage.Headers.ExpectContinue = false;

                // Applies Jira authentication to the request.
                requestMessage.Headers.Authorization = jiraClient.Authentication.NewAuthenticationHeader();

                // Required to bypass Atlassian’s built-in attachment protections.
                requestMessage.Headers.Add("X-Atlassian-Token", "no-check");

                // Creates the multipart container used to transport the file.
                var multiPartContent = new MultipartFormDataContent($"----{Guid.NewGuid()}");

                // Loads the file from disk and adds it as binary content.
                var fileInfo = new FileInfo(file);
                var fileContents = File.ReadAllBytes(fileInfo.FullName);
                var byteArrayContent = new ByteArrayContent(fileContents);

                // Marks the file content as a generic binary stream.
                byteArrayContent.Headers.Add("Content-Type", "application/octet-stream");

                // Adds the file into the multipart form under the field name "attachment".
                multiPartContent.Add(byteArrayContent, "attachment", fileInfo.Name);

                // Assigns the multipart content to the HTTP request.
                requestMessage.Content = multiPartContent;

                // Includes the Xray JWT token in the request headers.
                requestMessage.Headers.Add("X-acpt", token);

                // Returns the fully constructed HTTP request message.
                return requestMessage;
            }

            // Creates a new HTTP request used for submitting evidence to an Xray test run or
            // a specific step within the test run. The request is configured with authentication,
            // Xray headers, and a JSON request body.
            static HttpRequestMessage NewEvidenceRequest(
                JiraClient jiraClient,
                string token,
                string testRun,
                string testStep,
                string requestBody)
            {
                // Determines the correct evidence endpoint.
                // When no test step is provided, evidence is submitted at the test run level.
                var endpoint = string.IsNullOrEmpty(testStep)
                    ? $"{_xpandBaseUrl}/api/internal/testrun/{testRun}/evidence"
                    : $"{_xpandBaseUrl}/api/internal/testrun/{testRun}/step/{testStep}/evidence";

                // Prepares the JSON content using UTF8 encoding.
                var stringContent = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // Creates the HTTP request message with the POST method and the selected URI.
                var request = new HttpRequestMessage
                {
                    Content = stringContent,
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(endpoint)
                };

                // Disables the Expect: 100-continue handshake to improve request performance.
                request.Headers.ExpectContinue = false;

                // Applies basic Jira authentication.
                request.Headers.Authorization = jiraClient.Authentication.NewAuthenticationHeader();

                // Required to allow attachments and evidence bypassing built-in Atlassian validation rules.
                request.Headers.Add("X-Atlassian-Token", "no-check");

                // Includes the Xray JWT token representing the execution/test context.
                request.Headers.Add("X-acpt", token);

                return request;
            }

            // Requests the Xray JWT token associated with the supplied issue key.
            var token = JiraClient.Authentication.GetJwt(issueKey: testRun.Key).Result;

            // Builds the request that uploads the file as an attachment to the test run.
            var attachmentRequest = NewAttachmentRequest(JiraClient, token, testRun.Id, file);

            // Sends the attachment upload request.
            var response = _httpClient.SendAsync(attachmentRequest).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("Failed to upload attachment for evidence.");
                return;
            }

            // Reads the JSON descriptor returned by Xray for the uploaded attachment.
            var requestBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            // Builds the request that submits the attachment as evidence.
            var evidenceRequest = NewEvidenceRequest(JiraClient, token, testRun.Id, testStep, requestBody);

            // Sends the evidence submission request.
            response = _httpClient.SendAsync(evidenceRequest).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("Failed to submit evidence to Xray.");
            }
        }

        /// <summary>
        /// Creates a new step in an Xray test issue. The method forwards the supplied action,
        /// expected result, and insertion index to the Xray command layer and returns the parsed
        /// JSON response produced by the API.
        /// </summary>
        /// <param name="test">A tuple containing the Jira internal test issue id and its key.</param>
        /// <param name="action">The action text that defines what the step performs.</param>
        /// <param name="result">The expected result text associated with the step.</param>
        /// <param name="index">The zero-based position where the new step should be inserted.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after the step is created.</returns>
        public JsonElement NewTestStep((string Id, string Key) test, string action, string result, int index)
        {
            // Sends the creation command to Xray and returns the parsed JSON root element.
            return XpandCommands
                .NewTestStep(test, action, result, index)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Removes a step from an Xray test issue without deleting it from Jira. 
        /// This overload always uses a non-destructive removal and delegates to the full method.
        /// </summary>
        /// <param name="test">A tuple containing the Jira internal test issue id and its key.</param>
        /// <param name="stepId">The Xray internal identifier of the step that should be removed.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after removing the step from the test.</returns>
        public JsonElement RemoveTestStep((string Id, string Key) test, string stepId)
        {
            // Delegates to the full overload using removeFromJira = false.
            return RemoveTestStep(test, stepId, removeFromJira: false);
        }

        /// <summary>
        /// Removes a step from an Xray test issue and optionally deletes it from Jira.
        /// The removeFromJira flag determines whether Jira’s step representation is also removed.
        /// </summary>
        /// <param name="test">A tuple containing the Jira internal test issue id and its key.</param>
        /// <param name="stepId">The Xray internal identifier of the step that should be removed.</param>
        /// <param name="removeFromJira">Indicates whether the step should also be deleted from Jira's representation.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after removing or deleting the step.</returns>
        public JsonElement RemoveTestStep((string Id, string Key) test, string stepId, bool removeFromJira)
        {
            // Sends the Xray removal command with the specified deletion behavior.
            return XpandCommands
                .RemoveTestStep(test, stepId, removeFromJira)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Updates the actual result text of a specific step inside an Xray test run.
        /// The method wraps the Xray command call and returns the parsed JSON response.
        /// </summary>
        /// <param name="execution">A tuple containing the Xray test run identifier and the Jira issue key of the execution that contains the step. The Id refers to the Xray internal test run id.</param>
        /// <param name="step">A tuple containing the Xray step identifier and the actual result text that should be recorded.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after updating the step's actual result.</returns>
        public JsonElement UpdateStepActual((string Id, string Key) execution, (string Id, string Actual) step)
        {
            // Sends the update command to Xray and returns the parsed JSON root element.
            return XpandCommands
                .UpdateStepActual(execution, step)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Updates the execution status of a specific step within an Xray test run. 
        /// The method wraps the Xray command call and returns the parsed JSON response.
        /// </summary>
        /// <param name="execution">A tuple containing the Xray test run identifier and the Jira issue key of the execution that owns the step. The Id is the Xray internal test run id, not a Jira numeric id.</param>
        /// <param name="step">A tuple containing the Xray step identifier and the new status value that should be applied.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after updating the step status.</returns>
        public JsonElement UpdateStepStatus((string Id, string Key) execution, (string Id, string Status) step)
        {
            // Sends the update step status command to Xray and returns the parsed JSON root element.
            return XpandCommands
                .UpdateStepStatus(execution, step)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Updates the overall status of a specific Xray test run. The method forwards the
        /// request to the Xray command layer using the provided test run identifier, project
        /// identifier, and new status, then returns the parsed JSON response.
        /// </summary>
        /// <param name="testRun">A tuple containing the Xray test run identifier and the Jira issue key of the execution that owns this test run. The Id refers to the Xray internal test run id.</param>
        /// <param name="projectId">The Jira project identifier associated with the test run.</param>
        /// <param name="status">The new status value that should be applied to the test run.</param>
        /// <returns>A <see cref="JsonElement"/> representing the response returned by Xray after the test run status is updated.</returns>
        public JsonElement UpdateTestRunStatus((string Id, string Key) testRun, string projectId, string status)
        {
            // Sends the update command to Xray and returns the parsed JSON root element.
            return XpandCommands
                .UpdateTestRunStatus(testRun, projectId, status)
                .Send(JiraClient.Invoker)
                .ConvertToJsonDocument()
                .RootElement;
        }

        // Evaluates whether the supplied JSON entity matches the specified Jira identifier or key.
        // The method safely checks both the <c>id</c> and <c>key</c> fields and returns true when
        // either field equals the provided value.
        private static bool AssertRequestEntity(JsonElement entity, string idOrKey)
        {
            try
            {
                // Retrieves the id and key from the entity and compares them to the supplied search value.
                var isId = $"{entity.GetProperty("id")}" == idOrKey;
                var isKey = $"{entity.GetProperty("key")}" == idOrKey;

                return isId || isKey;
            }
            catch (Exception)
            {
                // Returns false when the expected properties are missing or inaccessible.
                return false;
            }
        }

        // Retrieves a collection of expanded test cases by loading each Jira test issue
        // and enriching it with its corresponding Xray steps. The method performs the
        // expansion in parallel and returns a concurrent collection of fully detailed
        // test case representations.
        private static ConcurrentBag<JsonElement> GetTestCases(
            JiraClient jiraClient, 
            ParallelOptions parallelOptions,
            params string[] idsOrKeys)
        {
            // Retrieves the detailed representation of a test case by loading its Xray steps
            // and merging them into the original Jira test case JSON. The method returns a new
            // JsonElement that contains the full expanded structure.
            static JsonElement GetTestCase(JiraCommandInvoker invoker, JsonElement testCase)
            {
                // Extracts the Jira issue ID and key from the provided test case JSON.
                var id = testCase.GetProperty("id").GetString();
                var key = testCase.GetProperty("key").GetString();

                // Loads the Xray steps for the test case by invoking the relevant command.
                var response = XpandCommands
                    .GetSteps((id, key))
                    .Send(invoker)
                    .ConvertToJsonDocument()
                    .RootElement;

                // Converts the existing test case JSON into a dictionary so that new fields can be appended.
                var testCaseObject = testCase
                    .EnumerateObject()
                    .ToDictionary(i => i.Name, i => i.Value);

                // Adds the loaded steps into the final output.
                testCaseObject["steps"] = response.GetProperty("steps");

                // Serializes the dictionary back into a JSON string so it can be parsed into a JsonElement.
                var json = JsonSerializer.Serialize(testCaseObject);

                // Returns the merged JSON result to the caller.
                return JsonElement.Parse(json);
            }

            // Retrieves the base Jira issue data for the requested IDs or keys.
            var testCases = jiraClient.GetIssues(idsOrKeys);

            // If no test cases were found, an empty collection is returned.
            if (!testCases.Any())
            {
                return [];
            }

            var testCasesResult = new ConcurrentBag<JsonElement>();

            // Parallel expansion of each test case using the configured degree of concurrency.
            Parallel.ForEach(testCases, parallelOptions, testCase =>
            {
                var expandedTestCase = GetTestCase(jiraClient.Invoker, testCase);
                testCasesResult.Add(expandedTestCase);
            });

            // Returns the fully expanded test case collection.
            return testCasesResult;
        }
        #endregion
    }
}
