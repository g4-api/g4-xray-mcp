namespace Xpandit.Client.Internal
{
    // Centralizes the stable Xray GraphQL documents so repository methods focus on command data and results.
    internal static class XrayGraphQlDocuments
    {
        #region *** Constants    ***
        internal const string AddTestExecutionsToTestPlan =
            "mutation AddTestExecutionsToTestPlan($issueId: String!, $testExecutionIssueIds: [String]!) { " +
            "addTestExecutionsToTestPlan(issueId: $issueId, testExecIssueIds: $testExecutionIssueIds) { " +
            "addedTestExecutions warning } }";

        internal const string AddTestStep =
            "mutation AddTestStep($issueId: String!, $versionId: Int, $step: CreateStepInput!) { " +
            "addTestStep(issueId: $issueId, versionId: $versionId, step: $step) { " +
            "id action data result customFields { id value } } }";

        internal const string AddTestsToTestExecution =
            "mutation AddTestsToTestExecution($issueId: String!, $testIssueIds: [String]!) { " +
            "addTestsToTestExecution(issueId: $issueId, testIssueIds: $testIssueIds) { " +
            "addedTests warning } }";

        internal const string AddTestsToTestPlan =
            "mutation AddTestsToTestPlan($issueId: String!, $testIssueIds: [String]!) { " +
            "addTestsToTestPlan(issueId: $issueId, testIssueIds: $testIssueIds) { " +
            "addedTests warning } }";

        internal const string GetFolders =
            "query GetFolders($projectId: String!, $path: String!) { " +
            "getFolder(projectId: $projectId, path: $path) { " +
            "name path issuesCount testsCount preconditionsCount folders } }";

        internal const string GetTestRun =
            "query GetTestRun($testIssueId: String!, $testExecutionIssueId: String!) { " +
            "getTestRun(testIssueId: $testIssueId, testExecIssueId: $testExecutionIssueId) { " +
            "id status { name } test { issueId } testExecution { issueId } steps { " +
            "id action data result actualResult comment status { name } } } }";

        internal const string MoveTestToFolder =
            "mutation MoveTestToFolder($issueId: String!, $folderPath: String!) { " +
            "updateTestFolder(issueId: $issueId, folderPath: $folderPath) }";

        internal const string NewFolder =
            "mutation NewFolder($projectId: String!, $path: String!) { " +
            "createFolder(projectId: $projectId, path: $path) { " +
            "folder { name path } warnings } }";

        internal const string NewTest =
            "mutation NewTest($testType: UpdateTestTypeInput!, $steps: [CreateStepInput], $jira: JSON!) { " +
            "createTest(testType: $testType, steps: $steps, jira: $jira) { " +
            "test { issueId testType { name } steps { " +
            "id action data result customFields { id value } } jira(fields: [\"key\"]) } warnings } }";

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

        internal const string UpdateTestRunStep =
            "mutation UpdateTestRunStep($testRunId: String!, $stepId: String!, " +
            "$updateData: UpdateTestRunStepInput!, $iterationRank: String) { " +
            "updateTestRunStep(testRunId: $testRunId, stepId: $stepId, " +
            "updateData: $updateData, iterationRank: $iterationRank) { warnings } }";

        internal const string UpdateTestRunStatus =
            "mutation UpdateTestRunStatus($testRunId: String!, $status: String!) { " +
            "updateTestRunStatus(id: $testRunId, status: $status) }";

        internal const string UpdateTestStep =
            "mutation UpdateTestStep($stepId: String!, $step: UpdateStepInput!) { " +
            "updateTestStep(stepId: $stepId, step: $step) { warnings } }";
        #endregion
    }
}
