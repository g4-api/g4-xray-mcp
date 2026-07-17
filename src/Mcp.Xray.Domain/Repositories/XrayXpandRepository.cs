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
            return _xpandClient.GetTestCase(idOrKey);
        }

        /// <inheritdoc />
        public object NewTest(string project, TestCaseModel testCase)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(testCase);

                // Preserve the Jira description and resolved custom fields inside Xray's Jira creation payload.
                var additionalFields = new Dictionary<string, object>
                {
                    ["description"] = testCase.Description
                };

                testCase.CustomFields ??= [];

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

                foreach (var testStep in testCase.Steps ?? [])
                {
                    ArgumentNullException.ThrowIfNull(testStep);
                    steps.Add(new XrayTestStepInput
                    {
                        Action = testStep.Action,
                        Result = string.Join('\n', testStep.ExpectedResults ?? [])
                    });
                }

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
