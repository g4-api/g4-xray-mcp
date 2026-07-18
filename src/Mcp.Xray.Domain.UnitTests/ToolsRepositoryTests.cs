using System.Text.Json;

using Mcp.Xray.Domain.Models;
using Mcp.Xray.Domain.Repositories;

namespace Mcp.Xray.Domain.UnitTests
{
    /// <summary>
    /// Verifies that execution-focused MCP definitions are embedded and routed to strongly typed repository contracts.
    /// </summary>
    [TestClass]
    [TestCategory("ToolsRepository")]
    [TestCategory("UnitTest")]
    public class ToolsRepositoryTests
    {
        #region *** Methods      ***
        [TestMethod(DisplayName = "Verify that execution tool metadata exposes every dedicated Xray contract.")]
        public void ExecutionToolMetadataTest()
        {
            // Arrange: create the tool registry with an isolated provider because metadata discovery performs no I/O.
            var xrayRepository = new TestXrayRepository();
            var toolsRepository = new ToolsRepository(xrayRepository);
            var requiredTools = new Dictionary<string, string[]>
            {
                ["add_xray_test_executions_to_plan"] = ["testExecutionKeys", "testPlanKey"],
                ["add_xray_tests_to_execution"] = ["executionKey", "testKeys"],
                ["new_xray_execution"] = ["project", "summary", "testKeys"],
                ["update_xray_execution_step"] = ["executionKey", "stepNumber", "testKey"],
                ["update_xray_test_run_status"] = ["executionKey", "status", "testKey"]
            };

            // Act: read the same embedded registry returned to MCP clients during tool discovery.
            var response = toolsRepository.GetTools(
                id: "metadata-test",
                intent: string.Empty);
            var result = response.Result as ToolOutputSchema.ToolsResultSchema;
            var tools = result?.Tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal) ?? [];

            // Assert: every execution operation exposes input and output schemas under its exact routed name.
            foreach (var requiredTool in requiredTools)
            {
                Assert.IsTrue(tools.TryGetValue(requiredTool.Key, out var tool), requiredTool.Key);
                Assert.IsNotNull(tool.InputSchema, requiredTool.Key);
                Assert.IsNotNull(tool.OutputSchema, requiredTool.Key);
                Assert.IsNotNull(tool.OutputSchema.Properties, requiredTool.Key);
                CollectionAssert.AreEquivalent(requiredTool.Value, tool.InputSchema.Required);
            }

            // Assert: execution creation advertises the registered Test Run identities required by result replay.
            Assert.IsTrue(tools["new_xray_execution"].OutputSchema.Properties.ContainsKey("testRunIds"));
        }

        [TestMethod(DisplayName = "Verify that execution tool invocations route to typed Xray repository operations.")]
        public void ExecutionToolRoutingTest()
        {
            // Arrange: isolate the reflection dispatcher behind a provider that records typed execution requests.
            var xrayRepository = new TestXrayRepository();
            var toolsRepository = new ToolsRepository(xrayRepository);

            // Act: invoke each new execution tool through the complete JSON-RPC argument boundary.
            InvokeTool(
                toolsRepository,
                toolName: "add_xray_test_executions_to_plan",
                arguments: "{\"testPlanKey\":\"GAR-61\",\"testExecutionKeys\":[\"GAR-63\"]}");
            InvokeTool(
                toolsRepository,
                toolName: "add_xray_tests_to_execution",
                arguments: "{\"executionKey\":\"GAR-63\",\"testKeys\":[\"GAR-58\"]}");
            InvokeTool(
                toolsRepository,
                toolName: "update_xray_test_run_status",
                arguments: "{\"executionKey\":\"GAR-63\",\"testKey\":\"GAR-58\",\"status\":\"PASSED\"}");

            // Assert: reflection routing materializes each expected model without leaking raw JSON into the provider.
            Assert.IsNotNull(xrayRepository.TestExecutionsAssociation);
            Assert.AreEqual("GAR-61", xrayRepository.TestExecutionsAssociation.TestPlanKey);
            CollectionAssert.AreEqual(
                new[] { "GAR-63" },
                xrayRepository.TestExecutionsAssociation.TestExecutionKeys);
            Assert.IsNotNull(xrayRepository.TestsAssociation);
            Assert.AreEqual("GAR-63", xrayRepository.TestsAssociation.ExecutionKey);
            CollectionAssert.AreEqual(new[] { "GAR-58" }, xrayRepository.TestsAssociation.TestKeys);
            Assert.IsNotNull(xrayRepository.TestRunStatus);
            Assert.AreEqual("PASSED", xrayRepository.TestRunStatus.Status);
        }

        // Invokes one tool through the public JSON boundary so tests exercise reflection and shared deserialization.
        private static void InvokeTool(
            ToolsRepository toolsRepository,
            string toolName,
            string arguments)
        {
            // Construct the dispatcher envelope while preserving the supplied arguments as a closed JSON object.
            var json = $"{{\"name\":\"{toolName}\",\"arguments\":{arguments}}}";
            using var document = JsonDocument.Parse(json);

            // Invoke the public dispatcher because direct static calls bypass the behavior under test.
            toolsRepository.InvokeTool(
                parameters: document.RootElement,
                id: toolName);
        }
        #endregion

        #region *** Nested Types ***
        // Records execution-focused requests while rejecting unrelated provider operations used outside these tests.
        private sealed class TestXrayRepository : IXrayRepository
        {
            #region *** Properties   ***
            public AddTestExecutionsToPlanModel TestExecutionsAssociation { get; private set; }

            public AddTestsToExecutionModel TestsAssociation { get; private set; }

            public UpdateTestRunStatusModel TestRunStatus { get; private set; }
            #endregion

            #region *** Methods      ***
            public object AddTestExecutionsToPlan(AddTestExecutionsToPlanModel association)
            {
                // Retain the typed request so the routing test can verify deserialization and dispatch ownership.
                TestExecutionsAssociation = association;
                return new { Status = "recorded" };
            }

            public object AddTestsToExecution(AddTestsToExecutionModel association)
            {
                // Retain the typed request so the routing test can verify deserialization and dispatch ownership.
                TestsAssociation = association;
                return new { Status = "recorded" };
            }

            public object AddTestsToFolder(string idOrKey, string path, string jql)
            {
                throw new NotSupportedException();
            }

            public object AddTestsToPlan(string idOrKey, string jql)
            {
                throw new NotSupportedException();
            }

            public object GetTest(string idOrKey)
            {
                throw new NotSupportedException();
            }

            public object NewTest(string project, TestCaseModel testCase)
            {
                throw new NotSupportedException();
            }

            public object NewTestPlan(string project, NewTestPlanModel testPlan)
            {
                throw new NotSupportedException();
            }

            public object NewTestRepositoryFolder(string idOrKey, string name, string path)
            {
                throw new NotSupportedException();
            }

            public string ResolveFolderPath(string idOrKey, string path)
            {
                throw new NotSupportedException();
            }

            public object UpdateTest(string key, TestCaseModel testCase)
            {
                throw new NotSupportedException();
            }

            public object UpdateTestRunStatus(UpdateTestRunStatusModel testRun)
            {
                // Retain the typed request so the routing test can verify deserialization and dispatch ownership.
                TestRunStatus = testRun;
                return new { Status = "recorded" };
            }
            #endregion
        }
        #endregion
    }
}
