using Mcp.Xray.Domain.Clients;
using Mcp.Xray.Domain.Exceptions;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Xpandit.Client;
using Xpandit.Client.Models;
using Xpandit.Client.Repositories;

namespace Mcp.Xray.Domain.Repositories
{
    /// <summary>
    /// Provides an Xray repository implementation backed by the Xpand API,
    /// using Jira as the underlying issue management system.
    /// This repository is intended for Jira Cloud environments where Xray
    /// functionality is exposed through the Xpand integration.
    /// The repository encapsulates all low-level client interactions and
    /// exposes a stable interface to the rest of the domain.
    /// </summary>
    public class XrayXpandRepository(JiraAuthenticationModel jiraAuthentication) : IXrayRepository
    {
        #region *** Fields       ***
        // Jira client used to create and manage Jira issues related to Xray tests.
        // This client is initialized using the provided authentication model
        // and is reused internally by the repository for all Jira operations.
        private readonly JiraClient _jiraClient = new(jiraAuthentication);

        // Xpand client used to manage Xray-specific entities such as test steps.
        // This client handles communication with the Xpand API layer and is
        // responsible for extending Jira test issues with Xray metadata.
        private readonly XpandClient _xpandClient = new(jiraAuthentication);
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public object AddTestExecutionsToPlan(AddTestExecutionsToPlanModel association)
        {
            try
            {
                // Assert the complete Jira-key association contract before performing identity lookups.
                AssertArguments(association);

                // Resolve every entity before mutation so a missing Jira issue cannot leave a partial association.
                var testPlanIdentity = GetIssueIdentity(_jiraClient, association.TestPlanKey);
                var testExecutionKeys = association.TestExecutionKeys
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var testExecutionIdentities = testExecutionKeys
                    .Select(key => GetIssueIdentity(_jiraClient, key))
                    .ToArray();

                // Associate all resolved Test Executions through the public Xray GraphQL command repository.
                var commandsRepository = GetCommandsRepository();
                var result = commandsRepository
                    .AddTestExecutionsToTestPlanAsync(new AddTestExecutionsToTestPlanRequest
                    {
                        TestExecutionIssueIds = testExecutionIdentities
                            .Select(identity => identity.Id)
                            .ToArray(),
                        TestPlanIssueId = testPlanIdentity.Id
                    })
                    .GetAwaiter()
                    .GetResult();

                // Return requested identities and warnings so idempotent existing associations remain observable.
                return new
                {
                    TestPlanId = testPlanIdentity.Id,
                    TestPlanKey = testPlanIdentity.Key,
                    TestExecutionIds = testExecutionIdentities
                        .Select(identity => identity.Id)
                        .ToArray(),
                    TestExecutionKeys = testExecutionIdentities
                        .Select(identity => identity.Key)
                        .ToArray(),
                    result.Warnings
                };
            }
            catch (Exception exception)
            {
                // Convert integration failures into the stable error envelope consumed by MCP callers.
                return new
                {
                    Error = exception.GetBaseException().Message,
                    Message = "Failed to associate Xray Test Executions with the Test Plan."
                };
            }

            // Asserts the Test Plan key and required Test Execution key collection before Jira or Xray I/O.
            // The helper preserves caller state and reports nested failures against the owning association parameter.
            static void AssertArguments(AddTestExecutionsToPlanModel association)
            {
                // Require the root association before reading either side of the relationship.
                ArgumentNullException.ThrowIfNull(
                    argument: association,
                    paramName: nameof(association));

                // Require the Test Plan key consumed by Jira identity resolution.
                if (string.IsNullOrWhiteSpace(association.TestPlanKey))
                {
                    var message = "Test Execution association TestPlanKey cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(association));
                }

                // Require at least one concrete Test Execution key so the mutation always performs useful work.
                if (association.TestExecutionKeys is null || association.TestExecutionKeys.Length == 0)
                {
                    var message = "Test Execution association keys cannot be null or empty.";
                    throw new ArgumentException(message, nameof(association));
                }

                if (association.TestExecutionKeys.Any(string.IsNullOrWhiteSpace))
                {
                    var message = "Test Execution association keys cannot contain null or whitespace values.";
                    throw new ArgumentException(message, nameof(association));
                }
            }
        }

        /// <inheritdoc />
        public object AddTestsToExecution(AddTestsToExecutionModel association)
        {
            try
            {
                // Assert the complete Jira-key association contract before performing identity lookups.
                AssertArguments(association);

                // Resolve every entity before mutation so a missing Jira issue cannot leave a partial association.
                var executionIdentity = GetIssueIdentity(_jiraClient, association.ExecutionKey);
                var testKeys = association.TestKeys
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var testIdentities = testKeys
                    .Select(key => GetIssueIdentity(_jiraClient, key))
                    .ToArray();

                // Associate all resolved Tests through Xray before waiting for their executable Test Run state.
                var commandsRepository = GetCommandsRepository();
                var result = commandsRepository
                    .AddTestsToTestExecutionAsync(new AddTestsToTestExecutionRequest
                    {
                        TestExecutionIssueId = executionIdentity.Id,
                        TestIssueIds = testIdentities
                            .Select(identity => identity.Id)
                            .ToArray()
                    })
                    .GetAwaiter()
                    .GetResult();

                // Wait for every requested Test Run so a successful tool response is immediately executable.
                var testRuns = WaitForTestRuns(
                    commandsRepository,
                    executionIssueId: executionIdentity.Id,
                    testIdentities);

                // Return all resolved identities and registered runs for deterministic downstream step updates.
                return new
                {
                    ExecutionId = executionIdentity.Id,
                    ExecutionKey = executionIdentity.Key,
                    TestIds = testIdentities
                        .Select(identity => identity.Id)
                        .ToArray(),
                    TestKeys = testIdentities
                        .Select(identity => identity.Key)
                        .ToArray(),
                    TestRunIds = testRuns
                        .Select(testRun => testRun.Id)
                        .ToArray(),
                    result.Warnings
                };
            }
            catch (Exception exception)
            {
                // Convert integration failures into the stable error envelope consumed by MCP callers.
                return new
                {
                    Error = exception.GetBaseException().Message,
                    Message = "Failed to associate Xray Tests with the Test Execution."
                };
            }

            // Asserts the Test Execution key and required Test key collection before Jira or Xray I/O.
            // The helper preserves caller state and reports nested failures against the owning association parameter.
            static void AssertArguments(AddTestsToExecutionModel association)
            {
                // Require the root association before reading either side of the relationship.
                ArgumentNullException.ThrowIfNull(
                    argument: association,
                    paramName: nameof(association));

                // Require the Test Execution key consumed by Jira identity resolution.
                if (string.IsNullOrWhiteSpace(association.ExecutionKey))
                {
                    var message = "Test association ExecutionKey cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(association));
                }

                // Require at least one concrete Test key so the mutation always creates executable scope.
                if (association.TestKeys is null || association.TestKeys.Length == 0)
                {
                    var message = "Test association keys cannot be null or empty.";
                    throw new ArgumentException(message, nameof(association));
                }

                if (association.TestKeys.Any(string.IsNullOrWhiteSpace))
                {
                    var message = "Test association keys cannot contain null or whitespace values.";
                    throw new ArgumentException(message, nameof(association));
                }
            }
        }

        /// <inheritdoc />
        public object AddTestsToFolder(string idOrKey, string path, string jql)
        {
            // Resolve the repository folder identifier from the provided path.
            // This identifier is required by the internal Xray move operation.
            var folderId = _xpandClient.ResolveFolderPath(idOrKey, path);

            // Treat an empty folder identifier as a failed resolution and raise
            // a domain-specific exception to signal a missing repository node.
            if (string.IsNullOrEmpty(folderId))
            {
                throw new XrayTestRepositoryFolderNotFoundException(
                    message: $"Xray test repository folder not found at path {path}."
                );
            }

            // Execute the JQL query and extract issue identifiers for the move operation.
            // The internal Xray endpoint expects issue ids, so the response is projected
            // and filtered to include only valid identifiers.
            var issueIds = _jiraClient
                .GetIssues(jql: jql, "key")
                .Select(i => i.TryGetProperty("id", out JsonElement idOut) ? idOut.GetString() : string.Empty)
                .Where(i => !string.IsNullOrEmpty(i))
                .ToArray();

            // Invoke the internal Xray endpoint that performs the move into the repository folder.
            var response = _xpandClient.AddTestsToFolder(idOrKey, folderId, issueIds);

            // TODO: Add links when supported by Xpand API or composed manually.
            // Return a minimal consumer-friendly summary alongside the raw response payload.
            return new
            {
                Added = issueIds.Length,
                FolderId = folderId,
                Link = string.Empty,
                Path = path,
                Skipped = 0,
                Data = response
            };
        }

        /// <inheritdoc />
        public object AddTestsToPlan(string idOrKey, string jql)
        {
            try
            {
                // Execute the JQL in Jira to resolve matching test cases, then apply them
                // to the specified Test Plan using the Xray internal API.
                return AddTests(_jiraClient, _xpandClient, idOrKey, jql);
            }
            catch (Exception e)
            {
                // Applying test cases to the Test Plan failed.
                // Return a user-friendly error response suitable for tool/API callers.
                return new
                {
                    Error = e.GetBaseException().Message,
                    Message = "Failed to add test cases to the Xray Test Plan."
                };
            }
        }

        /// <inheritdoc />
        public object GetTest(string idOrKey)
        {
            // Resolve caller-facing Jira keys into the numeric identity required by the public Xray GraphQL query.
            var testIdentity = GetIssueIdentity(_jiraClient, idOrKey);

            // Retrieve the detached Test definition through the supported bearer-authenticated Xray client path.
            var commandsRepository = GetCommandsRepository();
            return commandsRepository.GetTestAsync(testIdentity.Id).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public object NewExecution(string project, NewExecutionModel execution)
        {
            try
            {
                // Assert the complete creation contract before resolving Jira identities or allocating GraphQL state.
                AssertArguments(project, execution);

                // Resolve every Test key before mutation so a missing Test cannot leave a partial Test Execution.
                var testIdentities = new List<(string Id, string Key)>();
                var testKeys = execution.TestKeys
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (var testKey in testKeys)
                {
                    testIdentities.Add(GetIssueIdentity(_jiraClient, testKey));
                }

                // Preserve the description and resolved Jira custom fields inside the Xray creation payload.
                var additionalFields = new Dictionary<string, object>
                {
                    ["description"] = execution.Description
                };
                var customFields = execution.CustomFields ?? [];

                foreach (var customField in customFields)
                {
                    var resolvedField = _jiraClient.GetCustomField(project, customField.Name);

                    if (!string.IsNullOrWhiteSpace(resolvedField))
                    {
                        additionalFields[resolvedField] = customField.Value;
                    }
                }

                // Translate caller-facing keys into the numeric Jira identities required by Xray GraphQL.
                var request = new NewTestExecutionRequest
                {
                    Jira = new XrayJiraIssue
                    {
                        AdditionalFields = additionalFields,
                        ProjectKey = project,
                        Summary = execution.Summary
                    },
                    TestEnvironments = execution.TestEnvironments ?? [],
                    TestIssueIds = testIdentities
                        .Select(identity => identity.Id)
                        .ToArray()
                };

                // Create the Test Execution and all initial associations in one public Xray mutation.
                var commandsRepository = GetCommandsRepository();
                var result = commandsRepository
                    .NewTestExecutionAsync(request)
                    .GetAwaiter()
                    .GetResult();

                // Wait for every initial Test Run so the returned Test Execution is ready for immediate recording.
                var testRuns = WaitForTestRuns(
                    commandsRepository,
                    executionIssueId: result.IssueId,
                    testIdentities);

                // Return stable Jira and Test Run identities together with non-fatal Xray diagnostics.
                return new
                {
                    Id = result.IssueId,
                    Key = result.Key,
                    Link = $"{jiraAuthentication.Collection}/browse/{result.Key}",
                    TestKeys = testIdentities
                        .Select(identity => identity.Key)
                        .ToArray(),
                    TestRunIds = testRuns
                        .Select(testRun => testRun.Id)
                        .ToArray(),
                    result.CreatedTestEnvironments,
                    result.Warnings
                };
            }
            catch (Exception exception)
            {
                // Return the established tool-friendly envelope while retaining the actionable integration failure.
                return new
                {
                    Error = exception.GetBaseException().Message,
                    Message = "Failed to create the Xray Test Execution and its initial Test associations."
                };
            }

            // Asserts the issue fields and associations before the parent method performs Jira or Xray I/O.
            // The helper preserves caller state and reports every invalid nested value against its owning parameter.
            static void AssertArguments(string project, NewExecutionModel execution)
            {
                // Require both declared parameters before validating the nested Test Execution contract.
                ArgumentException.ThrowIfNullOrWhiteSpace(
                    argument: project,
                    paramName: nameof(project));

                ArgumentNullException.ThrowIfNull(
                    argument: execution,
                    paramName: nameof(execution));

                // Require a Jira summary because Xray creates the Test Execution as an issue.
                if (string.IsNullOrWhiteSpace(execution.Summary))
                {
                    var message = "Test Execution summary cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(execution));
                }

                // Require at least one valid Test key so this operation always creates an executable manual cycle.
                if (execution.TestKeys is null || execution.TestKeys.Length == 0)
                {
                    var message = "Test Execution Test keys cannot be null or empty.";
                    throw new ArgumentException(message, nameof(execution));
                }

                if (execution.TestKeys.Any(string.IsNullOrWhiteSpace))
                {
                    var message = "Test Execution Test keys cannot contain null or whitespace values.";
                    throw new ArgumentException(message, nameof(execution));
                }

                // Reject null custom-field entries before field resolution accesses their names.
                var customFields = execution.CustomFields ?? [];

                if (customFields.Any(customField => customField is null))
                {
                    var message = "Test Execution custom fields cannot contain null entries.";
                    throw new ArgumentException(message, nameof(execution));
                }
            }
        }

        /// <inheritdoc />
        public object NewTest(string project, TestCaseModel testCase)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(
                    argument: testCase,
                    paramName: nameof(testCase));

                // Preserve the Jira description and resolved custom fields inside Xray's Jira creation payload.
                var additionalFields = new Dictionary<string, object>
                {
                    ["description"] = testCase.Description
                };

                // Normalize optional custom fields so resolution follows one linear collection workflow.
                testCase.CustomFields ??= [];

                // Resolve caller-facing names to Jira field identifiers while ignoring unavailable project fields.
                foreach (var customField in testCase.CustomFields)
                {
                    var resolvedField = _jiraClient.GetCustomField(project, customField.Name);

                    if (!string.IsNullOrWhiteSpace(resolvedField))
                    {
                        additionalFields[resolvedField] = customField.Value;
                    }
                }

                // Translate the complete ordered definition before sending so one mutation owns issue registration.
                var steps = new List<XrayTestStepInput>();
                testCase.Steps ??= [];

                foreach (var testStep in testCase.Steps)
                {
                    // Report invalid ordered entries against the owning public test-case parameter.
                    if (testStep is null)
                    {
                        var message = "Test case steps cannot contain null entries.";
                        throw new ArgumentException(message, nameof(testCase));
                    }

                    steps.Add(new XrayTestStepInput
                    {
                        Action = testStep.Action,
                        Result = string.Join('\n', testStep.ExpectedResults ?? [])
                    });
                }

                // Build one Xray creation request containing the Jira issue and complete manual definition.
                var request = new NewTestRequest
                {
                    Jira = new XrayJiraIssue
                    {
                        AdditionalFields = additionalFields,
                        ProjectKey = project,
                        Summary = testCase.Summary
                    },
                    Steps = steps,
                    TestTypeName = "Manual"
                };

                // Bridge the existing synchronous domain contract to the isolated GraphQL Test creation command.
                var graphQlClient = new XrayGraphQlClient(
                    AppSettings.HttpClient,
                    AppSettings.JiraOptions.XrayClientOptions);
                var commandsRepository = new XrayCommandsRepository(graphQlClient);
                var result = commandsRepository
                    .NewTestAsync(request)
                    .GetAwaiter()
                    .GetResult();

                // Preserve the established response shape while exposing non-fatal Xray creation diagnostics.
                var response = new
                {
                    Id = result.IssueId,
                    Key = result.Key,
                    Link = $"{jiraAuthentication.Collection}/browse/{result.Key}",
                    result.Warnings
                };

                // Serialize and detach the compatibility response before its backing document leaves scope.
                var jsonResponse = JsonSerializer.Serialize(response, AppSettings.JsonOptions);
                using var responseDocument = JsonDocument.Parse(jsonResponse);
                return responseDocument.RootElement.Clone();
            }
            catch (Exception exception)
            {
                // Return the established tool-friendly failure envelope without exposing credentials or bearer data.
                return new
                {
                    Error = exception.GetBaseException().Message,
                    Message = "Failed to create the Xray test and its Jira issue."
                };
            }
        }

        /// <inheritdoc />
        public object NewTestPlan(string project, NewTestPlanModel testPlan)
        {
            // Create the Test Plan issue in Jira/Xray via the shared issue-creation flow.
            // The returned payload is expected to include the created issue identifier.
            var (data, isSuccess) = NewIssue(
                _jiraClient,
                jiraAuthentication,
                project,
                issueType: "Test Plan",
                issueModel: testPlan);

            // If creation failed (or no JQL was provided), return the creation response as-is.
            // Test cases can still be applied later using a dedicated operation.
            if (!isSuccess || string.IsNullOrWhiteSpace(testPlan.Jql))
            {
                return data;
            }

            try
            {
                // Extract the created Test Plan ID from the response payload.
                // This ID is required for the follow-up operation that applies test cases to the plan.
                var testPlanId = data.GetProperty("id").GetString();

                // Apply test cases to the newly created Test Plan based on the provided JQL.
                // The JQL is executed in Jira, and the resulting issue IDs are sent to Xray
                // to associate those tests with the plan.
                AddTests(_jiraClient, _xpandClient, testPlanId, testPlan.Jql);
            }
            catch (Exception e)
            {
                // The Test Plan was created successfully, but applying test cases failed.
                // Return both the created plan payload and a user-friendly error message.
                return new
                {
                    Data = data,
                    Error = e.GetBaseException().Message,
                    Message = "Xray test plan was created, but an error occurred while adding test cases."
                };
            }

            // Return the created Test Plan payload (tests were applied successfully).
            return data;
        }

        /// <inheritdoc />
        public object NewTestRepositoryFolder(string idOrKey, string name, string path)
        {
            // Resolve the identifier of the parent folder based on the provided repository path.
            // This determines where the new folder will be created in the repository hierarchy.
            var parentId = _xpandClient.ResolveFolderPath(idOrKey, path);

            // Invoke the internal Xray endpoint to create the repository folder
            // under the resolved parent folder.
            var response = _xpandClient.NewTestRepositoryFolder(idOrKey, name, parentId);

            // Construct the resulting repository path for the newly created folder.
            // This path is returned for consumer visibility and confirmation.
            var outputPath = string.IsNullOrEmpty(path)
                ? $"/{name}"
                : $"/{path}/{name}";

            // Validate that the response contains the expected result object
            // and a folder identifier.
            var isResult = response.TryGetProperty("result", out JsonElement resultOut);
            var isId = resultOut.TryGetProperty("folderId", out JsonElement folderIdOut);

            // If the response does not contain a valid folder identifier,
            // treat the operation as a failure and raise a domain-specific exception.
            if (!isResult || !isId)
            {
                throw new XrayTestRepositoryFolderNotCreatedException(
                    message: $"Xray test repository folder was not created successfully at path {outputPath}."
                );
            }

            // TODO: Add links when supported by Xpand API or composed manually.
            // Return a minimal consumer-friendly representation of the created folder.
            return new
            {
                Id = folderIdOut.GetString(),
                Path = outputPath
            };
        }

        /// <inheritdoc />
        public string ResolveFolderPath(string idOrKey, string path)
        {
            return _xpandClient.ResolveFolderPath(idOrKey, path);

        }

        /// <inheritdoc />
        public object UpdateExecution(UpdateExecutionModel execution)
        {
            try
            {
                // Assert the complete sparse update before resolving either side of the composite Test Run identity.
                AssertArguments(execution);

                // Resolve both Jira keys before Xray lookup so the GraphQL query receives numeric issue identifiers.
                var executionIdentity = GetIssueIdentity(_jiraClient, execution.ExecutionKey);
                var testIdentity = GetIssueIdentity(_jiraClient, execution.TestKey);
                var commandsRepository = GetCommandsRepository();

                // Wait for the Test Run because Xray can register execution state after the association response.
                var testRun = commandsRepository
                    .WaitForTestRunAsync(new WaitForTestRunRequest
                    {
                        TestExecutionIssueId = executionIdentity.Id,
                        TestIssueId = testIdentity.Id
                    })
                    .GetAwaiter()
                    .GetResult();

                // Convert the caller's one-based step number into the ordered run-step snapshot.
                var step = testRun.Steps.ElementAtOrDefault(execution.StepNumber - 1);

                if (step is null)
                {
                    var message = $"Test Run '{testRun.Id}' contains {testRun.Steps.Count} manual steps; " +
                        $"step number {execution.StepNumber} is outside that range.";
                    throw new ArgumentOutOfRangeException(
                        nameof(execution),
                        execution.StepNumber,
                        message);
                }

                // Apply only caller-selected outcome values so omitted Test Run Step fields remain unchanged.
                var result = commandsRepository
                    .UpdateTestRunStepAsync(new UpdateTestRunStepRequest
                    {
                        IterationRank = execution.IterationRank,
                        StepId = step.Id,
                        TestRunId = testRun.Id,
                        Update = new XrayTestRunStepUpdate
                        {
                            ActualResult = execution.ActualResult,
                            Comment = execution.Comment,
                            Status = execution.Status
                        }
                    })
                    .GetAwaiter()
                    .GetResult();

                // Return every resolved identity and applied value so callers can continue the manual cycle safely.
                return new
                {
                    ExecutionId = executionIdentity.Id,
                    ExecutionKey = executionIdentity.Key,
                    TestId = testIdentity.Id,
                    TestKey = testIdentity.Key,
                    TestRunId = testRun.Id,
                    StepId = step.Id,
                    execution.StepNumber,
                    execution.Status,
                    execution.ActualResult,
                    execution.Comment,
                    execution.IterationRank,
                    result.Warnings
                };
            }
            catch (Exception exception)
            {
                // Return the established tool-friendly envelope while retaining the actionable execution failure.
                return new
                {
                    Error = exception.GetBaseException().Message,
                    Message = "Failed to update the Xray Test Run Step."
                };
            }

            // Asserts the composite identity, one-based step number, and sparse outcome before any remote work.
            // The helper does not mutate caller state and reports invalid nested values against the owning parameter.
            static void AssertArguments(UpdateExecutionModel execution)
            {
                // Require the declared request parameter before validating its nested execution values.
                ArgumentNullException.ThrowIfNull(
                    argument: execution,
                    paramName: nameof(execution));

                // Require both Jira keys because together they identify the Xray Test Run.
                if (string.IsNullOrWhiteSpace(execution.ExecutionKey))
                {
                    var message = "Execution update Test Execution key cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(execution));
                }

                if (string.IsNullOrWhiteSpace(execution.TestKey))
                {
                    var message = "Execution update Test key cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(execution));
                }

                // Require a one-based step number so selection matches the tool contract.
                if (execution.StepNumber <= 0)
                {
                    var message = "Execution update step number must be greater than zero.";
                    throw new ArgumentOutOfRangeException(
                        nameof(execution),
                        execution.StepNumber,
                        message);
                }

                // Require at least one sparse outcome value while retaining empty text as an explicit clear request.
                var hasActualResult = execution.ActualResult is not null;
                var hasComment = execution.Comment is not null;
                var hasStatus = execution.Status is not null;
                var hasUpdate = hasActualResult || hasComment || hasStatus;

                if (!hasUpdate)
                {
                    var message = "Execution update must select an actual result, comment, or status.";
                    throw new ArgumentException(message, nameof(execution));
                }

                if (hasStatus && string.IsNullOrWhiteSpace(execution.Status))
                {
                    var message = "Execution update status cannot be empty or whitespace.";
                    throw new ArgumentException(message, nameof(execution));
                }

                var isIterationRankInvalid = execution.IterationRank is not null &&
                    string.IsNullOrWhiteSpace(execution.IterationRank);

                if (isIterationRankInvalid)
                {
                    var message = "Execution update iteration rank cannot be empty or whitespace.";
                    throw new ArgumentException(message, nameof(execution));
                }
            }
        }

        /// <inheritdoc />
        public object UpdateTestRunStatus(UpdateTestRunStatusModel testRun)
        {
            try
            {
                // Assert the complete composite identity and status before resolving either Jira issue.
                AssertArguments(testRun);

                // Resolve caller-facing keys into the numeric Jira identities required for Test Run lookup.
                var executionIdentity = GetIssueIdentity(_jiraClient, testRun.ExecutionKey);
                var testIdentity = GetIssueIdentity(_jiraClient, testRun.TestKey);
                var commandsRepository = GetCommandsRepository();

                // Wait for Xray registration before applying the status through the opaque Test Run identity.
                var registeredTestRun = commandsRepository
                    .WaitForTestRunAsync(new WaitForTestRunRequest
                    {
                        TestExecutionIssueId = executionIdentity.Id,
                        TestIssueId = testIdentity.Id
                    })
                    .GetAwaiter()
                    .GetResult();

                // Apply the requested status and retain the scalar value confirmed by Xray's mutation response.
                var status = commandsRepository
                    .UpdateTestRunStatusAsync(new UpdateTestRunStatusRequest
                    {
                        Status = testRun.Status,
                        TestRunId = registeredTestRun.Id
                    })
                    .GetAwaiter()
                    .GetResult();

                // Return every resolved identity so the execution journal can persist a complete sync receipt.
                return new
                {
                    ExecutionId = executionIdentity.Id,
                    ExecutionKey = executionIdentity.Key,
                    TestId = testIdentity.Id,
                    TestKey = testIdentity.Key,
                    TestRunId = registeredTestRun.Id,
                    Status = status
                };
            }
            catch (Exception exception)
            {
                // Convert integration failures into the stable error envelope consumed by MCP callers.
                return new
                {
                    Error = exception.GetBaseException().Message,
                    Message = "Failed to update the Xray Test Run status."
                };
            }

            // Asserts the composite Jira identity and status before the parent method performs any remote work.
            // The helper preserves caller state and reports nested failures against the owning Test Run parameter.
            static void AssertArguments(UpdateTestRunStatusModel testRun)
            {
                // Require the root request before reading its Test Execution, Test, or status values.
                ArgumentNullException.ThrowIfNull(
                    argument: testRun,
                    paramName: nameof(testRun));

                // Require both Jira keys because together they identify one Xray Test Run.
                if (string.IsNullOrWhiteSpace(testRun.ExecutionKey))
                {
                    var message = "Test Run status ExecutionKey cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(testRun));
                }

                if (string.IsNullOrWhiteSpace(testRun.TestKey))
                {
                    var message = "Test Run status TestKey cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(testRun));
                }

                // Require a meaningful Xray status name or identifier before constructing mutation state.
                if (string.IsNullOrWhiteSpace(testRun.Status))
                {
                    var message = "Test Run status value cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(testRun));
                }
            }
        }

        /// <inheritdoc />
        public object UpdateTest(string key, TestCaseModel testCase)
        {
            // Retrieve the existing Xray test case using its Jira issue key.
            // The response is expected to include both the internal test identifier
            // and the currently defined test steps.
            var test = _xpandClient.GetTestCase(idOrKey: key);

            // Extract the internal test identifier required for step-level operations.
            var id = test.GetProperty("id").GetString();

            // Enumerate the existing test steps so they can be removed
            // before recreating the updated step set.
            var steps = test.GetProperty("steps").EnumerateArray();

            // Configure parallel execution behavior based on the configured bucket size.
            // A bucket size of zero forces sequential execution to ensure safe, deterministic behavior.
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = AppSettings.JiraOptions.BucketSize == 0
                    ? 1
                    : AppSettings.JiraOptions.BucketSize
            };

            // Remove all existing Xray test steps using parallel execution.
            // Each step is removed independently, allowing faster cleanup while
            // respecting the configured concurrency limits.
            Parallel.ForEach(steps, parallelOptions, step =>
            {
                // Extract the unique identifier of the test step.
                // This identifier is required to remove the step from Xray.
                var stepId = step.GetProperty("id").GetString();

                // Remove the test step from the Xray test case.
                // Failures here will surface according to the caller’s error-handling strategy.
                _xpandClient.RemoveTestStep((id, key), stepId);
            });

            // Build a direct browser link to the Jira issue representing the test.
            var link = $"{jiraAuthentication.Collection}/browse/{key}";

            // Recreate the test steps sequentially while the standalone client owns authentication and retries.
            NewTestSteps(id, key, testCase);

            // Return a minimal consumer-friendly representation of the updated test.
            // This avoids exposing raw Xray or Jira payloads while preserving key identifiers.
            return new
            {
                Id = id,
                Key = key,
                Link = link
            };
        }

        // Adds test cases selected by a JQL query to the specified Xray Test Plan.
        // This method encapsulates the full lifecycle of querying Jira for test cases
        // and associating them with the Test Plan using Xray internal endpoints.
        private static object AddTests(
            JiraClient jiraClient,
            XpandClient xpandClient,
            string testPlanId,
            string jql)
        {
            // Query Jira using the provided JQL to determine which test cases
            // should be applied to the specified Test Plan.
            var issues = jiraClient.GetIssues(jql);

            // Extract Jira issue IDs from the result set.
            // Only non-empty IDs are included in the final request payload.
            var issueIds = issues
                .Select(i => i.TryGetProperty("id", out JsonElement idOut) ? idOut.GetString() : string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray();

            // Create the domain exception used by the retry wrapper.
            // This represents a failure to apply one or more test cases to the Test Plan.
            var applyException = new XrayTestCaseNotAppliedException(
                message: "Failed to apply one or more test cases to the Xray Test Plan."
            );

            // Retrieve the Jira issue key for the Test Plan.
            // Xray internal endpoints typically require this key as part of request validation headers.
            var planMetadata = jiraClient.GetIssue(idOrKey: testPlanId, fields: ["key", "id"]);

            // Extract the plan key and id from the retrieved metadata.
            var key = planMetadata.GetProperty("key").GetString();
            var id = planMetadata.GetProperty("id").GetString();

            // Send the Xray internal command to associate the selected test cases with the Test Plan.
            // Transient integration failures are retried before the domain exception is propagated.
            return InvokeRepeatableRequest(
                exception: applyException,
                func: () =>
                {
                    return xpandClient.AddTestsToPlan(
                        testPlan: (id, key),
                        testIds: issueIds);
                }
            );
        }

        // Creates the public GraphQL command repository used for Test Execution and Test Run mutations.
        // The helper owns no disposable state because the application supplies the shared HTTP client lifecycle.
        private static XrayCommandsRepository GetCommandsRepository()
        {
            var graphQlClient = new XrayGraphQlClient(
                AppSettings.HttpClient,
                AppSettings.JiraOptions.XrayClientOptions);
            return new XrayCommandsRepository(graphQlClient);
        }

        // Resolves one Jira issue key or identifier into the numeric ID required by public Xray GraphQL operations.
        // The helper performs read-only Jira I/O and reports an incomplete identity without mutating caller state.
        private static (string Id, string Key) GetIssueIdentity(
            JiraClient jiraClient,
            string idOrKey)
        {
            // Require the lookup value before Jira I/O so malformed execution requests fail locally.
            ArgumentException.ThrowIfNullOrWhiteSpace(
                argument: idOrKey,
                paramName: nameof(idOrKey));

            // Request only identity metadata because execution commands do not consume Jira issue fields.
            var issue = jiraClient.GetIssue(idOrKey, "id", "key");
            var hasId = issue.TryGetProperty("id", out var idElement);
            var hasKey = issue.TryGetProperty("key", out var keyElement);
            var id = hasId ? idElement.GetString() : string.Empty;
            var key = hasKey ? keyElement.GetString() : string.Empty;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(key))
            {
                var message = $"Jira issue '{idOrKey}' did not return a complete numeric ID and key identity.";
                throw new KeyNotFoundException(message);
            }

            return (id, key);
        }

        // Executes the provided action repeatedly until it succeeds or the retry limit is reached.
        private static object InvokeRepeatableRequest(Exception exception, Func<object> func)
        {
            // Retrieve retry configuration from application settings.
            var maxAttempts = AppSettings.JiraOptions.RetryOptions.MaxAttempts;
            var delayMilliseconds = AppSettings.JiraOptions.RetryOptions.DelayMilliseconds;
            object response = null;

            // Attempt to execute the action up to the configured retry count.
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    // Execute the action and immediately return the result on success.
                    response = func();

                    var jsonResponse = (JsonElement)response;
                    var isCode = jsonResponse.TryGetProperty("code", out JsonElement code);

                    if (!isCode || code.GetInt16() < 400)
                    {
                        return response;
                    }

                    Thread.Sleep(delayMilliseconds);
                }
                catch
                {
                    // Swallow the exception and continue to the next retry attempt.
                    // The final exception will be thrown only after all retries are exhausted.
                    // Wait for the configured delay before the next retry attempt.
                    Thread.Sleep(delayMilliseconds);
                }
            }

            // All retry attempts have failed, so propagate the provided exception
            // to signal an unrecoverable failure to the caller.
            return response;
        }

        // Creates a new Jira issue using the provided parameters and returns a minimal representation.
        // This method encapsulates the full lifecycle of issue creation, including request building,
        // validation, and error handling.
        private static (JsonElement Data, bool IsSuccess) NewIssue(
            JiraClient jiraClient,
            JiraAuthenticationModel jiraAuthentication,
            string project,
            string issueType,
            NewIssueModelBase issueModel)
        {
            // Normalize all values required for creating the base Jira issue into a single options model.
            // This keeps the request builder isolated from the shape of the incoming domain model.
            var options = new IssueCreateOptions
            {
                Context = issueModel.Context,
                CustomFields = issueModel.CustomFields,
                Description = issueModel.Description,
                IssueType = issueType,
                Project = project,
                Summary = issueModel.Summary
            };

            // Build the Jira issue creation request payload, including custom fields when enabled.
            var testIssue = NewIssueRequest(jiraClient, options);

            // Define a domain-specific exception that represents an incomplete Jira issue creation result.
            // This exception is reused across retries to keep error semantics consistent.
            var issueException = new JiraIssueNotCreatedException(
                message: "Jira issue was not created successfully. The response did not contain an issue id or key."
            );

            // Attempt to create the Jira issue with retry semantics to handle transient Jira failures.
            // The invocation validates that the response contains both id and key before accepting success.
            var jiraResponse = (JsonElement)InvokeRepeatableRequest(
                exception: issueException,
                func: () =>
                {
                    // Create the Jira issue and capture the raw JSON response.
                    return jiraClient.NewIssue(testIssue);
                }
            );

            // Validate that the Jira response contains the expected issue key property.
            var isKey = jiraResponse.TryGetProperty("key", out JsonElement key);

            // If the issue key is missing, treat the operation as a failure and return the raw response.
            if (!isKey)
            {
                return (jiraResponse, false);
            }

            // Extract the created issue key and id from the validated Jira response.
            // These are stored as JsonElement values and converted to strings only when needed.
            var id = jiraResponse.GetProperty("id").GetString();

            // Build a direct browser link to the newly created Jira issue.
            var link = $"{jiraAuthentication.Collection}/browse/{key}";

            // Return a minimal consumer-friendly representation of the created test.
            // This avoids leaking the full Jira response while still providing key identifiers.
            var response = new
            {
                Id = id,
                Key = key,
                Link = link
            };

            // Serialize the response using the configured JSON options for consistency.
            var jsonResponse = JsonSerializer.Serialize(response, AppSettings.JsonOptions);

            // Parse and return the serialized response as a JsonElement.
            return (JsonDocument.Parse(jsonResponse).RootElement, true);
        }

        // Builds a Jira issue creation request payload using normalized issue creation options
        // and the current Jira configuration.
        private static Dictionary<string, object> NewIssueRequest(
            JiraClient jiraClient,
            IssueCreateOptions options)
        {
            // Determine whether custom field resolution is enabled in configuration.
            // When disabled, only the base issue fields are included in the request.
            var resolveCustomFields = AppSettings.JiraOptions.ResolveCustomFields;

            // Resolve the issue type to be used for the request.
            // A non-empty value provided in the context takes precedence over the default option.
            var testCaseType =
                options.Context.TryGetValue("IssueType", out object value) &&
                !string.IsNullOrEmpty($"{value}")
                    ? value
                    : options.IssueType;

            // Build the base Jira issue request with standard fields populated
            // from the normalized issue creation options.
            var baseRequest = new Dictionary<string, object>
            {
                ["fields"] = new Dictionary<string, object>
                {
                    ["description"] = options.Description,
                    ["issuetype"] = new Dictionary<string, object>
                    {
                        ["name"] = testCaseType
                    },
                    ["project"] = new Dictionary<string, object>
                    {
                        ["key"] = options.Project
                    },
                    ["summary"] = options.Summary
                }
            };

            // If custom field resolution is disabled, or no custom fields are provided,
            // return the base request without modification.
            if (!resolveCustomFields || options.CustomFields is null || options.CustomFields.Length == 0)
            {
                return baseRequest;
            }

            // Iterate through the custom fields and attempt to resolve each schema
            // to its corresponding Jira field identifier.
            foreach (var item in options.CustomFields)
            {
                // Extract the schema identifier for the custom field.
                var schema = item.Name;

                // Resolve the Jira field name for the given schema within the target project.
                var resolvedField = jiraClient.GetCustomField(options.Project, schema);

                // Only include the field in the request when resolution succeeds.
                if (resolvedField is not null)
                {
                    ((Dictionary<string, object>)baseRequest["fields"])[resolvedField] = item.Value;
                }
            }

            // Return the fully constructed Jira issue creation request payload.
            return baseRequest;
        }

        // Creates all Xray test steps through the standalone GraphQL client while preserving their source order.
        private static void NewTestSteps(
            string id,
            string key,
            TestCaseModel testCase)
        {
            // Avoid validating or authenticating the Xray client when the Test contains no manual steps.
            if (testCase.Steps.Length == 0)
            {
                return;
            }

            // Create one transport client for the complete batch so authentication and retry state span every step.
            var graphQlClient = new XrayGraphQlClient(
                AppSettings.HttpClient,
                AppSettings.JiraOptions.XrayClientOptions);
            var commandsRepository = new XrayCommandsRepository(graphQlClient);

            // Send steps sequentially because the add-step mutation appends each result to the current Test version.
            for (int i = 0; i < testCase.Steps.Length; i++)
            {
                var step = testCase.Steps[i];

                // Translate the domain step into the standalone command contract without adding synthetic test data.
                var request = new AddTestStepRequest
                {
                    IssueId = id,
                    Step = new XrayTestStepInput
                    {
                        Action = step.Action,
                        Result = string.Join('\n', step.ExpectedResults)
                    }
                };

                try
                {
                    // Bridge the existing synchronous repository contract to the client's async mutation lifecycle.
                    commandsRepository
                        .AddTestStepAsync(request)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception exception)
                {
                    // Add the Test key and source index while retaining the actionable GraphQL failure as context.
                    throw new XrayTestStepNotCreatedException(
                        message: $"Xray test step was not created successfully for test {key} at index {i}.",
                        innerException: exception);
                }
            }
        }

        // Waits for every requested Test Run after association so downstream execution writes never race registration.
        // The helper preserves Test ordering, performs no caller-state mutation, and propagates bounded polling failures.
        private static List<XrayTestRunResult> WaitForTestRuns(
            XrayCommandsRepository commandsRepository,
            string executionIssueId,
            IReadOnlyCollection<(string Id, string Key)> testIdentities)
        {
            var testRuns = new List<XrayTestRunResult>(testIdentities.Count);

            // Poll each composite identity independently because Xray can register Tests at different times.
            foreach (var testIdentity in testIdentities)
            {
                var testRun = commandsRepository
                    .WaitForTestRunAsync(new WaitForTestRunRequest
                    {
                        TestExecutionIssueId = executionIssueId,
                        TestIssueId = testIdentity.Id
                    })
                    .GetAwaiter()
                    .GetResult();

                // Preserve caller ordering so returned Test Run IDs align with the resolved Test key collection.
                testRuns.Add(testRun);
            }

            return testRuns;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents a normalized set of options used to construct
        /// a Jira issue creation request.
        /// This model acts as an internal aggregation of issue-related data
        /// that may originate from multiple sources, such as test case definitions
        /// and runtime context, before being translated into a Jira API payload.
        /// </summary>
        private sealed class IssueCreateOptions
        {
            /// <summary>
            /// Gets or sets the contextual values that influence issue creation behavior.
            /// </summary>
            public IDictionary<string, object> Context { get; set; }

            /// <summary>
            /// Gets or sets the custom field values associated with the issue.
            /// </summary>
            public CustomFieldModel[] CustomFields { get; set; }

            /// <summary>
            /// Gets or sets the textual description of the issue.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Gets or sets the Jira issue type name to be used during creation.
            /// </summary>
            public string IssueType { get; set; }

            /// <summary>
            /// Gets or sets the Jira project key under which the issue will be created.
            /// </summary>
            public string Project { get; set; }

            /// <summary>
            /// Gets or sets the short summary or title of the issue.
            /// </summary>
            public string Summary { get; set; }
        }
        #endregion
    }
}
