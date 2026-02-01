using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

using static System.Net.Mime.MediaTypeNames;

namespace Mcp.Xray.Domain.Repositories
{
    public class ToolsRepository(IXrayRepository xray) : IToolsRepository
    {
        #region *** Constants    ***
        // The JSON-RPC protocol version used in all responses.
        private const string JsonRpcVersion = "2.0";

        // Static, atomically swappable snapshot
        private static readonly ConcurrentDictionary<string, McpToolModel> s_tools = FormatTools();
        #endregion

        #region *** Fields       ***
        // Reference to the domain layer for accessing other services and repositories.
        private readonly IXrayRepository _xray = xray;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public ToolOutputSchema GetTools(object id, string intent, params string[] types)
        {
            // Return a new CopilotToolsResponseModel with the list of tools from the registry.
            return new()
            {
                // Include the request ID to correlate the response with the request.
                Id = id,
                Jsonrpc = JsonRpcVersion,
                Result = new ToolOutputSchema.ToolsResultSchema()
                {
                    // Provide the list of tools contained in the registry as the result.
                    Tools = s_tools.Values
                }
            };
        }

        /// <inheritdoc />
        public McpInitializeResponseModel Initialize(object id) => new()
        {
            // Set the request ID and JSON-RPC version for the response.
            Id = id,
            Jsonrpc = JsonRpcVersion,

            // Define the result, which contains the capabilities, protocol version, and server info.
            Result = new()
            {
                Capabilities = new()
                {
                    // Indicate that the tool list has changed (can be used to trigger updates or notifications).
                    Tools = new()
                    {
                        ListChanged = true
                    }
                },

                // The protocol version the server is using (fixed value in this case).
                ProtocolVersion = "2025-03-26",

                // Define the server information (name and version).
                ServerInfo = new()
                {
                    Name = "g4-engine-copilot-mcp",
                    Version = "4.0.0"
                }
            }
        };

        /// <inheritdoc />
        public ToolOutputSchema InvokeTool(JsonElement parameters, object id)
        {
            // Parse and normalize the invocation parameters into a strongly-typed options object.
            // This typically extracts the tool name and prepares arguments for reflection-based execution.
            var options = new InvokeOptions(_xray, parameters);

            // Discover all public static methods on the Tools type that are marked
            // with the SystemToolAttribute, which identifies callable system tools.
            var tools = typeof(Tools)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(i => i.GetCustomAttribute<SystemToolAttribute>() != null);

            // Attempt to locate the tool whose attribute name matches the requested tool name.
            // The comparison is case-insensitive to provide a more forgiving API surface.
            var tool = tools
                .FirstOrDefault(i => string.Equals(
                    i.GetCustomAttribute<SystemToolAttribute>()?.Name,
                    options.ToolName,
                    StringComparison.OrdinalIgnoreCase));

            // If no matching tool is found, the request cannot be fulfilled.
            // This is treated as a hard failure since the caller requested an unknown tool.
            if (tool == default)
            {
                throw new Exception();
            }

            // Invoke the resolved tool method via reflection.
            // Tools are static, so no instance is required, and the invocation
            // receives the prepared InvokeOptions object as its sole argument.
            var result = tool.Invoke(obj: null, parameters: [options]);

            // Construct and return the JSON-RPC response envelope.
            return new()
            {
                // Echo the original request identifier so the caller can correlate responses.
                Id = id,

                // Specify the JSON-RPC protocol version used by this service.
                Jsonrpc = JsonRpcVersion,

                // Populate the result payload with both a textual and structured representation.
                Result = new ToolOutputSchema.ToolOutputResultSchema
                {
                    // Textual content is provided as serialized JSON for clients
                    // that only consume plain text responses.
                    Content = new[]
                    {
                        new
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(result, IToolsRepository.JsonOptions)
                        }
                    },

                    // StructuredContent preserves the original object returned by the tool
                    // for clients that can consume typed or structured data directly.
                    StructuredContent = result
                }
            };
        }

        // Loads and materializes all embedded MCP tool definitions from the executing assembly.
        // This method scans the current assembly for embedded JSON resources that match the
        // Definitions.*.json naming convention, deserializes them and indexes them by tool name.
        private static ConcurrentDictionary<string, McpToolModel> FormatTools()
        {
            // Get the currently executing assembly (where the embedded resources live).
            var assembly = Assembly.GetExecutingAssembly();

            // Retrieve all embedded resource names declared in the assembly.
            var definitions = assembly.GetManifestResourceNames();

            // Thread-safe, case-insensitive dictionary for storing tools by name.
            var toolsCollection = new ConcurrentDictionary<string, McpToolModel>(
                StringComparer.OrdinalIgnoreCase
            );

            // Iterate over all embedded resources.
            foreach (var definition in definitions)
            {
                // Skip any resource that does not follow the expected naming convention:
                // "Definitions.<something>.json"
                if (!Regex.IsMatch(input: definition, pattern: @"(Definitions\.).*\.json"))
                {
                    continue;
                }

                try
                {
                    // Open a stream to the embedded JSON resource.
                    using var stream = assembly.GetManifestResourceStream(definition);

                    // The null-forgiving operator is safe here because we control the resource list.
                    using var reader = new StreamReader(stream!);

                    // Read the full JSON payload.
                    var json = reader.ReadToEnd();

                    // Deserialize the JSON into an MCP tool model using shared repository options.
                    var tool = JsonSerializer.Deserialize<McpToolModel>(
                        json,
                        IToolsRepository.JsonOptions
                    );

                    // Skip invalid or empty tool definitions.
                    if (tool == null)
                    {
                        continue;
                    }

                    // Populate the metadata section explicitly.
                    // This decouples runtime metadata from the raw definition payload.
                    tool.Metadata = new()
                    {
                        Description = tool.Description,
                        Name = tool.Name
                    };

                    // Add or overwrite the tool entry using its logical name as the key.
                    toolsCollection[tool.Name] = tool;
                }
                catch (Exception e)
                {
                    // TODO: Replace with proper logging.
                    // Intentionally swallow individual failures to keep the system resilient.
                    // A single malformed tool definition should not prevent startup.
                    Console.WriteLine(e);
                    continue;
                }
            }

            // Return the fully populated tools collection.
            return toolsCollection;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Encapsulates the options required to invoke a tool,
        /// including driver configuration, session management,
        /// authentication, and OpenAI integration settings.
        /// </summary>
        private sealed class InvokeOptions
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="InvokeOptions"/> class.
            /// Provides an empty options object where properties may be set manually.
            /// </summary>
            public InvokeOptions(IXrayRepository xray)
            {
                Xray = xray;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="InvokeOptions"/> class
            /// using a <see cref="JsonElement"/> containing tool invocation parameters.
            /// </summary>
            /// <param name="parameters">The JSON parameters containing an <c>"arguments"</c> object with tool-specific settings.</param>
            public InvokeOptions(IXrayRepository xray, JsonElement parameters)
            {
                // Store the Xray repository reference for use by invoked tools.
                Xray = xray;

                // Extract the "arguments" object from the JSON parameters.
                // This contains tool-specific input such as driver settings, session IDs, etc.
                var arguments = parameters.GetProperty("arguments");
                Arguments = arguments;

                // Retrieve the "name" argument (the tool identifier to be invoked).
                // Defaults to null if not provided.
                ToolName = parameters.GetOrDefault("name", () => default(string));
            }

            /// <summary>
            /// The raw JSON <c>"arguments"</c> object supplied to the tool.
            /// </summary>
            public JsonElement Arguments { get; set; }

            /// <summary>
            /// Gets the Xray repository for performing Xray-related operations.
            /// </summary>
            public IXrayRepository Xray { get; }

            /// <summary>
            /// The name of the tool being invoked.
            /// </summary>
            public string ToolName { get; set; }
        }

        /// <summary>
        /// Encapsulates the static system tools available for invocation.
        /// This class contains methods that implement various Xray-related operations,
        /// and called via reflection based on tool names.
        /// </summary>
        private static class Tools
        {
            // Adds Xray test cases selected by a JQL query to a specified folder
            // in the Xray Test Repository.
            [SystemTool("add_xray_tests_to_folder")]
            public static object AddXrayTestsToFolder(InvokeOptions options)
            {
                // Extract the Jira project identifier or project key from the invocation arguments.
                // This value defines the scope of the Xray Test Repository.
                var project = options.Arguments.GetProperty("project").GetString();

                // Extract the JQL query used to select the test issues
                // that will be moved into the target repository folder.
                var jql = options.Arguments.GetProperty("jql").GetString();

                // Attempt to extract the repository folder path.
                // When not provided, the root folder is assumed.
                var hasPath = options.Arguments.TryGetProperty("path", out JsonElement pathOut);

                // Normalize the path so downstream logic can treat an empty value
                // as a request to operate at the repository root.
                var path = hasPath ? pathOut.GetString() : string.Empty;

                // Delegate the folder assignment logic to the Xray repository implementation.
                // The repository is responsible for resolving the folder path,
                // executing the JQL query, and invoking the internal Xray API.
                return options.Xray.AddTestsToFolder(
                    idOrKey: project,
                    path: path,
                    jql: jql);
            }

            // Adds one or more Xray test cases to an existing Test Plan
            // based on a JQL query.
            [SystemTool("add_xray_tests_to_plan")]
            public static object AddXrayTestsToPlan(InvokeOptions options)
            {
                // Extract the Test Plan identifier or Jira issue key.
                // This identifies the target Xray Test Plan to which
                // test cases will be applied.
                var key = options.Arguments.GetProperty("key").GetString();

                // Extract the JQL expression used to resolve the test cases
                // that should be added to the Test Plan.
                var jql = options.Arguments.GetProperty("jql").GetString();

                // Delegate the operation to the Xray domain layer.
                // The implementation is responsible for executing the JQL,
                // resolving test issue IDs, and invoking the internal Xray API
                // to associate those tests with the Test Plan.
                return options.Xray.AddTestsToPlan(idOrKey: key, jql);
            }

            // This system tool acts as a read-only query against the Xray API.
            // It expects a valid Xray test issue key (for example: <c>PROJ-123</c>)
            // and returns the full Test Case model as provided by Xray.
            [SystemTool("get_xray_test")]
            public static object GetXrayTest(InvokeOptions options)
            {
                // Extract the Xray issue key from the tool invocation arguments.
                // Expected format example: "PROJ-123"
                var key = options.Arguments
                    .GetProperty("key")
                    .GetString();

                // Delegate the lookup to the Xray client.
                // The API accepts either an ID or an issue key; here we pass the key.
                return options.Xray.GetTest(idOrKey: key);
            }

            // This system tool exposes read-only metadata about Xray-integrated
            // MCP tools. It performs a lookup against the internal tool registry
            // using the supplied tool name and returns the corresponding
            // tool definition when found.
            [SystemTool("get_xray_tool_metadata")]
            public static object GetXrayTool(InvokeOptions options)
            {
                // Extract the tool name argument provided by the caller.
                // This value is used as the lookup key in the internal tool registry.
                var toolName = options.Arguments
                    .GetProperty("toolName")
                    .GetString();

                // Attempt to resolve the tool metadata from the registry.
                // If the tool is not found, return null to indicate
                // that no matching tool exists.
                return s_tools.TryGetValue(key: toolName, out McpToolModel tool)
                    ? tool
                    : null;
            }

            // Creates a new Xray test case in Jira based on the provided invocation options.
            // The invocation context containing the resolved Jira project and the
            // serialized test case definition supplied by the caller.
            [SystemTool("new_xray_test")]
            public static object NewXrayTest(InvokeOptions options)
            {
                // Extract the Jira project identifier from the invocation arguments.
                // This value determines which Jira project the new test case will belong to.
                var project = options.Arguments.GetProperty("project").GetString();

                // Deserialize the test case definition from the invocation arguments
                // into a strongly typed domain model using the configured JSON options.
                var testCase = options.Arguments.Deserialize<TestCaseModel>(AppSettings.JsonOptions);

                // Delegate the creation of the test case to the Xray repository,
                // which handles communication with the underlying Xray API.
                return options.Xray.NewTest(project, testCase);
            }

            // Creates a new Xray test plan in Jira based on the provided invocation options.
            // The invocation context containing the resolved Jira project and the
            // serialized test plan definition supplied by the caller.
            [SystemTool("new_xray_test_plan")]
            public static object NewXrayTestPlan(InvokeOptions options)
            {
                // Extract the Jira project identifier from the invocation arguments.
                // This value determines which Jira project the new test plan will belong to.
                var project = options.Arguments.GetProperty("project").GetString();

                // Deserialize the test plan definition from the invocation arguments
                // into a strongly typed domain model using the configured JSON options.
                var testPlan = options.Arguments.Deserialize<NewTestPlanModel>(AppSettings.JsonOptions);

                // Delegate the creation of the test plan to the Xray repository,
                // which handles communication with the underlying Xray API.
                return options.Xray.NewTestPlan(project, testPlan);
            }

            // Resolves an Xray Test Repository folder path to its corresponding folder identifier
            // within the specified Jira project.
            [SystemTool("resolve_xray_folder_path")]
            public static object ResolveFolderPath(InvokeOptions options)
            {
                // Extract the Jira project identifier or key from the invocation arguments.
                // This value defines the scope of the Xray Test Repository.
                var project = options.Arguments.GetProperty("project").GetString();

                // Extract the hierarchical repository folder path to resolve.
                // The path is expected to use forward slashes as separators.
                var path = options.Arguments.GetProperty("path").GetString();

                // Delegate the resolution logic to the Xray repository implementation,
                // which handles interaction with the Xray Test Repository structure.
                return options.Xray.ResolveFolderPath(project, path);
            }

            // Creates a new folder in the Xray Test Repository for the specified Jira project.
            [SystemTool("new_xray_test_repository_folder")]
            public static object NewXrayTestRepositoryFolder(InvokeOptions options)
            {
                // Extract the Jira project identifier or project key from the invocation arguments.
                // This value defines the scope of the Xray Test Repository.
                var project = options.Arguments.GetProperty("project").GetString();

                // Extract the display name of the new repository folder to be created.
                var name = options.Arguments.GetProperty("name").GetString();

                // Attempt to extract the parent repository folder path.
                // When not provided, the folder will be created at the repository root.
                var hasPath = options.Arguments.TryGetProperty("path", out JsonElement pathOut);

                // Normalize the path value so downstream logic can treat an empty path
                // as a request to create the folder at the root level.
                var path = hasPath ? pathOut.GetString() : string.Empty;

                // Delegate the folder creation logic to the Xray repository implementation,
                // which handles path resolution and interaction with the internal Xray API.
                return options.Xray.NewTestRepositoryFolder(project, name, path);
            }

            // Updates an existing Xray test case in Jira using the provided invocation context.
            // The invocation arguments must include the test key and the updated test case definition.
            [SystemTool("update_xray_test")]
            public static object UpdateXrayTest(InvokeOptions options)
            {
                // Extract the Jira issue key that uniquely identifies the existing Xray test.
                // This key is required in order to locate and update the correct test entity.
                var key = options.Arguments.GetProperty("key").GetString();

                // Deserialize the updated test case definition from the invocation arguments
                // into a strongly typed domain model using the configured JSON options.
                var testCase = options.Arguments.Deserialize<TestCaseModel>(AppSettings.JsonOptions);

                // Delegate the update operation to the Xray repository, which encapsulates
                // all Jira and Xray-specific update logic and communication.
                return options.Xray.UpdateTest(key, testCase);
            }
        }
        #endregion

        #region *** Attributes   ***
        /// <summary>
        /// Custom attribute used to mark properties that represent system tools.
        /// This attribute is applied to properties to indicate that they correspond to a system tool, 
        /// which can be used for automation or other tool-based operations.
        /// </summary>
        /// <param name="name">The name of the system tool.</param>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
        private sealed class SystemToolAttribute(string name) : Attribute
        {
            /// <summary>
            /// Gets the name of the system tool.
            /// </summary>
            public string Name { get; } = name;
        }
        #endregion
    }
}
