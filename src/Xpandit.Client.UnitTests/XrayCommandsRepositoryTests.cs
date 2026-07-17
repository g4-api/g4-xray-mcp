using System.Net;
using System.Text.Json;

using Xpandit.Client;
using Xpandit.Client.Exceptions;
using Xpandit.Client.Models;
using Xpandit.Client.Repositories;

namespace Xpandit.Client.UnitTests
{
    /// <summary>
    /// Verifies the standalone Xray repository contract through deterministic HTTP request and response sequences.
    /// </summary>
    [TestClass]
    [TestCategory("XrayCommandsRepository")]
    [TestCategory("UnitTest")]
    public class XrayCommandsRepositoryTests
    {
        #region *** Methods      ***
        [TestMethod(DisplayName = "Verify that AddTestStep sends typed variables and maps the persisted step.")]
        public async Task AddTestStepTypedVariablesTestAsync()
        {
            // Arrange: queue authentication and a complete step response so serialization and mapping share one flow.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\n" +
                "    \"data\": {\n" +
                "        \"addTestStep\": {\n" +
                "            \"id\": \"step-1\",\n" +
                "            \"action\": \"Open page\",\n" +
                "            \"data\": \"GAR-22\",\n" +
                "            \"result\": \"Page opens\",\n" +
                "            \"customFields\": [{\"id\": \"field-1\", \"value\": \"Chrome\"}]\n" +
                "        }\n" +
                "    }\n" +
                "}");
            var repository = NewRepository(handler);
            var request = new AddTestStepRequest
            {
                IssueId = "10097",
                VersionId = 3,
                Step = new XrayTestStepInput
                {
                    Action = "Open page",
                    Data = "GAR-22",
                    Result = "Page opens",
                    CustomFields =
                    [
                        new XrayCustomStepField
                        {
                            Id = "field-1",
                            Value = "Chrome"
                        }
                    ]
                }
            };

            // Act: add the step through the same public repository method used by production callers.
            var result = await repository.AddTestStepAsync(request);

            // Assert: the result retains the stable step ID and the request contains numeric identity and version data.
            Assert.AreEqual("step-1", result.Id);
            Assert.AreEqual("Open page", result.Action);
            Assert.AreEqual(2, handler.Requests.Count);
            Assert.AreEqual("Bearer token-one", handler.Requests[1].Authorization);

            using var requestDocument = JsonDocument.Parse(handler.Requests[1].Body);
            var variables = requestDocument.RootElement.GetProperty("variables");
            Assert.AreEqual("10097", variables.GetProperty("issueId").GetString());
            Assert.AreEqual(3, variables.GetProperty("versionId").GetInt32());
            Assert.AreEqual(
                "field-1",
                variables.GetProperty("step").GetProperty("customFields")[0].GetProperty("id").GetString());
        }

        [TestMethod(DisplayName = "Verify that an injected XrayGraphQlClient executes typed repository commands.")]
        public async Task InjectedGraphQlClientTestAsync()
        {
            // Arrange: inject one public client so repository commands share its authentication lifecycle.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\"data\":{\"addTestStep\":{\"id\":\"step-1\",\"action\":\"Open page\",\"result\":\"Page opens\"}}}");
            var repository = new XrayCommandsRepository(NewGraphQlClient(handler));

            // Act: execute the typed step mutation through the constructor used by the domain integration.
            var result = await repository.AddTestStepAsync(new AddTestStepRequest
            {
                IssueId = "10097",
                Step = new XrayTestStepInput
                {
                    Action = "Open page",
                    Result = "Page opens"
                }
            });

            // Assert: the injected client authenticates once and maps the persisted step through the repository.
            Assert.AreEqual("step-1", result.Id);
            Assert.AreEqual(2, handler.Requests.Count);
            Assert.AreEqual("Bearer token-one", handler.Requests[1].Authorization);
        }

        [TestMethod(DisplayName = "Verify that authentication is reused across independent GraphQL commands.")]
        public async Task AuthenticationTokenReuseTestAsync()
        {
            // Arrange: provide one token followed by two folder responses to expose redundant authentication calls.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"shared-token\"");
            handler.AddResponse(HttpStatusCode.OK, NewFolderResponse(path: "/"));
            handler.AddResponse(HttpStatusCode.OK, NewFolderResponse(path: "/Regression"));
            var repository = NewRepository(handler);

            // Act: invoke two commands through the same repository instance and token cache.
            await repository.GetFoldersAsync(new GetFoldersRequest
            {
                ProjectId = "10035",
                Path = "/"
            });
            await repository.GetFoldersAsync(new GetFoldersRequest
            {
                ProjectId = "10035",
                Path = "/Regression"
            });

            // Assert: only the initial request authenticates and both GraphQL requests reuse the same bearer value.
            Assert.AreEqual(3, handler.Requests.Count);
            Assert.AreEqual(new Uri("https://unit.test/authenticate"), handler.Requests[0].Uri);
            Assert.AreEqual("Bearer shared-token", handler.Requests[1].Authorization);
            Assert.AreEqual("Bearer shared-token", handler.Requests[2].Authorization);
        }

        [TestMethod(DisplayName = "Verify that caller cancellation stops pending authentication immediately.")]
        public async Task CancellationStopsAuthenticationTestAsync()
        {
            // Arrange: hold authentication open until the test observes the request and cancels its owned token.
            var handler = new TestHttpMessageHandler();
            var requestStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            handler.AddResponse(async (_, cancellationToken) =>
            {
                requestStarted.SetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            var repository = NewRepository(handler);
            using var cancellationSource = new CancellationTokenSource();

            // Act: begin the command, wait for transport ownership, then stop it through the public token contract.
            var commandTask = repository.GetFoldersAsync(
                new GetFoldersRequest
                {
                    ProjectId = "10035",
                    Path = "/"
                },
                cancellationSource.Token);
            await requestStarted.Task;
            cancellationSource.Cancel();

            // Assert: cancellation escapes unchanged and no repeated authentication request is allocated.
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await commandTask);
            Assert.AreEqual(1, handler.Requests.Count);
        }

        [TestMethod(DisplayName = "Verify that GraphQL errors retain message, path, and response context.")]
        public async Task GraphQlErrorMappingTestAsync()
        {
            // Arrange: return a GraphQL error envelope after successful authentication to exercise protocol-level failure.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\n" +
                "    \"errors\": [\n" +
                "        {\n" +
                "            \"message\": \"Folder access denied\",\n" +
                "            \"path\": [\"getFolder\"],\n" +
                "            \"extensions\": {\"code\": \"FORBIDDEN\"}\n" +
                "        }\n" +
                "    ],\n" +
                "    \"data\": null\n" +
                "}");
            var repository = NewRepository(handler);

            // Act: capture the public exception after the transport interprets the successful HTTP envelope.
            var exception = await Assert.ThrowsExactlyAsync<XpanditClientException>(async () =>
                await repository.GetFoldersAsync(new GetFoldersRequest
                {
                    ProjectId = "10035",
                    Path = "/"
                }));

            // Assert: callers receive structured GraphQL diagnostics rather than a generic serialization failure.
            Assert.AreEqual(HttpStatusCode.OK, exception.StatusCode);
            Assert.AreEqual(1, exception.Errors.Count);
            Assert.AreEqual("Folder access denied", exception.Errors.Single().Message);
            CollectionAssert.AreEqual(new[] { "getFolder" }, exception.Errors.Single().Path.ToArray());
        }

        [TestMethod(DisplayName = "Verify that GetTestRun maps manual step identities and recorded outcomes.")]
        public async Task GetTestRunMapsManualStepsTestAsync()
        {
            // Arrange: queue a complete manual Test Run snapshot behind one successful authentication response.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\n" +
                "    \"data\": {\n" +
                "        \"getTestRun\": {\n" +
                "            \"id\": \"run-1\",\n" +
                "            \"status\": {\"name\": \"EXECUTING\"},\n" +
                "            \"test\": {\"issueId\": \"10097\"},\n" +
                "            \"testExecution\": {\"issueId\": \"20003\"},\n" +
                "            \"steps\": [\n" +
                "                {\n" +
                "                    \"id\": \"run-step-1\",\n" +
                "                    \"action\": \"Open page\",\n" +
                "                    \"data\": \"Chrome\",\n" +
                "                    \"result\": \"Page opens\",\n" +
                "                    \"actualResult\": \"Page opened\",\n" +
                "                    \"comment\": \"Observed manually\",\n" +
                "                    \"status\": {\"name\": \"PASSED\"}\n" +
                "                },\n" +
                "                {\n" +
                "                    \"id\": \"run-step-2\",\n" +
                "                    \"action\": \"Sign in\",\n" +
                "                    \"data\": null,\n" +
                "                    \"result\": \"Dashboard opens\",\n" +
                "                    \"actualResult\": null,\n" +
                "                    \"comment\": null,\n" +
                "                    \"status\": {\"name\": \"TODO\"}\n" +
                "                }\n" +
                "            ]\n" +
                "        }\n" +
                "    }\n" +
                "}");
            var repository = NewRepository(handler);

            // Act: resolve the run through the public composite-identity command used by domain orchestration.
            var result = await repository.GetTestRunAsync(new GetTestRunRequest
            {
                TestExecutionIssueId = "20003",
                TestIssueId = "10097"
            });

            // Assert: the run retains both owning issue IDs and Xray's opaque mutation identity.
            Assert.AreEqual("run-1", result.Id);
            Assert.AreEqual("EXECUTING", result.Status);
            Assert.AreEqual("20003", result.TestExecutionIssueId);
            Assert.AreEqual("10097", result.TestIssueId);
            Assert.AreEqual(2, result.Steps.Count);

            // Assert: expected and actual outcomes remain distinct on the first ordered manual step.
            var firstStep = result.Steps.First();
            Assert.AreEqual("run-step-1", firstStep.Id);
            Assert.AreEqual("Page opens", firstStep.ExpectedResult);
            Assert.AreEqual("Page opened", firstStep.ActualResult);
            Assert.AreEqual("Observed manually", firstStep.Comment);
            Assert.AreEqual("PASSED", firstStep.Status);

            // Assert: GraphQL receives numeric Test and Test Execution identities under their documented variables.
            using var requestDocument = JsonDocument.Parse(handler.Requests[1].Body);
            var variables = requestDocument.RootElement.GetProperty("variables");
            Assert.AreEqual("10097", variables.GetProperty("testIssueId").GetString());
            Assert.AreEqual("20003", variables.GetProperty("testExecutionIssueId").GetString());
        }

        [TestMethod(DisplayName = "Verify that GetTestRun reports an absent composite Test Run identity.")]
        public async Task GetTestRunMissingRunTestAsync()
        {
            // Arrange: authenticate successfully and return an explicit null Test Run for the requested issues.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(HttpStatusCode.OK, "{\"data\":{\"getTestRun\":null}}");
            var repository = NewRepository(handler);

            // Act: capture the not-found contract exposed after Xray completes the composite lookup.
            var exception = await Assert.ThrowsExactlyAsync<KeyNotFoundException>(async () =>
                await repository.GetTestRunAsync(new GetTestRunRequest
                {
                    TestExecutionIssueId = "20003",
                    TestIssueId = "10097"
                }));

            // Assert: the diagnostic retains both issue IDs required to investigate the missing association.
            StringAssert.Contains(exception.Message, "10097");
            StringAssert.Contains(exception.Message, "20003");
        }

        [TestMethod(DisplayName = "Verify that move and update commands use normalized paths and sparse step fields.")]
        public async Task MoveAndUpdateCommandsTestAsync()
        {
            // Arrange: queue scalar move data and a warning-bearing update response behind one cached token.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\"data\":{\"updateTestFolder\":\"/Regression/API\"}}");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\"data\":{\"updateTestStep\":{\"warnings\":[\"field normalized\"]}}}");
            var repository = NewRepository(handler);

            // Act: move a Test and update only its expected result through separate typed commands.
            await repository.MoveTestToFolderAsync(new MoveTestToFolderRequest
            {
                IssueId = "10097",
                Path = "Regression\\API"
            });
            var updateResult = await repository.UpdateTestStepAsync(new UpdateTestStepRequest
            {
                StepId = "step-1",
                Step = new XrayTestStepUpdate
                {
                    Result = "Updated result"
                }
            });

            // Assert: the folder is absolute, the update omits untouched fields, and warnings remain visible.
            using var moveDocument = JsonDocument.Parse(handler.Requests[1].Body);
            var moveVariables = moveDocument.RootElement.GetProperty("variables");
            Assert.AreEqual("/Regression/API", moveVariables.GetProperty("folderPath").GetString());

            using var updateDocument = JsonDocument.Parse(handler.Requests[2].Body);
            var step = updateDocument.RootElement.GetProperty("variables").GetProperty("step");
            Assert.AreEqual("Updated result", step.GetProperty("result").GetString());
            Assert.IsFalse(step.TryGetProperty("action", out _));
            CollectionAssert.AreEqual(new[] { "field normalized" }, updateResult.Warnings.ToArray());
        }

        [TestMethod(DisplayName = "Verify that NewFolder creates only missing cumulative repository paths.")]
        public async Task NewFolderMissingSegmentsTestAsync()
        {
            // Arrange: expose an existing parent and queue responses for two missing segments plus final verification.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\n" +
                "    \"data\": {\n" +
                "        \"getFolder\": {\n" +
                "            \"name\": \"\",\n" +
                "            \"path\": \"/\",\n" +
                "            \"issuesCount\": 0,\n" +
                "            \"testsCount\": 0,\n" +
                "            \"preconditionsCount\": 0,\n" +
                "            \"folders\": [{\"name\":\"Regression\",\"path\":\"/Regression\",\"folders\":[]}]\n" +
                "        }\n" +
                "    }\n" +
                "}");
            handler.AddResponse(HttpStatusCode.OK, NewFolderMutationResponse(path: "/Regression/API"));
            handler.AddResponse(HttpStatusCode.OK, NewFolderMutationResponse(path: "/Regression/API/Smoke"));
            handler.AddResponse(HttpStatusCode.OK, NewFolderResponse(path: "/Regression/API/Smoke"));
            var repository = NewRepository(handler);

            // Act: ensure a three-segment path where only the first segment already exists.
            var folder = await repository.NewFolderAsync(new NewFolderRequest
            {
                ProjectId = "10035",
                Path = "/Regression/API/Smoke"
            });

            // Assert: the two missing segments are created parent-first and the verified leaf is returned.
            Assert.AreEqual("/Regression/API/Smoke", folder.Path);
            Assert.AreEqual(5, handler.Requests.Count);
            Assert.AreEqual("/Regression/API", GetRequestVariable(handler.Requests[2].Body, "path"));
            Assert.AreEqual("/Regression/API/Smoke", GetRequestVariable(handler.Requests[3].Body, "path"));
        }

        [TestMethod(DisplayName = "Verify that issue creation commands send optional Test IDs and map identities.")]
        public async Task NewIssueCommandsOptionalTestIdsTestAsync()
        {
            // Arrange: queue one token and entity-specific payloads for Set, Plan, and Execution creation.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(HttpStatusCode.OK, NewIssueResponse("createTestSet", "testSet", "20001", "GAR-30"));
            handler.AddResponse(HttpStatusCode.OK, NewIssueResponse("createTestPlan", "testPlan", "20002", "GAR-31"));
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\"data\":{\"createTestExecution\":{\"testExecution\":{\"issueId\":\"20003\",\"jira\":{\"key\":\"GAR-32\"}}," +
                "\"warnings\":[],\"createdTestEnvironments\":[\"Chrome\"]}}}");
            var repository = NewRepository(handler);
            var jira = new XrayJiraIssue
            {
                ProjectKey = "GAR",
                Summary = "Created by unit test",
                AdditionalFields = new Dictionary<string, object>
                {
                    ["labels"] = new[] { "automated" }
                }
            };

            // Act: create each supported issue type with the same optional Test association.
            var testSet = await repository.NewTestSetAsync(new NewTestSetRequest
            {
                Jira = jira,
                TestIssueIds = ["10097"]
            });
            var testPlan = await repository.NewTestPlanAsync(new NewTestPlanRequest
            {
                Jira = jira,
                TestIssueIds = ["10097"]
            });
            var testExecution = await repository.NewTestExecutionAsync(new NewTestExecutionRequest
            {
                Jira = jira,
                TestEnvironments = ["Chrome"],
                TestIssueIds = ["10097"]
            });

            // Assert: identities map by entity and each serialized mutation retains the optional Test ID.
            Assert.AreEqual("GAR-30", testSet.Key);
            Assert.AreEqual("20002", testPlan.IssueId);
            Assert.AreEqual("GAR-32", testExecution.Key);
            CollectionAssert.AreEqual(new[] { "Chrome" }, testExecution.CreatedTestEnvironments.ToArray());

            for (var requestIndex = 1; requestIndex <= 3; requestIndex++)
            {
                using var requestDocument = JsonDocument.Parse(handler.Requests[requestIndex].Body);
                var testIssueIds = requestDocument.RootElement
                    .GetProperty("variables")
                    .GetProperty("testIssueIds");
                Assert.AreEqual("10097", testIssueIds[0].GetString());
            }
        }

        [TestMethod(DisplayName = "Verify that NewTest creates the Jira issue and ordered Manual definition in one mutation.")]
        public async Task NewTestCompleteDefinitionTestAsync()
        {
            // Arrange: queue one complete Test result containing its Jira identity, type, steps, and warning.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\n" +
                "    \"data\": {\n" +
                "        \"createTest\": {\n" +
                "            \"test\": {\n" +
                "                \"issueId\": \"10097\",\n" +
                "                \"testType\": {\"name\": \"Manual\"},\n" +
                "                \"steps\": [\n" +
                "                    {\"id\": \"step-1\", \"action\": \"Open page\", \"data\": null, \"result\": \"Page opens\", \"customFields\": []},\n" +
                "                    {\"id\": \"step-2\", \"action\": \"Sign in\", \"data\": null, \"result\": \"Dashboard opens\", \"customFields\": []}\n" +
                "                ],\n" +
                "                \"jira\": {\"key\": \"GAR-22\"}\n" +
                "            },\n" +
                "            \"warnings\": [\"description normalized\"]\n" +
                "        }\n" +
                "    }\n" +
                "}");
            var repository = NewRepository(handler);
            var request = new NewTestRequest
            {
                Jira = new XrayJiraIssue
                {
                    AdditionalFields = new Dictionary<string, object>
                    {
                        ["description"] = "Verify sign-in.",
                        ["labels"] = new[] { "automated" }
                    },
                    ProjectKey = "GAR",
                    Summary = "Verify sign-in"
                },
                Steps =
                [
                    new XrayTestStepInput
                    {
                        Action = "Open page",
                        Result = "Page opens"
                    },
                    new XrayTestStepInput
                    {
                        Action = "Sign in",
                        Result = "Dashboard opens"
                    }
                ],
                TestTypeName = "Manual"
            };

            // Act: create the Jira issue and registered Xray Test through one repository command.
            var result = await repository.NewTestAsync(request);

            // Assert: the response and variables retain the complete ordered definition without an issue type field.
            Assert.AreEqual("10097", result.IssueId);
            Assert.AreEqual("GAR-22", result.Key);
            Assert.AreEqual("Manual", result.TestTypeName);
            Assert.AreEqual(2, result.Steps.Count);
            Assert.AreEqual("step-1", result.Steps.First().Id);
            CollectionAssert.AreEqual(new[] { "description normalized" }, result.Warnings.ToArray());

            using var requestDocument = JsonDocument.Parse(handler.Requests[1].Body);
            var variables = requestDocument.RootElement.GetProperty("variables");
            var jiraFields = variables.GetProperty("jira").GetProperty("fields");
            var steps = variables.GetProperty("steps");
            Assert.AreEqual("Manual", variables.GetProperty("testType").GetProperty("name").GetString());
            Assert.AreEqual("GAR", jiraFields.GetProperty("project").GetProperty("key").GetString());
            Assert.AreEqual("Verify sign-in", jiraFields.GetProperty("summary").GetString());
            Assert.AreEqual("Verify sign-in.", jiraFields.GetProperty("description").GetString());
            Assert.AreEqual("automated", jiraFields.GetProperty("labels")[0].GetString());
            Assert.IsFalse(jiraFields.TryGetProperty("issuetype", out _));
            Assert.AreEqual("Open page", steps[0].GetProperty("action").GetString());
            Assert.AreEqual("Sign in", steps[1].GetProperty("action").GetString());
        }

        [TestMethod(DisplayName = "Verify that NewTest rejects a null creation payload.")]
        public async Task NewTestMissingPayloadTestAsync()
        {
            // Arrange: authenticate successfully and return an explicit null createTest payload.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(HttpStatusCode.OK, "{\"data\":{\"createTest\":null}}");
            var repository = NewRepository(handler);

            // Act: capture the protocol failure exposed by the typed creation command.
            var exception = await Assert.ThrowsExactlyAsync<XpanditClientException>(async () =>
                await repository.NewTestAsync(new NewTestRequest
                {
                    Jira = new XrayJiraIssue
                    {
                        ProjectKey = "GAR",
                        Summary = "Verify sign-in"
                    }
                }));

            // Assert: the missing mutation payload is reported instead of being treated as success.
            StringAssert.Contains(exception.Message, "createTest");
        }

        [TestMethod(DisplayName = "Verify that NewTest rejects a creation payload without a registered Test.")]
        public async Task NewTestMissingTestTestAsync()
        {
            // Arrange: return the mutation envelope without the Xray Test registration result.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\"data\":{\"createTest\":{\"test\":null,\"warnings\":[]}}}");
            var repository = NewRepository(handler);

            // Act: capture the missing Test failure through the public repository contract.
            var exception = await Assert.ThrowsExactlyAsync<XpanditClientException>(async () =>
                await repository.NewTestAsync(new NewTestRequest
                {
                    Jira = new XrayJiraIssue
                    {
                        ProjectKey = "GAR",
                        Summary = "Verify sign-in"
                    }
                }));

            // Assert: callers do not receive a false-positive result from an HTTP-success envelope.
            StringAssert.Contains(exception.Message, "'test'");
        }

        [TestMethod(DisplayName = "Verify that the root XrayGraphQlClient is publicly callable by package consumers.")]
        public async Task PublicClientInvokeTestAsync()
        {
            // Arrange: configure the public client directly so the test does not rely on repository-level access.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(HttpStatusCode.OK, "{\"data\":{\"status\":\"ready\"}}");
            var httpClient = new HttpClient(handler);
            var options = new XrayClientOptions
            {
                AuthenticationEndpoint = new Uri("https://unit.test/authenticate"),
                ClientId = "client-id",
                ClientSecret = "client-secret",
                GraphQlEndpoint = new Uri("https://unit.test/graphql"),
                Retry = new XrayRetryOptions
                {
                    Delay = TimeSpan.Zero,
                    MaxAttempts = 3
                }
            };
            var client = new XrayGraphQlClient(httpClient, options);

            // Act: invoke a raw GraphQL document through the root public package API.
            var data = await client.InvokeAsync(
                operationName: "PublicClient",
                query: "query PublicClient { status }",
                variables: new { });

            // Assert: direct consumers receive detached GraphQL data and authenticated request behavior.
            Assert.AreEqual("ready", data.GetProperty("status").GetString());
            Assert.AreEqual("Bearer token-one", handler.Requests[1].Authorization);
        }

        [TestMethod(DisplayName = "Verify that ResolveTestIssueId uses exact-key JQL and returns a numeric ID.")]
        public async Task ResolveTestIssueIdExactKeyTestAsync()
        {
            // Arrange: queue one matching Test result after authentication.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\"data\":{\"getTests\":{\"total\":1,\"results\":[{\"issueId\":\"10097\",\"jira\":{\"key\":\"GAR-22\"}}]}}}");
            var repository = NewRepository(handler);

            // Act: resolve the human-readable Jira key through the explicit lookup command.
            var issueId = await repository.ResolveTestIssueIdAsync("GAR-22");

            // Assert: the numeric ID is returned and the key remains inside the GraphQL variables object.
            Assert.AreEqual("10097", issueId);
            Assert.AreEqual("key = \"GAR-22\"", GetRequestVariable(handler.Requests[1].Body, "jql"));
        }

        [TestMethod(DisplayName = "Verify that transient GraphQL responses use the configured repeatable-send policy.")]
        public async Task TransientResponseRetryTestAsync()
        {
            // Arrange: queue one service outage followed by success and remove delay to keep the test deterministic.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(HttpStatusCode.ServiceUnavailable, "{\"error\":\"temporary\"}");
            handler.AddResponse(HttpStatusCode.OK, NewFolderResponse(path: "/"));
            var repository = NewRepository(handler, maxAttempts: 3);

            // Act: query the root folder through the repeatable transport.
            var folder = await repository.GetFoldersAsync(new GetFoldersRequest
            {
                ProjectId = "10035",
                Path = "/"
            });

            // Assert: the command succeeds after one retry and both attempts retain the cached bearer token.
            Assert.IsNotNull(folder);
            Assert.AreEqual(3, handler.Requests.Count);
            Assert.AreEqual("Bearer token-one", handler.Requests[1].Authorization);
            Assert.AreEqual("Bearer token-one", handler.Requests[2].Authorization);
        }

        [TestMethod(DisplayName = "Verify that transient response exhaustion returns the final HTTP failure context.")]
        public async Task TransientResponseRetryExhaustionTestAsync()
        {
            // Arrange: return a token followed by three service failures to consume the complete retry policy.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(HttpStatusCode.ServiceUnavailable, "{\"error\":\"attempt-one\"}");
            handler.AddResponse(HttpStatusCode.ServiceUnavailable, "{\"error\":\"attempt-two\"}");
            handler.AddResponse(HttpStatusCode.ServiceUnavailable, "{\"error\":\"attempt-three\"}");
            var repository = NewRepository(handler, maxAttempts: 3);

            // Act: capture the public failure after the final transient response is no longer repeatable.
            var exception = await Assert.ThrowsExactlyAsync<XpanditClientException>(async () =>
                await repository.GetFoldersAsync(new GetFoldersRequest
                {
                    ProjectId = "10035",
                    Path = "/"
                }));

            // Assert: all attempts occurred and the final service status remains available to diagnostics.
            Assert.AreEqual(4, handler.Requests.Count);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
            StringAssert.Contains(exception.ResponseBody, "attempt-three");
        }

        [TestMethod(DisplayName = "Verify that a 401 refreshes authentication once and replays the command.")]
        public async Task UnauthorizedTokenRefreshTestAsync()
        {
            // Arrange: model the full token-one rejection and token-two replay sequence.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(HttpStatusCode.Unauthorized, "{\"error\":\"expired\"}");
            handler.AddResponse(HttpStatusCode.OK, "\"token-two\"");
            handler.AddResponse(HttpStatusCode.OK, NewFolderResponse(path: "/"));
            var repository = NewRepository(handler);

            // Act: execute one folder command whose first bearer token is rejected.
            var folder = await repository.GetFoldersAsync(new GetFoldersRequest
            {
                ProjectId = "10035",
                Path = "/"
            });

            // Assert: the command completes after exactly one refresh and the replay carries the replacement token.
            Assert.IsNotNull(folder);
            Assert.AreEqual(4, handler.Requests.Count);
            Assert.AreEqual("Bearer token-one", handler.Requests[1].Authorization);
            Assert.AreEqual(new Uri("https://unit.test/authenticate"), handler.Requests[2].Uri);
            Assert.AreEqual("Bearer token-two", handler.Requests[3].Authorization);
        }

        [TestMethod(DisplayName = "Verify that UpdateTestRunStep rejects an update without selected outcome values.")]
        public async Task UpdateTestRunStepEmptyUpdateTestAsync()
        {
            // Arrange: create an isolated repository without responses because local validation must prevent I/O.
            var handler = new TestHttpMessageHandler();
            var repository = NewRepository(handler);

            // Act: capture the validation failure for a run-step update with no selected execution values.
            var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
                await repository.UpdateTestRunStepAsync(new UpdateTestRunStepRequest
                {
                    StepId = "run-step-1",
                    TestRunId = "run-1",
                    Update = new XrayTestRunStepUpdate()
                }));

            // Assert: the failure identifies the sparse update contract and no authentication request is allocated.
            StringAssert.Contains(exception.Message, "At least one Test Run Step field");
            Assert.AreEqual(0, handler.Requests.Count);
        }

        [TestMethod(DisplayName = "Verify that UpdateTestRunStep sends sparse manual execution values.")]
        public async Task UpdateTestRunStepSparseFieldsTestAsync()
        {
            // Arrange: queue one successful mutation with a non-fatal Xray warning behind cached authentication.
            var handler = new TestHttpMessageHandler();
            handler.AddResponse(HttpStatusCode.OK, "\"token-one\"");
            handler.AddResponse(
                HttpStatusCode.OK,
                "{\"data\":{\"updateTestRunStep\":{\"warnings\":[\"status normalized\"]}}}");
            var repository = NewRepository(handler);

            // Act: record an observed outcome and status while leaving comment and iteration context untouched.
            var result = await repository.UpdateTestRunStepAsync(new UpdateTestRunStepRequest
            {
                StepId = "run-step-1",
                TestRunId = "run-1",
                Update = new XrayTestRunStepUpdate
                {
                    ActualResult = "Page opened",
                    Status = "PASSED"
                }
            });

            // Assert: opaque run identities and selected values reach the documented GraphQL variable names.
            using var requestDocument = JsonDocument.Parse(handler.Requests[1].Body);
            var variables = requestDocument.RootElement.GetProperty("variables");
            var updateData = variables.GetProperty("updateData");
            Assert.AreEqual("run-1", variables.GetProperty("testRunId").GetString());
            Assert.AreEqual("run-step-1", variables.GetProperty("stepId").GetString());
            Assert.AreEqual("Page opened", updateData.GetProperty("actualResult").GetString());
            Assert.AreEqual("PASSED", updateData.GetProperty("status").GetString());

            // Assert: omitted values remain absent and Xray warnings stay visible to the manual-cycle caller.
            Assert.IsFalse(updateData.TryGetProperty("comment", out _));
            Assert.IsFalse(variables.TryGetProperty("iterationRank", out _));
            CollectionAssert.AreEqual(new[] { "status normalized" }, result.Warnings.ToArray());
        }

        // Reads one string variable from a captured GraphQL request while disposing temporary JSON state locally.
        private static string GetRequestVariable(string requestBody, string variableName)
        {
            using var document = JsonDocument.Parse(requestBody);
            return document.RootElement
                .GetProperty("variables")
                .GetProperty(variableName)
                .GetString();
        }

        // Produces a documented folder query response with an empty child tree for focused command tests.
        private static string NewFolderResponse(string path)
        {
            return
                "{\n" +
                "    \"data\": {\n" +
                "        \"getFolder\": {\n" +
                "            \"name\": \"Folder\",\n" +
                $"            \"path\": \"{path}\",\n" +
                "            \"issuesCount\": 0,\n" +
                "            \"testsCount\": 0,\n" +
                "            \"preconditionsCount\": 0,\n" +
                "            \"folders\": []\n" +
                "        }\n" +
                "    }\n" +
                "}";
        }

        // Produces a folder mutation response for one cumulative path created during recursive traversal.
        private static string NewFolderMutationResponse(string path)
        {
            return
                "{\n" +
                "    \"data\": {\n" +
                "        \"createFolder\": {\n" +
                "            \"folder\": {\"name\": \"Folder\", \"path\": \"" + path + "\"},\n" +
                "            \"warnings\": []\n" +
                "        }\n" +
                "    }\n" +
                "}";
        }

        // Produces the shared Jira identity response returned by Test Set and Test Plan creation mutations.
        private static string NewIssueResponse(
            string mutationName,
            string entityName,
            string issueId,
            string issueKey)
        {
            return
                "{\n" +
                "    \"data\": {\n" +
                $"        \"{mutationName}\": {{\n" +
                $"            \"{entityName}\": {{\"issueId\": \"{issueId}\", \"jira\": {{\"key\": \"{issueKey}\"}}}},\n" +
                "            \"warnings\": []\n" +
                "        }\n" +
                "    }\n" +
                "}";
        }

        // Creates a public GraphQL client with isolated endpoints and zero delay for deterministic request sequences.
        private static XrayGraphQlClient NewGraphQlClient(
            TestHttpMessageHandler handler,
            int maxAttempts = 3)
        {
            var httpClient = new HttpClient(handler);
            var options = new XrayClientOptions
            {
                AuthenticationEndpoint = new Uri("https://unit.test/authenticate"),
                ClientId = "client-id",
                ClientSecret = "client-secret",
                GraphQlEndpoint = new Uri("https://unit.test/graphql"),
                Retry = new XrayRetryOptions
                {
                    Delay = TimeSpan.Zero,
                    MaxAttempts = maxAttempts
                }
            };

            return new XrayGraphQlClient(httpClient, options);
        }

        // Creates a repository over an isolated public client so tests exercise production constructor wiring.
        private static XrayCommandsRepository NewRepository(
            TestHttpMessageHandler handler,
            int maxAttempts = 3)
        {
            return new XrayCommandsRepository(NewGraphQlClient(handler, maxAttempts));
        }
        #endregion
    }
}
