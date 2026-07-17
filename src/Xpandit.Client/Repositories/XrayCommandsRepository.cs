using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Xpandit.Client;
using Xpandit.Client.Exceptions;
using Xpandit.Client.Internal;
using Xpandit.Client.Models;

namespace Xpandit.Client.Repositories
{
    /// <summary>
    /// Executes typed Xray Cloud GraphQL commands for issue creation, manual steps, and Test Repository folders.
    /// </summary>
    /// <remarks>
    /// The repository owns command validation and response mapping. Its <see cref="XrayGraphQlClient"/> instance
    /// owns token and retry state, while the caller retains ownership of the supplied <see cref="HttpClient"/> and
    /// its lifetime.
    /// </remarks>
    public class XrayCommandsRepository : IXrayCommandsRepository
    {
        #region *** Constants    ***
        private static readonly Regex JiraIssueKeyPattern = new(
            "^[A-Za-z][A-Za-z0-9_]*-[1-9][0-9]*$",
            RegexOptions.CultureInvariant);
        #endregion

        #region *** Fields       ***
        private readonly XrayGraphQlClient _client;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a standalone Xray commands repository with caller-owned HTTP infrastructure.
        /// </summary>
        /// <param name="httpClient">Reusable HTTP client retained by the caller for the repository lifetime.</param>
        /// <param name="options">Xray credentials, public endpoints, and repeatable-send configuration.</param>
        /// <exception cref="ArgumentException">Thrown when required credentials or endpoints are invalid.</exception>
        public XrayCommandsRepository(HttpClient httpClient, XrayClientOptions options)
        {
            // Delegate transport validation and state ownership to one internal client for every command.
            _client = new XrayGraphQlClient(httpClient, options);
        }

        /// <summary>
        /// Initializes an Xray commands repository over an existing token-owning GraphQL client.
        /// </summary>
        /// <param name="client">Shared GraphQL client that owns authentication and repeatable-send state.</param>
        /// <remarks>
        /// This overload lets multiple typed command flows reuse one authenticated client without transferring
        /// ownership of the caller-supplied HTTP infrastructure.
        /// </remarks>
        public XrayCommandsRepository(XrayGraphQlClient client)
        {
            ArgumentNullException.ThrowIfNull(client);

            // Retain the caller-owned client so every command shares its cached token and retry lifecycle.
            _client = client;
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public async Task<XrayTestStepResult> AddTestStepAsync(
            AddTestStepRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Step);

            // Validate the numeric Test identity before authentication so invalid local input causes no remote work.
            ConfirmNumericId(request.IssueId, nameof(request.IssueId));

            // Include only meaningful optional values so Xray applies its default Test version and field semantics.
            var variables = new Dictionary<string, object>
            {
                ["issueId"] = request.IssueId,
                ["step"] = GetStepInput(request.Step)
            };

            if (request.VersionId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    "VersionId cannot be negative.");
            }

            if (request.VersionId > 0)
            {
                variables["versionId"] = request.VersionId;
            }

            // Execute the public mutation through the shared token and repeatable-send lifecycle.
            var data = await _client.InvokeAsync(
                operationName: "AddTestStep",
                query: XrayGraphQlDocuments.AddTestStep,
                variables,
                cancellationToken).ConfigureAwait(false);

            // Map the persisted step so callers can use its returned identifier in later updates.
            var stepElement = GetRequiredProperty(data, "addTestStep", "AddTestStep");
            return GetTestStepResult(stepElement);
        }

        /// <inheritdoc />
        public async Task<XrayFolder> GetFoldersAsync(
            GetFoldersRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Normalize and validate repository context before querying the recursive testing tree.
            ConfirmNumericId(request.ProjectId, nameof(request.ProjectId));
            var path = GetNormalizedPath(request.Path, allowRoot: true);

            // Preserve a null response for absent paths so callers can decide whether to create them.
            return await GetFoldersCoreAsync(
                instance: this,
                request.ProjectId,
                path,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<XrayTestRunResult> GetTestRunAsync(
            GetTestRunRequest request)
        {
            // Route convenience callers through the cancellation-aware operation with no external cancellation.
            return await GetTestRunAsync(
                request,
                cancellationToken: default).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<XrayTestRunResult> GetTestRunAsync(
            GetTestRunRequest request,
            CancellationToken cancellationToken)
        {
            // Assert both issue identities before authentication so invalid local input causes no remote work.
            AssertArguments(request);

            // Preserve the composite Test and Test Execution identity expected by Xray's Test Run lookup.
            var variables = new
            {
                testIssueId = request.TestIssueId,
                testExecutionIssueId = request.TestExecutionIssueId
            };

            // Resolve the execution snapshot through the shared authentication and repeatable-send lifecycle.
            var data = await _client.InvokeAsync(
                operationName: "GetTestRun",
                query: XrayGraphQlDocuments.GetTestRun,
                variables,
                cancellationToken).ConfigureAwait(false);

            // Distinguish an absent Test Run from an incompatible GraphQL response so callers can report not-found state.
            if (!data.TryGetProperty("getTestRun", out var testRunElement) ||
                testRunElement.ValueKind == JsonValueKind.Null)
            {
                var message = $"No Xray Test Run was found for Test '{request.TestIssueId}' " +
                    $"inside Test Execution '{request.TestExecutionIssueId}'.";
                throw new KeyNotFoundException(message);
            }

            if (testRunElement.ValueKind != JsonValueKind.Object)
            {
                throw new XpanditClientException("Xray operation 'GetTestRun' returned an invalid Test Run response.");
            }

            // Map the detached execution snapshot so later mutations can use its opaque run and step identifiers.
            return GetTestRunResult(testRunElement);

            // Asserts the composite numeric identity before the parent method allocates GraphQL request state.
            // The helper does not mutate caller data and reports invalid values through argument exceptions.
            static void AssertArguments(GetTestRunRequest request)
            {
                // Require the request before accessing the two issue identifiers that form the lookup identity.
                ArgumentNullException.ThrowIfNull(
                    argument: request,
                    paramName: nameof(request));

                // Require positive numeric Jira identifiers because Xray does not accept human-readable keys here.
                ConfirmNumericId(
                    request.TestExecutionIssueId,
                    nameof(request.TestExecutionIssueId));
                ConfirmNumericId(
                    request.TestIssueId,
                    nameof(request.TestIssueId));
            }
        }

        /// <inheritdoc />
        public async Task<XrayCommandResult> MoveTestToFolderAsync(
            MoveTestToFolderRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Validate and normalize both parts before changing the Test's single repository location.
            ConfirmNumericId(request.IssueId, nameof(request.IssueId));
            var path = GetNormalizedPath(request.Path, allowRoot: true);
            var variables = new
            {
                issueId = request.IssueId,
                folderPath = path
            };

            // Xray returns the updated path as a scalar, so successful completion is the observable command result.
            await _client.InvokeAsync(
                operationName: "MoveTestToFolder",
                query: XrayGraphQlDocuments.MoveTestToFolder,
                variables,
                cancellationToken).ConfigureAwait(false);

            return new XrayCommandResult();
        }

        /// <inheritdoc />
        public async Task<XrayFolder> NewFolderAsync(
            NewFolderRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Normalize the complete destination once so discovery and every cumulative mutation share one path form.
            ConfirmNumericId(request.ProjectId, nameof(request.ProjectId));
            var targetPath = GetNormalizedPath(request.Path, allowRoot: true);

            // Read the full tree once so existing segments are preserved without issuing redundant create mutations.
            var rootFolder = await GetFoldersCoreAsync(
                instance: this,
                request.ProjectId,
                path: "/",
                cancellationToken).ConfigureAwait(false);

            if (rootFolder is null)
            {
                var message = $"Xray returned no Test Repository root for project '{request.ProjectId}'.";
                throw new XpanditClientException(message);
            }

            if (targetPath == "/")
            {
                return rootFolder;
            }

            // Index every path in Xray's JSON tree so only missing cumulative segments are created.
            var existingPaths = new HashSet<string>(StringComparer.Ordinal)
            {
                "/",
                rootFolder.Path
            };
            AddFolderPaths(rootFolder.Folders, existingPaths);

            foreach (var candidatePath in GetCumulativePaths(targetPath))
            {
                if (existingPaths.Contains(candidatePath))
                {
                    continue;
                }

                // Create parents before children so each mutation targets a valid repository hierarchy.
                await NewFolderCoreAsync(
                    instance: this,
                    request.ProjectId,
                    candidatePath,
                    cancellationToken).ConfigureAwait(false);
                existingPaths.Add(candidatePath);
            }

            // Re-read the leaf after all mutations so the returned counts and child data reflect server state.
            var folder = await GetFoldersCoreAsync(
                instance: this,
                request.ProjectId,
                targetPath,
                cancellationToken).ConfigureAwait(false);

            return folder ?? throw new XpanditClientException(
                $"Xray did not return folder '{targetPath}' after creating its missing path segments.");
        }

        /// <inheritdoc />
        public async Task<XrayCreatedTestResult> NewTestAsync(
            NewTestRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(argument: request, paramName: nameof(request));

            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Jira);
            ArgumentNullException.ThrowIfNull(request.Steps);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.TestTypeName);

            // Normalize the Test type and every sparse step before entering the remote creation lifecycle.
            var testTypeName = request.TestTypeName.Trim();
            var steps = new List<object>(request.Steps.Count);

            foreach (var step in request.Steps)
            {
                ArgumentNullException.ThrowIfNull(step);
                steps.Add(GetStepInput(step));
            }

            var variables = new Dictionary<string, object>
            {
                ["jira"] = GetJiraInput(request.Jira),
                ["steps"] = steps,
                ["testType"] = new Dictionary<string, object>
                {
                    ["name"] = testTypeName
                }
            };

            // Let Xray create the Jira issue and register its Test definition within one mutation boundary.
            var data = await _client.InvokeAsync(
                operationName: "NewTest",
                query: XrayGraphQlDocuments.NewTest,
                variables,
                cancellationToken).ConfigureAwait(false);

            // Require the complete identity and definition so a partial GraphQL payload never reports success.
            var payload = GetRequiredProperty(data, "createTest", "NewTest");
            var test = GetRequiredProperty(payload, "test", "NewTest");
            var result = GetCreatedIssueResult(payload, "test", "NewTest");

            if (string.IsNullOrWhiteSpace(result.Key))
            {
                throw new XpanditClientException("Xray operation 'NewTest' did not return a Jira issue key.");
            }

            var testType = GetRequiredProperty(test, "testType", "NewTest");
            var returnedTestTypeName = GetOptionalString(testType, "name") ?? string.Empty;

            if (!string.Equals(returnedTestTypeName, testTypeName, StringComparison.OrdinalIgnoreCase))
            {
                throw new XpanditClientException(
                    $"Xray operation 'NewTest' returned Test type '{returnedTestTypeName}' instead of '{testTypeName}'.");
            }

            var persistedSteps = GetTestStepResults(test, "NewTest");

            if (persistedSteps.Count != steps.Count)
            {
                var message = $"Xray operation 'NewTest' " +
                    $"returned {persistedSteps.Count} steps after receiving {steps.Count}.";
                throw new XpanditClientException(message);
            }

            // Return warnings with the confirmed Test so callers retain non-fatal Xray diagnostics.
            return new XrayCreatedTestResult
            {
                IssueId = result.IssueId,
                Key = result.Key,
                Steps = persistedSteps,
                TestTypeName = returnedTestTypeName,
                Warnings = result.Warnings
            };
        }

        /// <inheritdoc />
        public async Task<XrayTestExecutionResult> NewTestExecutionAsync(
            NewTestExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Jira);

            // Validate optional associations independently so invalid values never create a partial Jira issue.
            var testIssueIds = GetOptionalNumericIds(request.TestIssueIds, nameof(request.TestIssueIds));
            var testEnvironments = GetOptionalStrings(
                request.TestEnvironments,
                nameof(request.TestEnvironments));
            var jira = GetJiraInput(request.Jira);
            var variables = new Dictionary<string, object>
            {
                ["jira"] = jira
            };
            AddOptionalCollection(variables, "testIssueIds", testIssueIds);
            AddOptionalCollection(variables, "testEnvironments", testEnvironments);

            // Create the execution and its requested associations in one Xray transaction.
            var data = await _client.InvokeAsync(
                operationName: "NewTestExecution",
                query: XrayGraphQlDocuments.NewTestExecution,
                variables,
                cancellationToken).ConfigureAwait(false);

            // Map execution-specific environments in addition to the shared Jira identity and warnings.
            var payload = GetRequiredProperty(data, "createTestExecution", "NewTestExecution");
            var result = GetCreatedIssueResult(payload, "testExecution", "NewTestExecution");
            return new XrayTestExecutionResult
            {
                CreatedTestEnvironments = GetStringCollection(payload, "createdTestEnvironments"),
                IssueId = result.IssueId,
                Key = result.Key,
                Warnings = result.Warnings
            };
        }

        /// <inheritdoc />
        public async Task<XrayCreatedIssueResult> NewTestPlanAsync(
            NewTestPlanRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Jira);

            // Prepare validated Jira fields and optional Test associations before entering the remote lifecycle.
            var testIssueIds = GetOptionalNumericIds(request.TestIssueIds, nameof(request.TestIssueIds));
            var variables = new Dictionary<string, object>
            {
                ["jira"] = GetJiraInput(request.Jira)
            };
            AddOptionalCollection(variables, "testIssueIds", testIssueIds);

            // Create the Test Plan with its initial Tests through the public Xray mutation.
            var data = await _client.InvokeAsync(
                operationName: "NewTestPlan",
                query: XrayGraphQlDocuments.NewTestPlan,
                variables,
                cancellationToken).ConfigureAwait(false);

            var payload = GetRequiredProperty(data, "createTestPlan", "NewTestPlan");
            return GetCreatedIssueResult(payload, "testPlan", "NewTestPlan");
        }

        /// <inheritdoc />
        public async Task<XrayCreatedIssueResult> NewTestSetAsync(
            NewTestSetRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Jira);

            // Prepare validated Jira fields and optional Test associations before entering the remote lifecycle.
            var testIssueIds = GetOptionalNumericIds(request.TestIssueIds, nameof(request.TestIssueIds));
            var variables = new Dictionary<string, object>
            {
                ["jira"] = GetJiraInput(request.Jira)
            };
            AddOptionalCollection(variables, "testIssueIds", testIssueIds);

            // Create the Test Set with its initial Tests through the public Xray mutation.
            var data = await _client.InvokeAsync(
                operationName: "NewTestSet",
                query: XrayGraphQlDocuments.NewTestSet,
                variables,
                cancellationToken).ConfigureAwait(false);

            var payload = GetRequiredProperty(data, "createTestSet", "NewTestSet");
            return GetCreatedIssueResult(payload, "testSet", "NewTestSet");
        }

        /// <inheritdoc />
        public async Task<string> ResolveTestIssueIdAsync(
            string issueKey,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(issueKey);
            issueKey = issueKey.Trim();

            if (!JiraIssueKeyPattern.IsMatch(issueKey))
            {
                throw new ArgumentException(
                    "The Jira issue key must contain a project key and positive issue number.",
                    nameof(issueKey));
            }

            // Pass JQL as a GraphQL variable so the issue key never changes the query document structure.
            var variables = new
            {
                jql = $"key = \"{issueKey}\""
            };
            var data = await _client.InvokeAsync(
                operationName: "ResolveTestIssueId",
                query: XrayGraphQlDocuments.ResolveTestIssueId,
                variables,
                cancellationToken).ConfigureAwait(false);

            // Require exactly one matching Test because command routing is unsafe when resolution is ambiguous.
            var testResults = GetRequiredProperty(data, "getTests", "ResolveTestIssueId");
            var total = GetOptionalInt32(testResults, "total");

            if (total == 0 ||
                !testResults.TryGetProperty("results", out var resultsElement) ||
                resultsElement.ValueKind != JsonValueKind.Array ||
                resultsElement.GetArrayLength() == 0)
            {
                throw new KeyNotFoundException($"No Xray Test was found for Jira issue key '{issueKey}'.");
            }

            if (total > 1 || resultsElement.GetArrayLength() > 1)
            {
                throw new InvalidOperationException(
                    $"More than one Xray Test was returned for Jira issue key '{issueKey}'.");
            }

            var issueId = GetOptionalString(resultsElement[0], "issueId");
            var resolvedIssueId = issueId ?? string.Empty;
            ConfirmNumericId(resolvedIssueId, "ResolvedIssueId");
            return resolvedIssueId;
        }

        /// <inheritdoc />
        public async Task<XrayCommandResult> UpdateTestRunStepAsync(
            UpdateTestRunStepRequest request)
        {
            // Route convenience callers through the cancellation-aware operation with no external cancellation.
            return await UpdateTestRunStepAsync(
                request,
                cancellationToken: default).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<XrayCommandResult> UpdateTestRunStepAsync(
            UpdateTestRunStepRequest request,
            CancellationToken cancellationToken)
        {
            // Assert the complete run-step identity before preparing sparse mutation data.
            AssertArguments(request);

            // Convert only caller-selected outcome fields so omitted values remain unchanged in Xray.
            var variables = new Dictionary<string, object>
            {
                ["stepId"] = request.StepId,
                ["testRunId"] = request.TestRunId,
                ["updateData"] = GetTestRunStepUpdate(request.Update)
            };

            if (request.IterationRank is not null)
            {
                // Include iteration context only for data-driven runs so ordinary manual steps use the base snapshot.
                variables["iterationRank"] = request.IterationRank;
            }

            // Apply the selected execution values through Xray's public Test Run Step mutation.
            var data = await _client.InvokeAsync(
                operationName: "UpdateTestRunStep",
                query: XrayGraphQlDocuments.UpdateTestRunStep,
                variables,
                cancellationToken).ConfigureAwait(false);
            var payload = GetRequiredProperty(data, "updateTestRunStep", "UpdateTestRunStep");

            // Retain non-fatal Xray diagnostics while the caller continues the manual execution cycle.
            return new XrayCommandResult
            {
                Warnings = GetStringCollection(payload, "warnings")
            };

            // Asserts all required run-step values before the parent method enters the remote mutation lifecycle.
            // The helper preserves caller state and reports invalid identities or iteration values locally.
            static void AssertArguments(UpdateTestRunStepRequest request)
            {
                // Require the request and sparse update contract before accessing mutation values.
                ArgumentNullException.ThrowIfNull(
                    argument: request,
                    paramName: nameof(request));

                ArgumentNullException.ThrowIfNull(
                    argument: request.Update,
                    paramName: nameof(request.Update));

                // Require Xray's opaque run and step identifiers so the mutation cannot target an unknown entity.
                ArgumentException.ThrowIfNullOrWhiteSpace(
                    argument: request.StepId,
                    paramName: nameof(request.StepId));

                ArgumentException.ThrowIfNullOrWhiteSpace(
                    argument: request.TestRunId,
                    paramName: nameof(request.TestRunId));

                if (request.IterationRank is not null)
                {
                    // Reject an empty iteration selector because omission and an invalid rank have different meaning.
                    ArgumentException.ThrowIfNullOrWhiteSpace(
                        argument: request.IterationRank,
                        paramName: nameof(request.IterationRank));
                }
            }
        }

        /// <inheritdoc />
        public async Task<XrayCommandResult> UpdateTestStepAsync(
            UpdateTestStepRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Step);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.StepId);

            // Convert only caller-selected fields so omitted values remain unchanged in Xray.
            var variables = new
            {
                stepId = request.StepId,
                step = GetStepUpdate(request.Step)
            };

            // Apply the partial mutation and retain non-fatal warnings for caller diagnostics.
            var data = await _client.InvokeAsync(
                operationName: "UpdateTestStep",
                query: XrayGraphQlDocuments.UpdateTestStep,
                variables,
                cancellationToken).ConfigureAwait(false);
            var payload = GetRequiredProperty(data, "updateTestStep", "UpdateTestStep");

            return new XrayCommandResult
            {
                Warnings = GetStringCollection(payload, "warnings")
            };
        }

        // Recursively scans Xray's schema-free child JSON and indexes every documented path value.
        private static void AddFolderPaths(JsonElement element, ISet<string> paths)
        {
            if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return;
            }

            var value = element;

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in value.EnumerateArray())
                {
                    AddFolderPaths(child, paths);
                }

                return;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            // Capture a folder path when present, then continue through every property for version-tolerant traversal.
            if (value.TryGetProperty("path", out var pathElement) &&
                pathElement.ValueKind == JsonValueKind.String)
            {
                var path = pathElement.GetString();

                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(GetNormalizedPath(path, allowRoot: true));
                }
            }

            foreach (var property in value.EnumerateObject())
            {
                AddFolderPaths(property.Value, paths);
            }
        }

        // Adds a serialized collection only when it contains values so GraphQL receives null for optional arguments.
        private static void AddOptionalCollection(
            Dictionary<string, object> variables,
            string name,
            List<string> values)
        {
            if (values.Count > 0)
            {
                variables[name] = values;
            }
        }

        // Maps one creation payload into the identity used by later commands and the warnings retained for diagnostics.
        private static XrayCreatedIssueResult GetCreatedIssueResult(
            JsonElement payload,
            string issuePropertyName,
            string operationName)
        {
            var issue = GetRequiredProperty(payload, issuePropertyName, operationName);
            var issueId = GetOptionalString(issue, "issueId");
            var requiredIssueId = issueId ?? string.Empty;
            ConfirmNumericId(requiredIssueId, $"{operationName}.IssueId");
            var key = string.Empty;

            if (issue.TryGetProperty("jira", out var jiraElement) &&
                jiraElement.ValueKind == JsonValueKind.Object)
            {
                key = GetOptionalString(jiraElement, "key") ?? string.Empty;
            }

            return new XrayCreatedIssueResult
            {
                IssueId = requiredIssueId,
                Key = key,
                Warnings = GetStringCollection(payload, "warnings")
            };
        }

        // Expands one normalized path into parent-first cumulative paths for safe recursive creation.
        private static List<string> GetCumulativePaths(string path)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var paths = new List<string>(segments.Length);
            var currentPath = string.Empty;

            foreach (var segment in segments)
            {
                currentPath += $"/{segment}";
                paths.Add(currentPath);
            }

            return paths;
        }

        // Queries one folder without repeating public validation during recursive path creation.
        private static async Task<XrayFolder> GetFoldersCoreAsync(
            XrayCommandsRepository instance,
            string projectId,
            string path,
            CancellationToken cancellationToken)
        {
            var variables = new
            {
                projectId,
                path
            };

            // Ask Xray for the selected folder and recursive child scalar through the shared transport.
            var data = await instance._client.InvokeAsync(
                operationName: "GetFolders",
                query: XrayGraphQlDocuments.GetFolders,
                variables,
                cancellationToken).ConfigureAwait(false);

            if (!data.TryGetProperty("getFolder", out var folderElement) ||
                folderElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (folderElement.ValueKind != JsonValueKind.Object)
            {
                throw new XpanditClientException("Xray returned an invalid folder response.");
            }

            return GetFolderResult(folderElement);
        }

        // Maps documented folder metadata while cloning the schema-free recursive child JSON.
        private static XrayFolder GetFolderResult(JsonElement folderElement)
        {
            var folders = default(JsonElement);

            if (folderElement.TryGetProperty("folders", out var foldersElement) &&
                foldersElement.ValueKind != JsonValueKind.Null)
            {
                folders = foldersElement.Clone();
            }

            return new XrayFolder
            {
                Folders = folders,
                IssuesCount = GetOptionalInt32(folderElement, "issuesCount"),
                Name = GetOptionalString(folderElement, "name") ?? string.Empty,
                Path = GetOptionalString(folderElement, "path") ?? string.Empty,
                PreconditionsCount = GetOptionalInt32(folderElement, "preconditionsCount"),
                TestsCount = GetOptionalInt32(folderElement, "testsCount")
            };
        }

        // Creates the GraphQL JSON scalar expected by Xray while keeping core Jira fields authoritative.
        private static Dictionary<string, object> GetJiraInput(XrayJiraIssue jira)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(jira.ProjectKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(jira.Summary);

            var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var additionalFields = jira.AdditionalFields ??
                throw new ArgumentNullException(nameof(jira.AdditionalFields));

            foreach (var field in additionalFields)
            {
                if (string.Equals(field.Key, "project", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field.Key, "summary", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"Additional Jira field '{field.Key}' conflicts with a strongly typed field.",
                        nameof(jira));
                }

                fields[field.Key] = field.Value;
            }

            // Apply core fields after extension validation so the request has one unambiguous project and summary.
            fields["project"] = new Dictionary<string, object>
            {
                ["key"] = jira.ProjectKey
            };
            fields["summary"] = jira.Summary;

            return new Dictionary<string, object>
            {
                ["fields"] = fields
            };
        }

        // Normalizes separators and rejects traversal segments that have no meaning in an Xray repository path.
        private static string GetNormalizedPath(string path, bool allowRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var segments = path
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Any(segment => segment is "." or ".."))
            {
                throw new ArgumentException("Folder paths cannot contain traversal segments.", nameof(path));
            }

            var normalizedPath = segments.Length == 0
                ? "/"
                : $"/{string.Join('/', segments)}";

            if (!allowRoot && normalizedPath == "/")
            {
                throw new ArgumentException("The repository root is not valid for this command.", nameof(path));
            }

            return normalizedPath;
        }

        // Validates optional numeric identifiers, removes exact duplicates, and preserves caller ordering.
        private static List<string> GetOptionalNumericIds(
            IReadOnlyCollection<string> values,
            string parameterName)
        {
            if (values is null || values.Count == 0)
            {
                return [];
            }

            var results = new List<string>(values.Count);
            var uniqueValues = new HashSet<string>(StringComparer.Ordinal);

            foreach (var value in values)
            {
                ConfirmNumericId(value, parameterName);

                if (uniqueValues.Add(value))
                {
                    results.Add(value);
                }
            }

            return results;
        }

        // Validates optional text collections, removes exact duplicates, and preserves caller ordering.
        private static List<string> GetOptionalStrings(
            IReadOnlyCollection<string> values,
            string parameterName)
        {
            if (values is null || values.Count == 0)
            {
                return [];
            }

            var results = new List<string>(values.Count);
            var uniqueValues = new HashSet<string>(StringComparer.Ordinal);

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Collection values cannot be null or whitespace.", parameterName);
                }

                if (uniqueValues.Add(value))
                {
                    results.Add(value);
                }
            }

            return results;
        }

        // Reads an optional integer scalar and returns zero when Xray omits count metadata.
        private static int GetOptionalInt32(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var propertyElement) &&
                propertyElement.TryGetInt32(out var value)
                ? value
                : 0;
        }

        // Reads an optional string scalar while preserving null and non-string values as absence.
        private static string GetOptionalString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var propertyElement) &&
                propertyElement.ValueKind == JsonValueKind.String
                ? propertyElement.GetString()
                : null;
        }

        // Retrieves a required payload field and raises a schema-focused failure when Xray changes its response shape.
        private static JsonElement GetRequiredProperty(
            JsonElement element,
            string propertyName,
            string operationName)
        {
            if (!element.TryGetProperty(propertyName, out var propertyElement) ||
                propertyElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                throw new XpanditClientException(
                    $"Xray operation '{operationName}' did not return '{propertyName}'.");
            }

            return propertyElement;
        }

        // Reads an optional Xray status name while preserving absent or incompatible status objects as empty state.
        private static string GetStatusName(JsonElement element)
        {
            if (!element.TryGetProperty("status", out var statusElement) ||
                statusElement.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            return GetOptionalString(statusElement, "name") ?? string.Empty;
        }

        // Converts an add-step model into a sparse GraphQL input while validating custom field identities.
        private static Dictionary<string, object> GetStepInput(XrayTestStepInput step)
        {
            var input = new Dictionary<string, object>();

            if (step.Action is not null)
            {
                input["action"] = step.Action;
            }

            if (step.Data is not null)
            {
                input["data"] = step.Data;
            }

            if (step.Result is not null)
            {
                input["result"] = step.Result;
            }

            var customFields = GetCustomFields(step.CustomFields);

            if (customFields.Count > 0)
            {
                input["customFields"] = customFields;
            }

            return input;
        }

        // Converts a partial step update while retaining the distinction between omitted and explicitly empty values.
        private static Dictionary<string, object> GetStepUpdate(XrayTestStepUpdate step)
        {
            var update = new Dictionary<string, object>();

            if (step.Action is not null)
            {
                update["action"] = step.Action;
            }

            if (step.Data is not null)
            {
                update["data"] = step.Data;
            }

            if (step.Result is not null)
            {
                update["result"] = step.Result;
            }

            if (step.CustomFields is not null)
            {
                update["customFields"] = GetCustomFields(step.CustomFields);
            }

            if (update.Count == 0)
            {
                throw new ArgumentException("At least one test-step field must be selected for update.", nameof(step));
            }

            return update;
        }

        // Maps a JSON string array to an immutable caller-facing collection and ignores incompatible values.
        private static List<string> GetStringCollection(
            JsonElement element,
            string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var valuesElement) ||
                valuesElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var values = new List<string>();

            foreach (var valueElement in valuesElement.EnumerateArray())
            {
                if (valueElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var value = valueElement.GetString();

                if (value is not null)
                {
                    values.Add(value);
                }
            }

            return values;
        }

        // Maps the execution snapshot into stable run identity and ordered manual steps used by later mutations.
        private static XrayTestRunResult GetTestRunResult(JsonElement testRunElement)
        {
            // Require the opaque run identifier because every execution-result mutation depends on it.
            var testRunId = GetOptionalString(testRunElement, "id") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(testRunId))
            {
                throw new XpanditClientException("Xray operation 'GetTestRun' did not return a Test Run identifier.");
            }

            // Require both owning Jira issues so callers can retain the composite lookup identity with the snapshot.
            var testElement = GetRequiredProperty(testRunElement, "test", "GetTestRun");
            var testExecutionElement = GetRequiredProperty(testRunElement, "testExecution", "GetTestRun");
            var testIssueId = GetOptionalString(testElement, "issueId") ?? string.Empty;
            var testExecutionIssueId = GetOptionalString(testExecutionElement, "issueId") ?? string.Empty;
            ConfirmNumericId(testIssueId, "GetTestRun.TestIssueId");
            ConfirmNumericId(testExecutionIssueId, "GetTestRun.TestExecutionIssueId");

            // Preserve Xray's step order so domain callers can map one-based manual step numbers deterministically.
            var steps = GetTestRunSteps(testRunElement);

            return new XrayTestRunResult
            {
                Id = testRunId,
                Status = GetStatusName(testRunElement),
                Steps = steps,
                TestExecutionIssueId = testExecutionIssueId,
                TestIssueId = testIssueId
            };
        }

        // Maps one manual run-step snapshot and rejects responses without the identifier required for later updates.
        private static XrayTestRunStepResult GetTestRunStepResult(JsonElement stepElement)
        {
            var stepId = GetOptionalString(stepElement, "id") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(stepId))
            {
                throw new XpanditClientException("Xray operation 'GetTestRun' returned a step without an identifier.");
            }

            return new XrayTestRunStepResult
            {
                Action = GetOptionalString(stepElement, "action"),
                ActualResult = GetOptionalString(stepElement, "actualResult"),
                Comment = GetOptionalString(stepElement, "comment"),
                Data = GetOptionalString(stepElement, "data"),
                ExpectedResult = GetOptionalString(stepElement, "result"),
                Id = stepId,
                Status = GetStatusName(stepElement)
            };
        }

        // Maps the complete manual step array while retaining Xray's execution-snapshot ordering.
        private static List<XrayTestRunStepResult> GetTestRunSteps(JsonElement testRunElement)
        {
            var stepsElement = GetRequiredProperty(testRunElement, "steps", "GetTestRun");

            if (stepsElement.ValueKind != JsonValueKind.Array)
            {
                throw new XpanditClientException("Xray operation 'GetTestRun' did not return a run-step array.");
            }

            var steps = new List<XrayTestRunStepResult>(stepsElement.GetArrayLength());

            // Convert every returned snapshot in order so step-number selection remains deterministic downstream.
            foreach (var stepElement in stepsElement.EnumerateArray())
            {
                steps.Add(GetTestRunStepResult(stepElement));
            }

            return steps;
        }

        // Converts a manual execution update into sparse GraphQL data while preserving explicit empty text values.
        private static Dictionary<string, object> GetTestRunStepUpdate(XrayTestRunStepUpdate update)
        {
            var updateData = new Dictionary<string, object>();

            if (update.ActualResult is not null)
            {
                // Retain an empty actual result because callers can use it to clear a previously recorded outcome.
                updateData["actualResult"] = update.ActualResult;
            }

            if (update.Comment is not null)
            {
                // Retain an empty comment because callers can use it to clear a previously recorded execution note.
                updateData["comment"] = update.Comment;
            }

            if (update.Status is not null)
            {
                // Require a meaningful Xray status name or identifier before adding it to the sparse mutation data.
                ArgumentException.ThrowIfNullOrWhiteSpace(
                    argument: update.Status,
                    paramName: nameof(update.Status));

                updateData["status"] = update.Status;
            }

            if (updateData.Count == 0)
            {
                throw new ArgumentException(
                    "At least one Test Run Step field must be selected for update.",
                    nameof(update));
            }

            return updateData;
        }

        // Maps the add-step payload into the documented public response and preserves custom JSON values.
        private static XrayTestStepResult GetTestStepResult(JsonElement stepElement)
        {
            var customFields = new List<XrayCustomStepField>();

            if (stepElement.TryGetProperty("customFields", out var customFieldsElement) &&
                customFieldsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var fieldElement in customFieldsElement.EnumerateArray())
                {
                    object value = null;

                    if (fieldElement.TryGetProperty("value", out var valueElement))
                    {
                        value = valueElement.Clone();
                    }

                    customFields.Add(new XrayCustomStepField
                    {
                        Id = GetOptionalString(fieldElement, "id") ?? string.Empty,
                        Value = value
                    });
                }
            }

            return new XrayTestStepResult
            {
                Action = GetOptionalString(stepElement, "action"),
                CustomFields = customFields,
                Data = GetOptionalString(stepElement, "data"),
                Id = GetOptionalString(stepElement, "id") ?? string.Empty,
                Result = GetOptionalString(stepElement, "result")
            };
        }

        // Maps the complete step array selected by Test creation and rejects missing or incompatible response data.
        private static List<XrayTestStepResult> GetTestStepResults(
            JsonElement testElement,
            string operationName)
        {
            var stepsElement = GetRequiredProperty(testElement, "steps", operationName);

            if (stepsElement.ValueKind != JsonValueKind.Array)
            {
                throw new XpanditClientException(
                    $"Xray operation '{operationName}' did not return a step array.");
            }

            // Preserve Xray's returned ordering so callers can compare the persisted definition with their request.
            var steps = new List<XrayTestStepResult>(stepsElement.GetArrayLength());

            foreach (var stepElement in stepsElement.EnumerateArray())
            {
                steps.Add(GetTestStepResult(stepElement));
            }

            return steps;
        }

        // Validates custom field identifiers before models enter the transport serialization boundary.
        private static IReadOnlyCollection<XrayCustomStepField> GetCustomFields(
            IReadOnlyCollection<XrayCustomStepField> customFields)
        {
            if (customFields is null || customFields.Count == 0)
            {
                return [];
            }

            foreach (var customField in customFields)
            {
                ArgumentNullException.ThrowIfNull(customField);
                ArgumentException.ThrowIfNullOrWhiteSpace(customField.Id);
            }

            return customFields;
        }

        // Creates one missing folder segment and lets GraphQL warnings remain non-fatal for final leaf verification.
        private static async Task NewFolderCoreAsync(
            XrayCommandsRepository instance,
            string projectId,
            string path,
            CancellationToken cancellationToken)
        {
            var variables = new
            {
                projectId,
                path
            };

            await instance._client.InvokeAsync(
                operationName: "NewFolder",
                query: XrayGraphQlDocuments.NewFolder,
                variables,
                cancellationToken).ConfigureAwait(false);
        }

        // Enforces the numeric Jira IDs required by Xray GraphQL rather than accepting human-readable keys implicitly.
        private static void ConfirmNumericId(string value, string parameterName)
        {
            var isNumeric = long.TryParse(value, out var numericValue);

            if (!isNumeric || numericValue <= 0)
            {
                throw new ArgumentException(
                    "Xray commands require a positive numeric Jira identifier.",
                    parameterName);
            }
        }
        #endregion
    }
}
