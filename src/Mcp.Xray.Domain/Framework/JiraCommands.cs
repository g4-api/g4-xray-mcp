using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;

namespace Mcp.Xray.Domain.Framework
{
    /// <summary>
    /// Provides factory methods for building Jira HTTP commands.
    /// This class centralizes the construction of REST API requests used for
    /// issue operations, transitions, worklogs, attachments, and user actions.
    /// </summary>
    internal static class JiraCommands
    {
        #region *** Fields  ***
        // The base route for all Jira API calls, constructed using the configured API version.
        private static readonly string _baseRoute = $"/rest/api/{AppSettings.JiraOptions.ApiVersion}";
        #endregion

        #region *** Methods ***
        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that adds a comment to a Jira issue.  
        /// The method prepares the JSON structure expected by Jira's update endpoint 
        /// and returns a command object containing the route, HTTP method, and payload.
        /// </summary>
        /// <param name="idOrKey">The identifier or key of the Jira issue, such as "TES-12".</param>
        /// <param name="comment">The text content of the comment that will be added to the issue.</param>
        /// <returns>A fully prepared <see cref="HttpCommand"/> that can be executed against the Jira API to append the specified comment to the issue.</returns>
        public static HttpCommand AddComment(string idOrKey, string comment)
        {
            // Builds the body required by Jira. 
            // Jira expects an object containing an "update" section, where "comment" holds
            // an array containing an "add" operation. The "body" field inside that operation
            // contains the comment text.
            var data = new
            {
                Update = new
                {
                    Comment = new[]
                    {
                        new
                        {
                            Add = new
                            {
                                Body = comment
                            }
                        }
                    }
                }
            };

            // Creates the HttpCommand using the PUT method and the correct route for updating an issue.
            // The command object carries the payload above so that Jira can process the comment addition.
            return new HttpCommand
            {
                Data = data,
                Method = HttpMethod.Put,
                Route = $"{_baseRoute}/issue/{idOrKey}"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that performs a Jira issue search using the provided JQL.
        /// The method prepares the correct request payload and configures the command to call the Jira search endpoint.
        /// </summary>
        /// <param name="jql">The Jira Query Language expression that defines the search criteria.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that can be executed to retrieve issues matching the specified JQL expression.</returns>
        public static HttpCommand FindIssues(string jql) => FindIssues(jql, "*all");

        /// <summary>
        /// Builds a Jira search command that executes a JQL query and returns matching issues.
        /// </summary>
        /// <param name="jql">The JQL expression used to filter issues.</param>
        /// <param name="fields">Optional list of fields to return. If empty, Jira will decide the default set.</param>
        /// <returns>An <see cref="HttpCommand"/> configured for Jira's search API.</returns>
        public static HttpCommand FindIssues(string jql, params string[] fields) => new()
        {
            // Jira requires the JQL query to be wrapped in a JSON body.
            Data = new
            {
                Jql = jql,
                Fields = fields.Length > 0 ? fields : []  // Use explicit field selection when provided.
            },

            // Jira's search API is accessed via POST, even when simply querying.
            Method = HttpMethod.Post,

            // Build the full route to the JQL search endpoint.
            Route = $"{_baseRoute}/search/jql"
        };

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves all users who can be assigned 
        /// to the specified Jira issue. The method prepares the correct route for the 
        /// assignable user search endpoint and returns a command configured for a GET request.
        /// </summary>
        /// <param name="key">The key or identifier of the Jira issue for which assignable users should be queried.</param>
        /// <returns>A fully configured <see cref="HttpCommand"/> that calls the Jira API to obtain the list of users eligible for assignment to the given issue.</returns>
        public static HttpCommand GetAssignableUsers(string key)
        {
            // Returns a command object configured for a GET request with no request body,
            // targeting the formatted route above.
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = $"{_baseRoute}/user/assignable/search?issueKey={key}"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves Jira create-meta information 
        /// for a specific project. This metadata describes the available issue types and 
        /// the fields required when creating new issues.
        /// </summary>
        /// <param name="project">The key of the Jira project for which the create-meta information is requested.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that performs a GET request to fetch the project's create-meta data from the Jira API.</returns>
        public static HttpCommand GetCreateMeta(string project)
        {
            // Returns a GET command with no body. The route is constructed by applying
            // the specified project key to the template above.
            return new()
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = $"{_baseRoute}/issue/createmeta?projectKeys={project}&expand=projects.issuetypes.fields"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves a Jira issue by its ID or key.
        /// The method optionally allows requesting specific fields, and constructs the route 
        /// accordingly. The returned command is configured for an HTTP GET request.
        /// </summary>
        /// <param name="idOrKey">The identifier or key of the Jira issue to retrieve.</param>
        /// <param name="fields">Optional field names to limit the response to specific properties. If no fields are provided, Jira returns the full issue representation.</param>
        /// <returns>A fully configured <see cref="HttpCommand"/> that can be executed against the Jira API to fetch the requested issue, with or without field filtering.</returns>
        public static HttpCommand GetIssue(string idOrKey, params string[] fields)
        {
            // If fields were supplied, the query string contains a comma-separated list.
            // If no fields are provided, the query string remains empty.
            var queryString = fields.Length > 0
                ? $"?fields={string.Join(",", fields)}"
                : string.Empty;

            // The constructed route is applied to a GET command.
            // No request body is required for this operation.
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = $"{_baseRoute}/issue/{idOrKey}{queryString}"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that requests a token for a specific Jira issue.
        /// The method loads an embedded text template, replaces placeholder values with the
        /// provided project and issue keys, and sends the resulting content as the request body.
        /// </summary>
        /// <param name="project">The Jira project key that is inserted into the token request template.</param>
        /// <param name="issue">The Jira issue key that is inserted into the token request template.</param>
        /// <returns>A fully constructed <see cref="HttpCommand"/> configured for an HTTP POST request, containing the processed token request payload.</returns>
        public static HttpCommand GetToken(string project, string issue)
        {
            // Reads the embedded resource file that contains the base request template.
            // The text placeholders are replaced with the actual project and issue keys.
            var data = Assembly
                .GetExecutingAssembly()
                .ReadEmbeddedResource("get_token.txt")
                .Replace("[project-key]", project)
                .Replace("[issue-key]", issue);

            // Returns the command configured with the processed template as the request body,
            // a POST method, and the defined route for token operations.
            return new()
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = "/rest/gira/1/"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that retrieves the available workflow 
        /// transitions for the specified Jira issue. The method prepares the appropriate 
        /// route and returns a command configured for an HTTP GET request.
        /// </summary>
        /// <param name="idOrKey">The Jira issue identifier or key used to locate the issue whose transitions are requested.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that can be executed to obtain the transitions available for the given issue.</returns>
        public static HttpCommand GetTransitions(string idOrKey)
        {
            // Returns a command with no request body and a GET method,
            // targeting the transitions endpoint for the specified issue.
            return new HttpCommand
            {
                Data = default,
                Method = HttpMethod.Get,
                Route = $"{_baseRoute}/issue/{idOrKey}/transitions"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that submits a request to create a new Jira issue.
        /// The method receives an object representing the issue payload and prepares a POST command 
        /// targeting the Jira issue creation endpoint.
        /// </summary>
        /// <param name="data">The issue definition that will be sent to Jira. This object should match the structure expected by the Jira create-issue API.</param>
        /// <returns>A configured <see cref="HttpCommand"/> containing the provided payload, ready to be executed against the Jira create-issue endpoint.</returns>
        public static HttpCommand NewIssue(object data)
        {
            // Builds a command using the supplied issue data. 
            // Jira's create-issue operation is handled through a POST request to the API versioned route.
            return new()
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = $"{_baseRoute}/issue"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that establishes a relationship between two Jira issues.
        /// The method prepares the request body using the supplied link type, issue keys, and optional comment,
        /// then returns a command configured for the Jira issue-link endpoint.
        /// </summary>
        /// <param name="linkType">The name of the link type that defines the relationship, such as "Relates" or "Blocks".</param>
        /// <param name="inward">The key of the issue on the inward side of the link.</param>
        /// <param name="outward">The key of the issue on the outward side of the link.</param>
        /// <param name="comment">An optional text comment describing the link or providing context.</param>
        /// <returns>A fully prepared <see cref="HttpCommand"/> that can be executed to create the link between the specified Jira issues.</returns>
        public static HttpCommand NewIssueLink(string linkType, string inward, string outward, string comment)
        {
            // The JSON structure reflects Jira's expectations for creating an issue link.
            // It contains the link type, the inward and outward issue references, 
            // and a comment body if provided.
            var data = new
            {
                Type = new
                {
                    Name = linkType
                },
                InwardIssue = new
                {
                    Key = inward
                },
                OutwardIssue = new
                {
                    Key = outward
                },
                Comment = new
                {
                    Body = comment
                }
            };

            // Returns a POST command that carries the constructed payload
            // and targets the issueLink endpoint in the Jira REST API.
            return new()
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = $"{_baseRoute}/issueLink"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that performs a workflow transition on a Jira issue.
        /// The method builds the request payload based on the transition identifier, and optionally
        /// includes a resolution and a comment if such values are provided.
        /// </summary>
        /// <param name="idOrKey">The Jira issue identifier or key on which the transition will be executed.</param>
        /// <param name="transition">The identifier of the workflow transition that should be applied.</param>
        /// <param name="resolution">An optional resolution name to apply as part of the transition. It is only added if a value is provided.</param>
        /// <param name="comment">An optional comment to record during the transition. It is included only when the value is not empty.</param>
        /// <returns>A fully prepared <see cref="HttpCommand"/> that submits the transition request to Jira.</returns>
        public static HttpCommand NewTransition(string idOrKey, string transition, string resolution, string comment)
        {
            // The base structure always contains the transition identifier.
            var data = new Dictionary<string, object>
            {
                ["transition"] = new { Id = transition }
            };

            // The resolution is included only when the caller provides a value.
            if (!string.IsNullOrEmpty(resolution))
            {
                data["fields"] = new
                {
                    Resolution = new { Name = resolution }
                };
            }

            // The update block is added when a non-empty comment is supplied.
            // Jira expects comments to be wrapped inside an "update" section.
            if (!string.IsNullOrEmpty(comment))
            {
                data["update"] = new
                {
                    Comment = new[]
                    {
                        new { Add = new { Body = comment } }
                    }
                };
            }

            // The command is configured with the constructed payload, a POST method,
            // and the appropriate transitions endpoint for the selected issue.
            return new()
            {
                Data = data,
                Method = HttpMethod.Post,
                Route = $"{_baseRoute}/issue/{idOrKey}/transitions"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that submits a new worklog entry for a Jira issue.
        /// The method builds the worklog payload using the provided time duration and comment,
        /// and appends a timestamp to the request route to prevent response caching.
        /// </summary>
        /// <param name="id">The Jira issue identifier to which the worklog entry will be added.</param>
        /// <param name="timeSpentSeconds">The amount of time spent, expressed in seconds, that will be recorded in the worklog.</param>
        /// <param name="comment">The descriptive text that will appear inside the worklog entry.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that sends the worklog record to Jira using an HTTP POST request.</returns>
        public static HttpCommand NewWorklog(string id, double timeSpentSeconds, string comment)
        {
            // A millisecond-based epoch value is appended to the URL to ensure that the request 
            // bypasses caching layers. Jira recognizes this pattern and accepts it.
            var epoch = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();

            // Jira's modern worklog API expects the comment in Atlassian Document Format.
            // The structure below represents a document that contains a single paragraph
            // holding the provided comment text.
            var requestBody = new
            {
                TimeSpentSeconds = timeSpentSeconds,
                Comment = new
                {
                    Version = 1,
                    Type = "doc",
                    Content = new[]
                    {
                        new
                        {
                            Type = "paragraph",
                            Content = new[]
                            {
                                new
                                {
                                    Type = "text",
                                    Text = comment
                                }
                            }
                        }
                    }
                }
            };

            // The command is returned with the constructed request body,
            // a POST method, and the formatted route targeting the worklog endpoint.
            return new()
            {
                Data = requestBody,
                Method = HttpMethod.Post,
                Route = $"{_baseRoute}/issue/{id}/worklog?_r={epoch}"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that deletes an attachment from Jira.
        /// The method prepares a DELETE request targeting the attachment resource identified by the given identifier.
        /// </summary>
        /// <param name="id">The identifier of the attachment that should be removed.</param>
        /// <returns>A fully configured <see cref="HttpCommand"/> that performs the deletion of the specified Jira attachment.</returns>
        public static HttpCommand RemoveAttachment(string id)
        {
            // Returns a DELETE command with no request body. The route is resolved using the format above.
            return new()
            {
                Data = default,
                Method = HttpMethod.Delete,
                Route = $"{_baseRoute}/attachment/{id}"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that assigns a Jira issue to a specific user.
        /// The method builds the request payload using the provided account identifier 
        /// and prepares a PUT request targeting the issue's assignee endpoint.
        /// </summary>
        /// <param name="idOrKey">The Jira issue identifier or key whose assignee should be updated.</param>
        /// <param name="account">The Atlassian account identifier of the user who will become the new assignee.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that updates the assignee of the specified Jira issue.</returns>
        public static HttpCommand SetAssignee(string idOrKey, string account)
        {
            // The command uses a PUT method and includes the account ID as the request body.
            return new()
            {
                Data = new { AccountId = account },
                Method = HttpMethod.Put,
                Route = $"{_baseRoute}/issue/{idOrKey}/assignee"
            };
        }

        /// <summary>
        /// Creates an <see cref="HttpCommand"/> that updates the fields or properties of a Jira issue.
        /// The method accepts an object that represents the update structure expected by Jira and 
        /// prepares a PUT request targeting the issue's update endpoint.
        /// </summary>
        /// <param name="idOrKey">The Jira issue identifier or key that will be updated.</param>
        /// <param name="data">The update payload that follows Jira's issue update format.</param>
        /// <returns>A configured <see cref="HttpCommand"/> that performs the update operation on the specified Jira issue.</returns>
        public static HttpCommand UpdateIssue(string idOrKey, object data) => new()
        {
            // The update request uses a PUT method. The route points to the issue resource 
            // corresponding to the provided identifier or key.
            Data = data,
            Method = HttpMethod.Put,
            Route = $"{_baseRoute}/issue/{idOrKey}"
        };
        #endregion
    }
}
