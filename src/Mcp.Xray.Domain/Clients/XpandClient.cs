using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Framework;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

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
    public class XpandClient
    {
        public static readonly string XpandBaseUrl = AppSettings.JiraOptions.XrayCloudOptions.BaseUrl;

        private readonly ILogger _logger;
        private readonly ParallelOptions _paralleloptions;

        #region *** Constructors       ***
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
                MaxDegreeOfParallelism = AppSettings.JiraOptions.BucketSize
            };

            // Creates the internal Jira client using the same authentication model and logger.
            JiraClient = new JiraClient(authentication, _logger);
        }
        #endregion

        #region *** Properties         ***
        /// <summary>
        /// Gets the internal JiraClient instance used for communicating with the Jira API.
        /// This client is initialized during construction and provides all Jira-related operations
        /// required by the Xpand client.
        /// </summary>
        public JiraClient JiraClient { get; }
        #endregion

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

            var testCases = new ConcurrentBag<string>();
            var token = "id";

            // Parallel extraction of test case identifiers.
            Parallel.ForEach(commands, _paralleloptions, command =>
            {
                var ids = command
                    .Send(JiraClient.Invoker)
                    .ConvertToJsonDocument()
                    .RootElement
                    .EnumerateArray()
                    .Select(i => $"{i.GetProperty(token)}");

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
                var id = i.GetProperty("id").GetString();
                var key = i.GetProperty("key").GetString();
                return XpandCommands.GetTestsByPlan((id, key));
            });

            var testCases = new ConcurrentBag<string>();
            var token = "issueId";


            // Parallel extraction of test case identifiers.
            Parallel.ForEach(commands, _paralleloptions, command =>
            {
                var ids = command
                    .Send(JiraClient.Invoker)
                    .ConvertToJsonDocument()
                    .RootElement
                    .EnumerateArray()
                    .Select(i => $"{i.GetProperty(token)}");

                testCases.AddRange(ids);
            });


            // Expands all collected test case identifiers into full test case structures.
            // Distinct is applied to avoid processing the same test multiple times.
            var testCaseIds = testCases.Distinct().ToArray();
            return GetTestCases(JiraClient, _paralleloptions, testCaseIds);
        }






















        public IEnumerable<JsonElement> GetTestsByExecution(params string[] idsOrKeys)
        {
            // setup
            var testCases = new ConcurrentBag<string>();

            // get
            Parallel.ForEach(idsOrKeys, _paralleloptions, idOrKey =>
            {
                var execution = JiraClient.GetIssue(idOrKey);
                var id = execution.GetProperty("id").GetString();
                var key = execution.GetProperty("key").GetString();

                var runs = XpandCommands
                    .GetRunsByExecution((id, key))
                    .Send(_invoker)
                    .ConvertToJsonDocument()
                    .RootElement
                    .EnumerateArray()
                    .Select(i => i);

                var range = runs.Select(i => i.GetProperty("testIssueId").GetString());
                testCases.AddRange(range);
            });

            // get
            return GetTestCases(idsOrKeys: testCases);
        }

        public IEnumerable<string> GetSetsByTest(string id, string key)
        {
            var testSets = XpandCommands
                .GetSetsByTest((id, key))
                .Send(_invoker)
                .ConvertToJsonDocument()
                .RootElement
                .EnumerateArray();

            return testSets
                .Select(i => i.GetProperty("id").GetString())
                .Where(i => !string.IsNullOrEmpty(i));
        }

        public IEnumerable<string> GetPlansByTest(string id, string key)
        {
            var testPlans = XpandCommands.GetPlansByTest((id, key)).Send(_invoker).ConvertToJsonDocument().RootElement.EnumerateArray();

            return testPlans.Select(i => i.GetProperty("id").GetString()).Where(i => !string.IsNullOrEmpty( i));
        }

        public IEnumerable<string> GetPreconditionsByTest(string id, string key)
        {
            var preconditions = XpandCommands.GetPreconditionsByTest((id, key)).Send(_invoker).ConvertToJsonDocument().RootElement.EnumerateArray();

            return preconditions.Select(i => i.GetProperty("id").GetString()).Where(i => !string.IsNullOrEmpty(i));
        }

        public JsonElement GetExecutionDetails(string execution, string test)
        {
            static bool AssertTestEntity(JsonElement input, string value)
            {
                try
                {
                    var isId = $"{input.GetProperty("id")}" == value;
                    var isKey = $"{input.GetProperty("key")}" == value;

                    return isId || isKey;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            execution = execution.ToUpper();
            test = test.ToUpper();

            var issues = JiraClient.GetIssues(new[] { execution, test });

            var onExecution = issues.FirstOrDefault(i => AssertTestEntity(input: i, value: execution));
            var onTest = issues.FirstOrDefault(i => AssertTestEntity(input: i, value: test));

            var executionKey = $"{onExecution.GetProperty("key")}";
            var testKey = $"{onTest.GetProperty("key")}";

            if(string.IsNullOrEmpty(executionKey) || string.IsNullOrEmpty(testKey))
            {
                // Critical;
                return default;
            }

            var response = XpandCommands
                .GetLoadTestRun(executionKey, testKey)
                .Send(_invoker);

            return string.IsNullOrEmpty(response)
                ? JsonElement.Parse("{}")
                : response.ConvertToJsonDocument().RootElement.GetProperty("testRun");
        }

        public JToken GetRunsByExecution((string id, string key) idAndKey)
        {
            return XpandCommands.GetRunsByExecution(idAndKey).Send(_invoker);
        }

        public void AddPrecondition(string precondition, string test)
        {
            // setup
            precondition = precondition.ToUpper();
            test = test.ToUpper();

            // get
            var issues = JiraClient.GetIssues(new[] { precondition, test });

            // setup
            var onPrecondition = issues.FirstOrDefault(i => $"{i.GetProperty("id")}" == precondition || $"{i.GetProperty("key")}" == precondition);
            var onTest = issues.FirstOrDefault(i => $"{i.GetProperty("id")}" == test || $"{i.GetProperty("key")}" == test);

            // setup
            var id = $"{onTest.GetProperty("id")}";
            var key = $"{onTest.GetProperty("key")}";
            //var preconditions = onPrecondition.SelectTokens("id").Cast<string>();

            // set
            XpandCommands.AddPrecondition((id, key), idsPrecondition: []).Send(_invoker);
        }

        #region *** Put: Execution     ***
        /// <summary>
        /// Adds tests to a test execution run issue with default status.
        /// </summary>
        /// <param name="idOrKeyExecution">The ID or key of the test execution issue.</param>
        /// <param name="idsOrKeysTest">A collection of test issue ID or key.</param>
        public void AddTestsToExecution(string idOrKeyExecution, params string[] idsOrKeysTest)
        {
            // setup
            var batches = idsOrKeysTest.Split(49);

            // put
            Parallel.ForEach(batches, _paralleloptions, batch => AddTests(idOrKeyExecution, idsOrKeysTest: batch));
        }

        // add tests bucket to test execution
        private void AddTests(string idOrKeyExecution, IEnumerable<string> idsOrKeysTest)
        {
            // setup: execution
            var onExecution = JiraClient.GetIssue(idOrKeyExecution);
            var id = $"{onExecution.GetProperty("id")}";
            var key = $"{onExecution.GetProperty("key")}";

            // exit conditions
            if (string.IsNullOrEmpty(id))
            {
                _logger?.LogCritical("");
                return;
            }

            // setup: tests to add
            var testCases = JiraClient.GetIssues(jql: $"key in ({string.Join(",", idsOrKeysTest)})")
                .Select(i => $"{i.GetProperty("id")}")
                .Where(i => i != default)
                .Distinct()
                .ToArray();

            // send
            XpandCommands.AddTestToExecution((id, key), testsIds: testCases).Send(_invoker);
        }

        /// <summary>
        /// Updates a test run result.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test execution issue.</param>
        /// <param name="idProject">The ID of the project.</param>
        /// <param name="run">The execution details ID.</param>
        /// <param name="status">The status to update.</param>
        public JToken UpdateTestRunStatus(
            (string id, string key) idAndKey,
            string idProject,
            string run,
            string status)
        {
            return XpandCommands.UpdateTestRunStatus(idAndKey, idProject, run, status).Send(_invoker).ConvertToJsonToken();
        }

        /// <summary>
        /// Updates test step result.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test execution issue.</param>
        /// <param name="run">The execution details ID.</param>
        /// <param name="step">The step ID and status to update.</param>
        public void UpdateStepStatus((string id, string key) idAndKey, string run, (string id, string key) step)
        {
            XpandCommands.UpdateStepStatus(idAndKey, run, step).Send(_invoker);
        }

        /// <summary>
        /// Updates test step actual result.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test execution issue.</param>
        /// <param name="run">The execution details ID.</param>
        /// <param name="step">The step ID and result to update.</param>
        public void UpdateStepActual(
            (string id, string key) idAndKey,
            string run,
            (string id, string actual) step)
        {
            XpandCommands.UpdateStepActual(idAndKey, run, step).Send(_invoker);
        }

        /// <summary>
        /// Adds a test execution to an existing test plan.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test plan issue.</param>
        /// <param name="idExecution">The ID of the test execution issue.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public JToken AddExecutionToPlan((string id, string key) idAndKey, string idExecution)
        {
            return XpandCommands.AddExecutionToPlan(idAndKey, idExecution).Send(_invoker).ConvertToJsonToken();
        }

        /// <summary>
        /// Adds a collection of test issue to an existing test set.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test set issue.</param>
        /// <param name="idsTests">A collection of test issue is to add.</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public JToken AddTestsToSet((string id, string key) idAndKey, IEnumerable<string> idsTests)
        {
            return XpandCommands.AddTestsToSet(idAndKey, idsTests).Send(_invoker).ConvertToJsonToken();
        }

        /// <summary>
        /// Sets a comment on test execution.
        /// </summary>
        /// <param name="idAndKey">The internal runtime ID and key of the test set issue.</param>
        /// <param name="comment">The comment to set</param>
        /// <returns>HttpCommand ready for execution.</returns>
        public JToken SetCommentOnExecution((string id, string key) idAndKey, string comment)
        {
            return XpandCommands.NewCommentOnExecution(idAndKey, comment).Send(_invoker);
        }

        /// <summary>
        /// Adds an existing defect to an existing execution.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the bug issue.</param>
        /// <param name="idExecution">The internal runtime id of the excution.</param>
        public JToken AddDefectToExecution((string id, string key) idAndKey, string idExecution)
        {
            return XpandCommands.AddDefectToExecution(idAndKey, idExecution).Send(_invoker);
        }
        #endregion

        #region ***  Test Steps        ***
        /// <summary>
        /// Deletes the given test step.
        /// </summary>
        /// <param name="idAndKey">The test issue ID and key.</param>
        /// <param name="stepId">The step runtime id.</param>
        public void DeleteTestStep((string id, string key) idAndKey, string stepId)
        {
            XpandCommands.RemoveTestStep(idAndKey, stepId, false).Send(_invoker);
        }

        /// <summary>
        /// Deletes the given test step.
        /// </summary>
        /// <param name="idAndKey">The test issue ID and key.</param>
        /// <param name="stepId">The step runtime id.</param>
        /// <param name="removeFromJira"><see cref="true"/> to remove from Jira; <see cref="false"/> to keep it.</param>
        public void DeleteTestStep((string id, string key) idAndKey, string stepId, bool removeFromJira)
        {
            XpandCommands.RemoveTestStep(idAndKey, stepId, removeFromJira).Send(_invoker);
        }

        /// <summary>
        /// Adds a test step to an existing test issue.
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test issue.</param>
        /// <param name="action">The step action.</param>
        /// <param name="result">The step expected result.</param>
        /// <param name="index">The step order in the test case steps collection.</param>
        public void CreateTestStep((string id, string key) idAndKey, string action, string result, int index)
        {
            XpandCommands.NewTestStep(idAndKey, action, result, index).Send(_invoker);
        }

        /// <summary>
        /// Creates an evidence on a test step and test run (the same evidence, linked to both)
        /// </summary>
        /// <param name="idAndKey">The ID and key of the test run issue.</param>
        /// <param name="testRun">The test run internal ID.</param>
        /// <param name="testStep">The test step internal ID.</param>
        /// <param name="file">The file to upload as evidence.</param>
        public void CreateEvidence((string id, string key) idAndKey, string testRun, string testStep, string file)
        {
            // setup: create attachment request (on test run)
            var request = CreateAttachmentRequest(testRun, idAndKey.key, file);

            // send to jira
            var response = JiraCommandInvoker.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError($"Create-Attachment -Key [{idAndKey.key}] -File [{file}] = false");
                return;
            }

            // setup
            var requestBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            // send to XpandIT step
            request = CreateEvidenceRequest(idAndKey.key, testRun, testStep, requestBody);
            response = JiraCommandInvoker.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError($"Create-Evidence -Key [{idAndKey.key}] -Step [{testStep}] -File [{file}] = false");
            }

            // send to XpandIT run
            request = CreateEvidenceRequest(idAndKey.key, testRun, testStep: string.Empty, requestBody);
            response = JiraCommandInvoker.HttpClient.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError($"Create-Evidence -Key [{idAndKey.key}] -Run [{testRun}] -File [{file}] = false");
            }
        }

        private HttpRequestMessage CreateAttachmentRequest(string testRun, string key, string file)
        {
            // setup
            var urlPath = $"{XpandCommands.XpandBaseUrl}/api/internal/attachments?testRunId={testRun}";

            // build request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, urlPath);
            requestMessage.Headers.ExpectContinue = false;
            requestMessage.Headers.Authorization = Authentication.NewAuthenticationHeader();
            requestMessage.Headers.Add("X-Atlassian-Token", "no-check");

            // build multi part content
            var multiPartContent = new MultipartFormDataContent($"----{Guid.NewGuid()}");

            // build file content
            var fileInfo = new FileInfo(file);
            var fileContents = File.ReadAllBytes(fileInfo.FullName);
            var byteArrayContent = new ByteArrayContent(fileContents);
            byteArrayContent.Headers.Add("Content-Type", "application/octet-stream");
            multiPartContent.Add(byteArrayContent, "attachment", fileInfo.Name);

            // set request content
            var itoken = JiraClient.Authentication.GetJwt(key).Result;
            requestMessage.Content = multiPartContent;
            requestMessage.Headers.Add("X-acpt", itoken/* jiraClient.GetJwt(key)*/);

            // get
            return requestMessage;
        }

        private HttpRequestMessage CreateEvidenceRequest(string key, string testRun, string testStep, string requestBody)
        {
            // setup
            var endpoint = string.IsNullOrEmpty(testStep)
                ? $"{XpandCommands.XpandBaseUrl}/api/internal/testrun/{testRun}/evidence"
                : $"{XpandCommands.XpandBaseUrl}/api/internal/testrun/{testRun}/step/{testStep}/evidence";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var jwt = JiraClient.Authentication.GetJwt(key).Result; /*jiraClient.GetJwt(key);*/

            // build
            var request = new HttpRequestMessage
            {
                Content = content,
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpoint)
            };
            request.Headers.ExpectContinue = false;
            request.Headers.Authorization = Authentication.NewAuthenticationHeader();
            request.Headers.Add("X-Atlassian-Token", "no-check");
            request.Headers.Add("X-acpt", jwt);

            // get
            return request;
        }
        #endregion

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
    }
}
