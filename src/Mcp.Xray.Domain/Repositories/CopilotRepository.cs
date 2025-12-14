using Mcp.Xray.Domain.Models;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;

namespace Mcp.Xray.Domain.Repositories
{
    public class CopilotRepository : ICopilotRepository
    {
        #region *** Constants    ***
        // The JSON-RPC protocol version used in all responses.
        private const string JsonRpcVersion = "2.0";

        // Static, atomically swappable snapshot
        private static readonly ConcurrentDictionary<string, McpToolModel> s_tools = FormatTools();
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
        public CopilotInitializeResponseModel Initialize(object id) => new()
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
            // Match the tool by name and execute the corresponding handler.
            // Some tools are built-in system tools, others are dynamically loaded plugins.
            var result = new ToolOutputSchema(); //tools.InvokeTool(parameters);

            // Construct and return the JSON-RPC response to the client.
            return new()
            {
                // Echo back the request ID so the caller can match responses to requests.
                Id = id,

                // JSON-RPC version identifier.
                Jsonrpc = JsonRpcVersion,

                // The result object contains both a text-serialized version and the structured data.
                Result = new ToolOutputSchema.ToolOutputResultSchema
                {
                    // Plain text content (serialized JSON of the result object).
                    Content = new[]
                    {
                        new
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(result, ICopilotRepository.JsonOptions)
                        }
                    },

                    // The raw structured content (original object form of the result).
                    StructuredContent = result
                }
            };
        }

        // Formats the tools available in the G4 framework, combining both
        // plugin-based tools and built-in system tools.
        static ConcurrentDictionary<string, McpToolModel> FormatTools()
        {
            // Use reflection to retrieve all properties from the SystemTools class
            // Filter properties that have the SystemToolAttribute applied
            McpToolModel[] systemTools = [.. typeof(SystemTools).GetProperties()
                    .Where(i => i.GetCustomAttributes(typeof(SystemToolAttribute), false).Length != 0)
                    .Select(i => i.GetValue(null) as McpToolModel)
                    .Where(i => i != null)];

            foreach (var tool in systemTools)
            {
                // Ensure each system tool has its Metadata property set to a read-only dictionary.
                tool.Metadata = new()
                {
                    Description = tool.Description,
                    Name = tool.Name
                };
            }

            // Create a concurrent dictionary to hold all tools, ensuring thread-safe access.

            var toolsCollection = new ConcurrentDictionary<string, McpToolModel>(StringComparer.OrdinalIgnoreCase);

            // Combine plugin-based tools with system tools and populate the registry
            foreach (var tool in systemTools)
            {
                // Add or overwrite entry by tool name
                toolsCollection[tool.Name] = tool;
            }

            // Return the populated tools collection.
            return toolsCollection;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents a collection of system tools that are joined into the G4 framework.
        /// </summary>
        private static class SystemTools
        {
            /// <summary>
            /// 
            /// </summary>
            [SystemTool("convert_to_rule")]
            public static McpToolModel ConvertToRulesTool => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "convert_to_rule",

                /// <summary>
                /// A brief description of what the tool does.
                /// This tool converts a given tool name and parameters into a G4 rule model,
                /// which can then be executed within the G4 automation framework.
                /// </summary>
                Description = "Converts a tool name and parameters into a G4 rule model. " +
                    "This is used to translate high-level tool invocations into executable rules within the G4 framework.",

                /// <summary>
                /// Defines the input schema for the tool, including the types and descriptions of input parameters.
                /// </summary>
                InputSchema = new()
                {
                    Type = "object",
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["rule"] = new()
                        {
                            Type = ["object"],
                            Description = "The rule object containing the tool name parameters and properties to convert."
                        }
                    },
                    Required = ["rule"]
                },

                /// <summary>
                /// Defines the output schema for the tool, including the types and descriptions of output parameters.
                /// </summary>
                OutputSchema = new()
                {
                    Type = "object",
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["rule"] = new()
                        {
                            Type = ["object"],
                            Description = "The converted G4 rule model ready for execution."
                        }
                    },
                    Required = ["rule"]
                }
            };

            /// <summary>
            /// Represents a system tool that retrieves the metadata and schema for a specific tool by its unique name.
            /// This tool provides detailed information about the tool, including its input/output schema, name, and description.
            /// </summary>
            [SystemTool(name: "find_tool")]
            public static McpToolModel FindToolTool => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "find_tool",

                /// <summary>
                /// A brief description of what the tool does.
                /// This tool retrieves the metadata and schema for a specific tool identified by its unique name.
                /// </summary>
                Description = "Retrieves the metadata and schema for a tool. " +
                    "Uses the tool name if available, otherwise falls back to intent matching to find the best match.",

                /// <summary>
                /// Defines the input schema for the tool, including the types and descriptions of input parameters.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type for the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of input parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["intent"] = new()
                        {
                            Type = ["string"],
                            Description = "The intent or purpose for which the tool is being sought."
                        },
                        ["tool_name"] = new()
                        {
                            Type = ["string"],
                            Description = "The unique identifier of the tool to find."
                        }
                    },

                    /// <summary>
                    /// A list of required input parameters that must be provided for the tool to execute successfully.
                    /// </summary>
                    Required = ["tool_name"]
                },

                /// <summary>
                /// Defines the output schema for the tool, including the types and descriptions of output parameters.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type for the output parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["tool"] = new()
                        {
                            Type = ["object"],
                            Description = "The tool's metadata including name, description, input and output schemas."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters that must be included in the tool's response.
                    /// </summary>
                    Required = ["tool"]
                }
            };

            /// <summary>
            /// Represents a system tool that retrieves the full HTML markup of the application's Document Object Model (DOM)
            /// for the current browser session. Useful for inspecting or analyzing the current state of the loaded web page.
            /// </summary>
            [SystemTool(name: "get_application_dom")]
            public static McpToolModel GetApplicationDomTool => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "get_application_dom",

                /// <summary>
                /// A brief description of what the tool does and its intended use.
                /// </summary>
                Description = "Retrieves the full HTML markup of the application's Document Object Model (DOM) for the current browser session." +
                    "Useful for inspecting or analyzing the current state of the loaded web page.",

                /// <summary>
                /// Defines the input schema for the tool, including the types and descriptions of input parameters.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type for the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of input parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The unique session ID associated with the current browser session. " +
                                "This ID is used to retrieve the appropriate browser driver for interacting with " +
                                "the session and performing automation tasks."
                        },

                        ["token"] = new()
                        {
                            Type = ["string"],
                            Description = "The G4 Authentication token used to authenticate the session initiation process. " +
                                "This is required to authorize the session creation."
                        }
                    },

                    /// <summary>
                    /// A list of required input parameters that must be provided for the tool to execute successfully.
                    /// </summary>
                    Required = ["driver_session", "token"]
                },

                /// <summary>
                /// Defines the output schema for the tool, including the types and descriptions of output parameters.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type for the output parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The session key for the browser session from which the DOM was retrieved."
                        },

                        ["value"] = new()
                        {
                            Type = ["string"],
                            Description = "A string containing the full HTML markup of the page’s Document Object Model."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters that must be included in the tool's response.
                    /// </summary>
                    Required = ["driver_session", "value"]
                }
            };

            /// <summary>
            /// Represents a system tool that gets instructions for the next tool call.
            /// </summary>
            [SystemTool(name: "get_instructions")]
            public static McpToolModel GetInstructionsTool => new()
            {
                /// <summary>
                /// Unique tool name used by the agent/runtime to select and invoke this system tool.
                /// </summary>
                Name = "get_instructions",

                /// <summary>
                /// Returns authoritative, versioned policy that governs the *next* tool call.
                /// Must be invoked immediately before any tool call so the agent can merge defaults,
                /// apply guards, and enforce mandatory behaviors.
                /// </summary>
                Description = "Returns authoritative, versioned policy for the next tool call. Must be invoked immediately before any tool call.",

                /// <summary>
                /// Input schema for this tool.
                /// This tool is side-effect free and requires no inputs; it only returns policy.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// Root type of the input payload. No properties are expected.
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// No input properties — the policy is derived from server-side configuration.
                    /// </summary>
                    Properties = [],

                    /// <summary>
                    /// No required inputs — call with an empty object.
                    /// </summary>
                    Required = []
                },

                /// <summary>
                /// Output schema describing the policy object that must be honored by the caller.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// Root type of the returned policy payload.
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// Policy fields:
                    ///  - policy_version: Identifies the policy document/version in force.
                    ///  - defaults: Baseline arguments to inject into the upcoming tool call.
                    ///  - guards: Validations and preconditions that must pass before calling a tool.
                    ///  - must: Non-negotiable behavioral rules the agent must follow.
                    ///  - ttl_seconds: How long this policy remains valid.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        /// <summary>
                        /// Version metadata for the returned policy.
                        /// Recommended fields (not strictly enforced by schema):
                        ///  - id (string): Stable policy identifier (e.g., "g4/agent/policy").
                        ///  - rev (string): Revision tag or semantic version (e.g., "2025.08.13-rc1").
                        ///  - issued_at (string): ISO-8601 timestamp when this policy was generated.
                        /// The agent should log this for traceability and cache invalidation.
                        /// </summary>
                        ["policy_version"] = new()
                        {
                            Type = ["object"],
                            Description = "Version metadata for the policy (e.g., id, rev, issued_at) for traceability and cache control."
                        },

                        /// <summary>
                        /// Baseline parameters that MUST be merged (caller-supplied values may override where allowed)
                        /// into the very next tool call. Typical keys:
                        ///  - driver (string): Default driver name (e.g., 'ChromeDriver').
                        ///  - driver_binaries (string): Default driver endpoint/path (e.g., 'http://localhost:4444/wd/hub').
                        ///  - token (string): Authentication token to attach to requests.
                        ///  - session (string): Preferred session ID; if missing, a session must be created.
                        ///  - timeouts (object): Default operation timeouts.
                        ///  - retries (object): Default retry policy (max attempts, backoff).
                        /// Use together with 'must' to determine which fields are mandatory vs. overridable.
                        /// </summary>
                        ["defaults"] = new()
                        {
                            Type = ["object"],
                            Description = "Baseline arguments (driver, driver_binaries, token, session, timeouts, retries) to inject into the next tool call."
                        },

                        /// <summary>
                        /// Preconditions and validations that MUST succeed before the next tool call proceeds.
                        /// Examples:
                        ///  - require_fields: ['token'] (ensure security-critical args exist).
                        ///  - allowed_tools: ['start_g4_session','get_tools','find_tool','...'].
                        ///  - session_state: 'existing' | 'new' (enforce session reuse or creation).
                        ///  - token_scope: required scopes/claims.
                        /// If any guard fails, the agent must abort the call and surface a clear error.
                        /// </summary>
                        ["guards"] = new()
                        {
                            Type = ["object"],
                            Description = "Validation rules (required fields, allowed tools, session/token checks). If any fail, the call must be aborted."
                        },

                        /// <summary>
                        /// Non-negotiable instructions the agent MUST follow for the next call.
                        /// Examples:
                        ///  - always include 'token' (fetch from .env if absent; otherwise prompt).
                        ///  - if 'session' is missing, call 'start_g4_session' first using defaults.
                        ///  - call order: get_tools → find_tool → build request → attach_session → call.
                        ///  - log policy_version and chosen tool name for audit.
                        /// These rules supersede caller preferences to ensure safety and correctness.
                        /// </summary>
                        ["must"] = new()
                        {
                            Type = ["object"],
                            Description = "Hard requirements (e.g., include token, ensure/attach driver_session, enforce call order, audit logging)."
                        },

                        /// <summary>
                        /// Time-to-live in seconds for this policy document.
                        /// The agent must re-fetch policy once TTL expires (or sooner if invalidated by server).
                        /// Short TTLs ensure the agent honors rapid config/security changes.
                        /// </summary>
                        ["ttl_seconds"] = new()
                        {
                            Type = ["number"],
                            Description = "Policy lifetime in seconds; agent must re-fetch policy after expiry."
                        }
                    },

                    /// <summary>
                    /// Fields that are guaranteed to be present in a valid response.
                    /// The agent may treat 'policy_version' as optional for forward compatibility.
                    /// </summary>
                    Required = ["defaults", "guards", "must", "ttl_seconds"]
                }
            };

            /// <summary>
            /// Represents a system tool that retrieves an optimal locator for a specific element on a web page.
            /// </summary>
            [SystemTool(name: "get_locator")]
            public static McpToolModel GetLocatorTool => new()
            {
                /// <summary>
                /// Unique tool name used to identify this system tool in the runtime and during tool selection.
                /// </summary>
                Name = "get_locator",

                /// <summary>
                /// Retrieves an optimal locator for a specific element on a web page,
                /// taking into account given constraints, driver session, and intended action.
                /// Locators are chosen to maximize reliability and stability across DOM changes.
                /// </summary>
                Description = "Retrieves a locator for a specific element on the page based on the provided parameters.",

                /// <summary>
                /// Defines the expected input format, including constraints, session details, intent, and authentication.
                /// </summary>
                InputSchema = new()
                {
                    Type = "object",
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["constraints"] = new()
                        {
                            Type = ["object"],
                            Description = "Optional rules influencing how the locator is selected, such as " +
                                "visibility, enablement, and allowed/disallowed strategies.",
                            Properties = new()
                            {
                                ["must_be_visible"] = new()
                                {
                                    Type = ["boolean"],
                                    Description = "If true, the element must be visible on the page to " +
                                        "qualify as a valid locator.",
                                    Default = true
                                },
                                ["must_be_enabled"] = new()
                                {
                                    Type = ["boolean"],
                                    Description = "If true, the element must not be disabled to qualify as a valid locator.",
                                    Default = true
                                },
                                ["prefer"] = new()
                                {
                                    Type = ["array"],
                                    Description = "Preferred locator strategies, in priority order. The first " +
                                        "matching strategy will be used.",
                                    Items = new()
                                    {
                                        Type = "string",
                                        Enum = ["data-testid", "aria", "id", "label", "role", "text", "css", "xpath"]
                                    },
                                },
                                ["forbid"] = new()
                                {
                                    Type = ["array"],
                                    Description = "Locator strategies to avoid. Helps prevent use of " +
                                        "brittle or unstable locators.",
                                    Items = new()
                                    {
                                        Type = "string",
                                        Enum = ["nth-child", "brittle-css"]
                                    }
                                }
                            }
                        },
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "Unique identifier for the active browser session in which the " +
                                "locator search is performed."
                        },
                        ["intent"] = new()
                        {
                            Type = ["string"],
                            Description = "Description of the intended interaction with the element, e.g., " +
                                "'click login button' or 'type into search field'."
                        },
                        ["openai_api_key"] = new()
                        {
                            Type = ["string"],
                            Description = "OpenAI authentication token for verifying and authorizing the locator retrieval request."
                        },
                        ["openai_model"] = new()
                        {
                            Type = ["string"],
                            Description = "Specifies the OpenAI model identifier (e.g., 'gpt-4o', 'gpt-4.1-mini')."
                        },
                        ["openai_uri"] = new()
                        {
                            Type = ["string"],
                            Description = "OpenAI API endpoint URI for the request."
                        },
                        ["token"] = new()
                        {
                            Type = ["string"],
                            Description = "G4 authentication token for verifying and authorizing the locator retrieval request."
                        }
                    },
                    Required = ["driver_session", "intent", "token", "openai_api_key", "openai_model", "openai_uri"]
                },

                /// <summary>
                /// Defines the shape and meaning of the locator output.
                /// </summary>
                OutputSchema = new()
                {
                    Type = "object",
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The browser session identifier associated with this locator result."
                        },
                        ["value"] = new()
                        {
                            Type = ["object"],
                            Description = "Locator details, including primary and fallback strategies, along with " +
                                "human-readable context.",
                            Properties = new()
                            {
                                ["primary_locator"] = new()
                                {
                                    Type = ["object"],
                                    Description = "Main locator used to find the element, optimized for reliability.",
                                    Properties = new()
                                    {
                                        ["value"] = new()
                                        {
                                            Type = ["string"],
                                            Description = "Locator string (e.g., CSS selector or XPath) used in element " +
                                                "identification."
                                        },
                                        ["using"] = new()
                                        {
                                            Type = ["string"],
                                            Description = "Locator strategy type, such as 'css', 'xpath', or 'id'.",
                                            Enum = ["CssSelector", "Xpath", "Id"]
                                        }
                                    }
                                },
                                ["fallback_locator"] = new()
                                {
                                    Type = ["object"],
                                    Description = "Alternative locator to use if the primary locator fails. Useful for " +
                                        "elements with multiple stable selectors.",
                                    Properties = new()
                                    {
                                        ["value"] = new()
                                        {
                                            Type = ["string"],
                                            Description = "Fallback locator string (e.g., CSS selector or XPath)."
                                        },
                                        ["using"] = new()
                                        {
                                            Type = ["string"],
                                            Description = "Strategy type used by the fallback locator.",
                                            Enum = ["CssSelector", "Xpath", "Id"]
                                        }
                                    }
                                },
                                ["description"] = new()
                                {
                                    Type = ["string"],
                                    Description = "Optional human-readable description of the element being located, " +
                                        "for debugging and clarity."
                                }
                            }
                        }
                    },
                    Required = ["driver_session", "value"]
                }
            };

            /// <summary>
            /// Represents a system tool that starts the execution of a G4 rule using the provided parameters.
            /// The rule execution involves interacting with the browser session and processing the rule's logic to produce a result.
            /// </summary>
            [SystemTool(name: "start_g4_rule")]
            public static McpToolModel StartG4RuleTool => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "start_g4_rule",

                /// <summary>
                /// A brief description of what the tool does.
                /// This tool initiates the execution of a G4 rule based on the provided session, authentication token, and rule configuration.
                /// </summary>
                Description = "Starts a G4 rule execution with the provided parameters.",

                /// <summary>
                /// Defines the input schema for the tool, including the types and descriptions of input parameters.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type for the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of input parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The unique session ID associated with the current browser session. " +
                                "This ID is used to retrieve the appropriate browser driver for interacting with " +
                                "the session and performing automation tasks."
                        },

                        ["token"] = new()
                        {
                            Type = ["string"],
                            Description = "The G4 Authentication token used to authenticate the session initiation process. " +
                                "This is required to authorize the session creation."
                        },

                        ["rule"] = new()
                        {
                            Type = ["object"],
                            Description = "The G4 rule to be executed, including its parameters and configuration.",
                            Properties = new(StringComparer.OrdinalIgnoreCase)
                            {
                                ["tool_name"] = new()
                                {
                                    Type = ["string"],
                                    Description = "The unique identifier of the tool (plugin) that defines the rule to be executed."
                                },
                                ["parameters"] = new()
                                {
                                    Type = ["object"],
                                    Description = "The parameters to be passed to the rule during execution."
                                },
                                ["properties"] = new()
                                {
                                    Type = ["object"],
                                    Description = "Additional properties and configuration for the rule."
                                }
                            }
                        }
                    },

                    /// <summary>
                    /// A list of required input parameters that must be provided for the tool to execute successfully.
                    /// </summary>
                    Required = ["driver_session", "token", "rule"]
                },

                /// <summary>
                /// Defines the output schema for the tool, including the types and descriptions of output parameters.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type for the output parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "The session key for the browser session from which the rule was executed."
                        },

                        ["value"] = new()
                        {
                            Type = ["object"],
                            Description = "The result of the rule execution, including any output or response data."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters that must be included in the tool's response.
                    /// </summary>
                    Required = ["driver_session", "value"]
                }
            };

            /// <summary>
            /// This tool starts a new G4 session by using specified driver binaries, browser (platform) name, and headless option.
            /// The session is initiated with a given G4 authentication token for secure access.
            /// </summary>
            [SystemTool(name: "start_g4_session")]
            public static McpToolModel StartG4SessionTool => new()
            {
                /// <summary>
                /// The unique name of the tool. Used to identify the tool in the system.
                /// </summary>
                Name = "start_g4_session",

                /// <summary>
                /// A short description of what the tool does. Provides context for its use in automation workflows.
                /// </summary>
                Description = "Starts a new G4 session using specified driver binaries, browser (platform) name, and headless option.",

                /// <summary>
                /// Defines the expected input parameters required by this tool to start the G4 session.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type of the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of input parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver"] = new()
                        {
                            Type = ["string"],
                            Description = "The name of the browser driver to use (e.g., ChromeDriver, GeckoDriver). " +
                                "This is necessary for the automation to interact with the browser."
                        },

                        ["driver_binaries"] = new()
                        {
                            Type = ["string"],
                            Description = "The path to the browser driver executable (e.g., ChromeDriver) or the URL of the Selenium Grid endpoint. " +
                                "This is necessary for the automation to interact with the browser."
                        },

                        ["token"] = new()
                        {
                            Type = ["string"],
                            Description = "The G4 Authentication token used to authenticate the session initiation process. " +
                                "This is required to authorize the session creation."
                        }
                    },

                    /// <summary>
                    /// A list of required input parameters. These parameters must be provided for the tool to execute successfully.
                    /// </summary>
                    Required = ["driver", "driver_binaries", "token"]
                },

                /// <summary>
                /// Defines the expected output schema after the tool has successfully run.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type of the input parameters (an object in this case).
                    /// </summary
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["driver_session"] = new()
                        {
                            Type = ["string"],
                            Description = "A unique identifier assigned to the newly created browser session. " +
                                "This identifier can be used for further interaction with the session."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters. These parameters must be present in the tool's response.
                    /// </summary>
                    Required = ["driver_session"]
                }
            };

            /// <summary>
            /// Represents a system tool that retrieves the full list of available tools that the Copilot agent can invoke.
            /// This tool provides the metadata and schemas for all the tools that are available in the Copilot environment.
            /// </summary>
            [SystemTool(name: "get_tools")]
            public static McpToolModel GetToolsTool => new()
            {
                /// <summary>
                /// The unique name of the tool, used to identify it within the system.
                /// </summary>
                Name = "get_tools",

                /// <summary>
                /// A brief description of what the tool does.
                /// This tool returns the full list of tools that the Copilot agent can invoke, including their metadata and schemas.
                /// </summary>
                Description = "Retrieves the full list of available tools that the Copilot agent can invoke.",

                /// <summary>
                /// Defines the input schema for the tool, including the types and descriptions of input parameters.
                /// </summary>
                InputSchema = new()
                {
                    /// <summary>
                    /// The data type for the input parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// An empty list of properties, as this tool does not require any specific input parameters.
                    /// </summary>
                    Properties = [],

                    /// <summary>
                    /// No required input parameters for this tool.
                    /// </summary>
                    Required = []
                },

                /// <summary>
                /// Defines the output schema for the tool, including the types and descriptions of output parameters.
                /// </summary>
                OutputSchema = new()
                {
                    /// <summary>
                    /// The data type for the output parameters (an object in this case).
                    /// </summary>
                    Type = "object",

                    /// <summary>
                    /// A dictionary of output parameters with their names and descriptions.
                    /// </summary>
                    Properties = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["tools"] = new()
                        {
                            Type = ["array", "object"],
                            Description = "An array of tool objects, each containing name, description, input and output schemas."
                        }
                    },

                    /// <summary>
                    /// A list of required output parameters. "tools" is required as the main result of the tool.
                    /// </summary>
                    Required = ["tools"]
                }
            };
        }
        #endregion

        #region *** Attributes   ***
        /// <summary>
        /// Custom attribute used to mark properties that represent system tools.
        /// This attribute is applied to properties to indicate that they correspond to a system tool, 
        /// which can be used for automation or other tool-based operations.
        /// </summary>
        /// <param name="name">The name of the system tool.</param>
        [AttributeUsage(AttributeTargets.Property)]
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
