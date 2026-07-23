using Mcp.Xray.Domain.Clients;
using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Mcp.Xray.Domain.Repositories
{
    /// <summary>
    /// Provides an Xray repository implementation backed by the Raven API,
    /// using Jira as the underlying issue management system.
    /// This repository is intended for Jira Data Center (on-premise) environments where Xray
    /// functionality is exposed through the Raven integration.
    /// The repository encapsulates all low-level client interactions and
    /// exposes a stable interface to the rest of the domain.
    /// </summary>
    public class XrayRavenRepository(JiraAuthenticationModel jiraAuthentication) : IXrayRepository
    {
        #region *** Constants    ***
        public const string TestPlanSchema = "com.xpandit.plugins.xray:tests-associated-with-test-plan-custom-field";
        public const string TestSetTestsSchema = "com.xpandit.plugins.xray:test-sets-tests-custom-field";
        public const string TestSetSchema = "com.xpandit.plugins.xray:test-sets-custom-field";
        public const string TestCaseSchema = "com.xpandit.plugins.xray:test-sets-custom-field";
        public const string TestExecutionSchema = "com.xpandit.plugins.xray:testexec-tests-custom-field";
        public const string PreconditionSchema = "com.xpandit.plugins.xray:test-precondition-custom-field";
        public const string ManualTestStepSchema = "com.xpandit.plugins.xray:manual-test-steps-custom-field";
        public const string AssociatedPlanSchema = "com.xpandit.plugins.xray:test-plans-associated-with-test-custom-field";
        #endregion

        #region *** Fields       ***
        // Jira client used to create and manage Jira issues related to Xray tests.
        // This client is initialized using the provided authentication model
        // and is reused internally by the repository for all Jira operations.
        private readonly JiraClient _jiraClient = new(jiraAuthentication);

        // Xpand client used to manage Xray-specific entities such as test steps.
        // This client handles communication with the Xpand API layer and is
        // responsible for extending Jira test issues with Xray metadata.
        private readonly RavenClient _ravenClient = new(jiraAuthentication);
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public object AddTestsToFolder(string idOrKey, string path, string jql)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public object AddTestsToPlan(string idOrKey, string jql)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public object GetTest(string idOrKey)
        {
            return _jiraClient.GetIssue(idOrKey);
        }

        //TODO: Add support for 'Severity' field in test case creation and updates
        /// <inheritdoc />
        public object NewTest(string project, TestCaseModel testCase)
        {
            // Resolve the Jira/Xray issue type used for test creation.
            // If no explicit issue type is configured, fall back to the standard Xray "Test" type.
            var isTestType = !string.IsNullOrEmpty(AppSettings.JiraOptions?.XrayClientOptions?.TestIssueType);
            var testType = isTestType
                ? AppSettings.JiraOptions.XrayClientOptions.TestIssueType
                : "Test";

            // Resolve the configured test priority into the Jira priority ID expected by the API.
            var priority = _jiraClient.GetAllowedValueId(
                project,
                testType,
                "..priority",
                testCase.Priority
            );

            // Store the resolved priority only when Jira returned a valid value.
            if (!string.IsNullOrEmpty(priority))
            {
                testCase.Context["test-priority"] = priority;
            }

            // Populate the context values required later by ConvertToXrayIssue.
            // These values are resolved from the target Jira project so the payload matches
            // the current Jira/Xray configuration.
            testCase.Context["issuetype-id"] = $"{_jiraClient.GetFieldDefinition(project, idOrKey: testType, path: "id")}";
            testCase.Context["project-key"] = project;
            testCase.Context["test-sets-custom-field"] = _jiraClient.GetCustomField(project, schema: TestSetSchema);
            testCase.Context["manual-test-steps-custom-field"] = _jiraClient.GetCustomField(project, schema: ManualTestStepSchema);
            testCase.Context["test-plan-custom-field"] = _jiraClient.GetCustomField(project, schema: AssociatedPlanSchema);
            testCase.Context["jira-custom-fields"] = _jiraClient.ResolveCustomFields(project, testType, testCase.CustomFields);

            // Build the Jira/Xray create-issue request body, including manual test steps.
            var requestBody = ConvertToXrayIssue(testCase, includeSteps: true);

            // Create the issue in Jira.
            var issue = _jiraClient.NewIssue(requestBody);

            // Add the standard controller signature comment to mark the issue as generated by this integration.
            var comment = ControllerUtilities.CommentSignature;
            _jiraClient.NewComment(idOrKey: $"{issue.GetProperty("key")}", comment);

            // Return the Jira response as a string/object-compatible result.
            return $"{issue}";

            // Converts a test case model into a Jira/Xray issue creation payload.
            static string ConvertToXrayIssue(TestCaseModel testCase, bool includeSteps)
            {
                // Build the Xray manual step payloads from the test case steps.
                var steps = GetSteps(testCase);

                // Description is optional, so fall back to an empty string when it is not provided
                // in the test case context.
                var description = testCase.Context.TryGetValue("description", out object descriptionOut)
                    ? descriptionOut
                    : string.Empty;

                // Build the base Jira issue fields required to create the Xray test issue.
                var payload = new Dictionary<string, object>
                {
                    ["summary"] = testCase.Scenario,
                    ["description"] = description,
                    ["issuetype"] = new Dictionary<string, object>
                    {
                        ["id"] = $"{testCase.Context["issuetype-id"]}"
                    },
                    ["project"] = new Dictionary<string, object>
                    {
                        ["key"] = $"{testCase.Context["project-key"]}"
                    },

                    // The manual steps field is configured per Jira/Xray instance,
                    // so its field ID is provided by the test case context.
                    [$"{testCase.Context["manual-test-steps-custom-field"]}"] = includeSteps
                        ? new Dictionary<string, object> { ["steps"] = steps }
                        : []
                };

                // Priority is optional. When provided, Jira expects it as an object with an id value.
                if (testCase.Context.TryGetValue("test-priority", out object priority))
                {
                    payload["priority"] = new Dictionary<string, object>
                    {
                        ["id"] = $"{priority}"
                    };
                }

                // Attach the test to one or more test plans when both the values and the target
                // custom field ID are available.
                var testPlans = testCase.TestPlans;
                var isTestPlan = testPlans.Length != 0 && testCase.Context.ContainsKey("test-plan-custom-field");
                if (isTestPlan)
                {
                    payload[$"{testCase.Context["test-plan-custom-field"]}"] = testPlans;
                }

                // Merge additional Jira custom fields that were already resolved by field ID.
                if (testCase.Context.TryGetValue("jira-custom-fields", out object value))
                {
                    var jiraCustomFields = value as IDictionary<string, object>;
                    foreach (var jiraCustomField in jiraCustomFields)
                    {
                        payload[jiraCustomField.Key] = jiraCustomField.Value;
                    }
                }

                // Jira's create-issue endpoint expects the field map to be wrapped in a top-level
                // "fields" object.
                return JsonConvert.SerializeObject(new Dictionary<string, object>
                {
                    ["fields"] = payload
                });
            }

            // Builds Jira/Xray-compatible step field payloads from the executable steps of a test case.
            static List<Dictionary<string, object>> GetSteps(TestCaseModel testCase)
            {
                // Store the exported step payloads in the same order as the source test case steps.
                var steps = new List<Dictionary<string, object>>();

                // Iterate by index so the exported id/index can match the original step position.
                for (int i = 0; i < testCase.Steps.Length; i++)
                {
                    // Skip empty/non-executable steps because Jira/Xray steps require an action value.
                    var isAction = !string.IsNullOrEmpty(testCase.Steps[i].Action);
                    if (!isAction)
                    {
                        continue;
                    }

                    // Cache the current step for readability and to avoid repeated array indexing.
                    var onStep = testCase.Steps[i];

                    // Convert multiple expected-result lines into a single newline-separated value.
                    var expectedResults = onStep.ExpectedResults.Length == 0
                        ? string.Empty
                        : string.Join('\n', onStep.ExpectedResults.Select(i => i));

                    // Escape curly braces because the target field processor treats braces as template markers.
                    var action = onStep.Action.Replace("{", "{{").Replace("}", "}}");
                    var expectedResult = expectedResults.Replace("{", "{{").Replace("}", "}}");

                    // Build the target step payload using the field names expected by Jira/Xray.
                    var step = new Dictionary<string, object>
                    {
                        ["id"] = i + 1,
                        ["index"] = i + 1,
                        ["fields"] = new Dictionary<string, object>
                        {
                            ["Action"] = action,
                            ["Expected Result"] = expectedResult
                        }
                    };

                    // Add the mapped step to the exported step list.
                    steps.Add(step);
                }

                // Return all executable steps that were mapped from the test case.
                return steps;
            }
        }

        /// <inheritdoc />
        public object NewTestPlan(string project, NewTestPlanModel testPlan)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public object NewTestRepositoryFolder(string idOrKey, string name, string parentId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public string ResolveFolderPath(string idOrKey, string path)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public object UpdateTest(string key, TestCaseModel testCase)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public object UpdateTest(string project, string key, TestCaseModel testCase)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
