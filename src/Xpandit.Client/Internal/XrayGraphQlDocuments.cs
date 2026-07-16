namespace Xpandit.Client.Internal
{
    // Centralizes the stable Xray GraphQL documents so repository methods focus on command data and results.
    internal static class XrayGraphQlDocuments
    {
        #region *** Constants    ***
        internal const string AddTestStep =
            "mutation AddTestStep($issueId: String!, $versionId: Int, $step: CreateStepInput!) { " +
            "addTestStep(issueId: $issueId, versionId: $versionId, step: $step) { " +
            "id action data result customFields { id value } } }";

        internal const string GetFolders =
            "query GetFolders($projectId: String!, $path: String!) { " +
            "getFolder(projectId: $projectId, path: $path) { " +
            "name path issuesCount testsCount preconditionsCount folders } }";

        internal const string MoveTestToFolder =
            "mutation MoveTestToFolder($issueId: String!, $folderPath: String!) { " +
            "updateTestFolder(issueId: $issueId, folderPath: $folderPath) }";

        internal const string NewFolder =
            "mutation NewFolder($projectId: String!, $path: String!) { " +
            "createFolder(projectId: $projectId, path: $path) { " +
            "folder { name path } warnings } }";

        internal const string NewTestExecution =
            "mutation NewTestExecution($testIssueIds: [String], $testEnvironments: [String], $jira: JSON!) { " +
            "createTestExecution(testIssueIds: $testIssueIds, testEnvironments: $testEnvironments, jira: $jira) { " +
            "testExecution { issueId jira(fields: [\"key\"]) } warnings createdTestEnvironments } }";

        internal const string NewTestPlan =
            "mutation NewTestPlan($testIssueIds: [String], $jira: JSON!) { " +
            "createTestPlan(testIssueIds: $testIssueIds, jira: $jira) { " +
            "testPlan { issueId jira(fields: [\"key\"]) } warnings } }";

        internal const string NewTestSet =
            "mutation NewTestSet($testIssueIds: [String], $jira: JSON!) { " +
            "createTestSet(testIssueIds: $testIssueIds, jira: $jira) { " +
            "testSet { issueId jira(fields: [\"key\"]) } warnings } }";

        internal const string ResolveTestIssueId =
            "query ResolveTestIssueId($jql: String!) { " +
            "getTests(jql: $jql, limit: 2) { total results { issueId jira(fields: [\"key\"]) } } }";

        internal const string UpdateTestStep =
            "mutation UpdateTestStep($stepId: String!, $step: UpdateStepInput!) { " +
            "updateTestStep(stepId: $stepId, step: $step) { warnings } }";
        #endregion
    }
}
