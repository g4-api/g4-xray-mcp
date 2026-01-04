using Mcp.Xray.Domain.Clients;
using Mcp.Xray.Domain.Exceptions;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace Mcp.Xray.Domain.Repositories
{
    /// <summary>
    /// Provides an Xray repository implementation backed by the Xpand API,
    /// using Jira as the underlying issue management system.
    /// </summary>
    /// <remarks>
    /// This repository is intended for Jira Cloud environments where Xray
    /// functionality is exposed through the Xpand integration.
    /// The repository encapsulates all low-level client interactions and
    /// exposes a stable interface to the rest of the domain.
    /// </remarks>
    public class XrayXpandRepository(JiraAuthenticationModel jiraAuthentication) : IXrayRepository
    {
        // Jira client used to create and manage Jira issues related to Xray tests.
        // This client is initialized using the provided authentication model
        // and is reused internally by the repository for all Jira operations.
        private readonly JiraClient _jiraClient = new(jiraAuthentication);

        // Xpand client used to manage Xray-specific entities such as test steps.
        // This client handles communication with the Xpand API layer and is
        // responsible for extending Jira test issues with Xray metadata.
        private readonly XpandClient _xpandClient = new(jiraAuthentication);

        /// <inheritdoc />
        public object NewTest(string project, TestCaseModel testCase)
        {
            // Normalize all values required for creating the base Jira issue into a single options model.
            // This keeps the request builder isolated from the shape of the incoming domain model.
            var options = new IssueCreateOptions
            {
                Context = testCase.Context,
                CustomFields = testCase.CustomFields,
                Description = testCase.Actual,
                IssueType = "Test",
                Project = project,
                Summary = testCase.Scenario
            };

            // Build the Jira issue creation request payload, including custom fields when enabled.
            var testIssue = NewIssueRequest(_jiraClient, options);

            // Define a domain-specific exception that represents an incomplete Jira issue creation result.
            // This exception is reused across retries to keep error semantics consistent.
            var issueException = new JiraIssueNotCreatedException(
                message: "Jira issue was not created successfully. The response did not contain an issue id or key."
            );

            // Attempt to create the Jira issue with retry semantics to handle transient Jira failures.
            // The invocation validates that the response contains both id and key before accepting success.
            var jiraResponse = (JsonElement)InvokeRepeatableAction(
                exception: issueException,
                func: () =>
                {
                    // Create the Jira issue and capture the raw JSON response.
                    var response = _jiraClient.NewIssue(testIssue);

                    // Attempt to extract both the issue id and key from the Jira response.
                    // These values are required to reference the created issue and build links.
                    var hasId = response.TryGetProperty("id", out JsonElement id);
                    var hasKey = response.TryGetProperty("key", out JsonElement key);

                    // Treat a missing id or key as a failure since subsequent operations depend on both.
                    if (!hasId || !hasKey)
                    {
                        throw issueException;
                    }

                    // Return the validated Jira response to the caller for further processing.
                    return response;
                }
            );

            // Extract the created issue key and id from the validated Jira response.
            // These are stored as JsonElement values and converted to strings only when needed.
            var key = jiraResponse.GetProperty("key");
            var id = jiraResponse.GetProperty("id");

            // Build a direct browser link to the newly created Jira issue.
            var link = $"{jiraAuthentication.Collection}/browse/{key.GetString()}";

            // Create each Xray test step in sequence to preserve order and ensure deterministic indexing.
            for (int i = 0; i < testCase.Steps.Length; i++)
            {
                var step = testCase.Steps[i];

                // Extract the step action and serialize expected results into a newline-delimited string.
                // This keeps the Xray step payload human-readable in the Xray UI.
                var action = step.Action;
                var result = string.Join('\n', step.ExpectedResults);

                // Define a domain-specific exception for step creation failures.
                // The message captures the test key and step index to simplify troubleshooting.
                var stepException = new XrayTestStepNotCreatedException(
                    message: $"Xray test step was not created successfully for test {key.GetString()} at index {i}."
                );

                // Attempt to create the Xray step with retry semantics to handle transient integration failures.
                InvokeRepeatableAction(
                    exception: stepException,
                    func: () =>
                    {
                        return _xpandClient.NewTestStep(
                            test: (id.GetString(), key.GetString()),
                            action,
                            result,
                            index: i);
                    }
                );
            }

            // Return a minimal consumer-friendly representation of the created test.
            // This avoids leaking the full Jira response while still providing key identifiers.
            return new
            {
                Id = id,
                Key = key,
                Link = link
            };
        }

        // Executes the provided action repeatedly until it succeeds or the retry limit is reached.
        private static object InvokeRepeatableAction(Exception exception, Func<object> func)
        {
            // Retrieve retry configuration from application settings.
            var maxAttempts = AppSettings.JiraOptions.RetryOptions.MaxAttempts;
            var delayMilliseconds = AppSettings.JiraOptions.RetryOptions.DelayMilliseconds;

            // Attempt to execute the action up to the configured retry count.
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    // Execute the action and immediately return the result on success.
                    return func();
                }
                catch
                {
                    // Swallow the exception and continue to the next retry attempt.
                    // The final exception will be thrown only after all retries are exhausted.
                }

                // Wait for the configured delay before the next retry attempt.
                Thread.Sleep(delayMilliseconds);
            }

            // All retry attempts have failed, so propagate the provided exception
            // to signal an unrecoverable failure to the caller.
            throw exception;
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
            if (!resolveCustomFields || options.CustomFields is null || options.CustomFields.Count == 0)
            {
                return baseRequest;
            }

            // Iterate through the custom fields and attempt to resolve each schema
            // to its corresponding Jira field identifier.
            foreach (var item in options.CustomFields)
            {
                // Extract the schema identifier for the custom field.
                var schema = item.Key;

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
            public IDictionary<string, string> CustomFields { get; set; }

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
    }
}
