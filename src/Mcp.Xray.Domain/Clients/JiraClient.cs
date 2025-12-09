using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Framework;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mcp.Xray.Domain.Clients
{
    /// <summary>
    /// High-level Jira client that wraps <see cref="JiraCommandInvoker"/> and exposes
    /// methods for common Jira operations.
    /// </summary>
    public class JiraClient
    {
        #region *** Fields         ***
        // Bucket size used for batched operations, read from application settings.
        private static readonly int _bucketSize = AppSettings.JiraOptions.BucketSize;

        // Default comment used when creating issues, including a timestamp for traceability.
        private static readonly string _createMessage =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC: Automatically created by G4 MCP Service.";

        // In-memory cache of project metadata keyed by project key (case-insensitive).
        private static readonly Dictionary<string, JObject> _projectMetaCache =
            new(StringComparer.OrdinalIgnoreCase);

        // Logger instance used for diagnostic and informational messages.
        private readonly ILogger _logger;
        #endregion

        #region *** Constructors   ***
        /// <summary>
        /// Initializes a <see cref="JiraClient"/> using explicit Jira connection details.
        /// </summary>
        /// <param name="baseUrl">The Jira base URL (Cloud or Server).</param>
        /// <param name="username">The Jira username or email.</param>
        /// <param name="apiKey">The API token or password used for authentication.</param>
        public JiraClient(string baseUrl, string username, string apiKey)
            : this(new() { Collection = baseUrl, Username = username, Password = apiKey })
        { }

        /// <summary>
        /// Initializes a <see cref="JiraClient"/> using a prepared authentication model.
        /// </summary>
        /// <param name="authentication">The authentication settings used to connect to Jira.</param>
        public JiraClient(JiraAuthenticationModel authentication)
            : this(authentication, logger: default)
        { }

        /// <summary>
        /// Initializes a <see cref="JiraClient"/> with authentication and optional logging.
        /// </summary>
        /// <param name="authentication">The Jira authentication configuration.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public JiraClient(JiraAuthenticationModel authentication, ILogger logger)
        {
            // Create the logger instance.
            _logger = logger;

            // Store the authentication model.
            Authentication = authentication;

            // Initialize the Jira command invoker with authentication.
            Invoker = new JiraCommandInvoker(authentication);

            // Load project metadata if a project key is present; otherwise fallback to an empty object.
            ProjectMeta = !string.IsNullOrEmpty(authentication.Project)
                ? GetProjectMeta(invoker: Invoker, project: authentication.Project)
                : JsonDocument.Parse("{}").RootElement;
        }
        #endregion

        #region *** Properties     ***
        /// <summary>
        /// Gets the Jira authentication settings used by this client.
        /// </summary>
        public JiraAuthenticationModel Authentication { get; }

        /// <summary>
        /// Gets the command invoker used to execute Jira operations.
        /// The invoker provides methods for sending commands to the Jira service. Use this
        /// property to perform custom or advanced Jira actions that are not directly exposed by higher-level APIs.
        /// </summary>
        public JiraCommandInvoker Invoker { get; }

        /// <summary>
        /// Gets the cached project metadata returned by Jira's <c>createmeta</c> API.
        /// </summary>
        public JsonElement ProjectMeta { get; }
        #endregion

        #region *** Methods        ***
        // TODO: Implement dynamic MIME type detection based on file extension or content.
        /// <summary>
        /// Adds one or more attachment files to the specified Jira issue.
        /// </summary>
        /// <param name="idOrKey">
        /// The Jira issue identifier or key (e.g., "BRIFF-123") to which the attachments will be added.
        /// </param>
        /// <param name="files">
        /// An array of local file paths representing the attachments to upload.
        /// Each file will be sent as multipart/form-data.
        /// </param>
        /// <returns>A <see cref="JsonElement"/> containing the Jira API response describing the uploaded attachments.</returns>
        public JsonElement AddAttachments(string idOrKey, params string[] files)
        {
            // Convert file paths into the tuple format expected by the JiraCommandInvoker:
            // (filePath, mimeType). For now, MIME type is statically set to "image/png".
            // In the future, this should be replaced with dynamic MIME detection.
            var attachments = files
                .Select(path => (path, "image/png"))
                .ToArray();

            // Perform the actual upload using the underlying Jira REST command invoker.
            // The API returns a JSON array describing the uploaded attachment(s).
            var response = Invoker.AddAttachments(idOrKey, attachments);

            // Log the result for diagnostic or auditing purposes.
            _logger?.LogInformation(
                message: "Added attachments to issue {IdOrKey}: {Attachments}",
                idOrKey,
                string.Join(", ", files));

            // Return the Jira REST API response.
            return response;
        }

        /// <summary>
        /// Gets the identifier of an allowed value for a specific Jira issue field,
        /// based on a provided display value (either the "name" or "value" of the option).
        /// </summary>
        /// <param name="project">The Jira project key (for example, "BRIFF") used to scope the issue type and field metadata lookup.</param>
        /// <param name="idOrKey">The identifier or key associated with the Jira issue type or option (for example, "Medium" or "10018"), as it appears in Jira's metadata or allowed values.</param>
        /// <param name="path">A JSON path or field key used to locate the relevant field definition in the issue type metadata.</param>
        /// <param name="value">The human-readable allowed value (matching either the "name" or "value" property)whose identifier ("id") should be returned.</param>
        /// <returns>The identifier ("id") of the matching allowed value if found; otherwise an empty string.</returns>
        public string GetAllowedValueId(string project, string idOrKey, string path, string value)
        {
            // Verifies whether the given token matches the provided value by comparing
            // its "name" or "value" properties using a case-insensitive comparison.
            static bool AssertToken(JToken token, string value)
            {
                // Define a case-insensitive string comparison.
                const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

                try
                {
                    // Compare the "name" property to the requested value.
                    var isName = $"{token.SelectToken("name")}".Equals(value, Compare);

                    // Compare the "value" property to the requested value.
                    var isValue = $"{token.SelectToken("value")}".Equals(value, Compare);

                    // Return true if either property matches; otherwise false.
                    return isName || isValue;
                }
                catch
                {
                    // In case of any unexpected JSON shape or nulls, treat this token as non-matching.
                    return false;
                }
            }

            // Extract and cache project metadata if it has not been loaded yet.
            if (!_projectMetaCache.ContainsKey(project))
            {
                _projectMetaCache[project] = JiraCommands
                    .GetCreateMeta(project)
                    .Send(Authentication)
                    .ConvertToJsonObject();
            }

            // Retrieve the field definition JSON for the given issue and path.
            // This typically comes from Jira's issue type fields metadata.
            var field = ExtractFieldDefinition(project, idOrKey, path);

            // Log the resolved field metadata for debugging purposes.
            _logger?.LogDebug(
                message: "Resolved field metadata for path {Path} in issue {IdOrKey}: {Field}",
                path, idOrKey, field);

            // If the field metadata could not be resolved, there are no allowed values to inspect.
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }

            // Log the resolution attempt for debugging purposes.
            _logger?.LogDebug(
                message: "Resolving allowed value ID for field at path {Path} with value {Value}",
                path, value);

            // Parse the field metadata and select the "allowedValues" collection from anywhere in the JSON.
            var allowedValues = JObject
                .Parse(field)
                .SelectToken("..allowedValues");

            // If the field does not define any allowedValues, return empty.
            if (allowedValues == null)
            {
                return string.Empty;
            }

            // Find the first allowed value whose "name" or "value" matches the requested value.
            var allowedValue = allowedValues.FirstOrDefault(i => AssertToken(token: i, value));

            // If no matching allowed value was found, return empty; otherwise return its "id".
            return allowedValue == default
                ? string.Empty
                : $"{allowedValue.SelectToken("id")}";
        }

        /// <summary>
        /// Resolves and returns the Jira custom field identifier (e.g., "customfield_12345")
        /// by searching the project's metadata for a matching custom field schema.
        /// </summary>
        /// <param name="project">The Jira project key whose metadata should be inspected (for example, "BRIFF").</param>
        /// <param name="schema">The schema name used to identify the custom field. This value is compared against the project's field definitions using a case-insensitive match.</param>
        /// <returns>The fully qualified Jira custom field ID (e.g., "customfield_12345") if found; otherwise, an empty string. </returns>
        public string GetCustomField(string project, string schema)
        {
            // If the project key is not provided, nothing can be resolved.
            if (string.IsNullOrEmpty(project))
            {
                return string.Empty;
            }

            // Retrieve or populate the project's metadata cache.
            if (!_projectMetaCache.TryGetValue(project, out JObject projectMeta))
            {
                projectMeta = JiraCommands
                    .GetCreateMeta(project)
                    .Send(Authentication)
                    .ConvertToJsonObject();
                _projectMetaCache[project] = projectMeta;
            }

            // Locate the "custom" field entry that matches the provided schema.
            // This searches recursively and performs a case-insensitive comparison.
            var customNode = projectMeta
                .SelectTokens("..custom")
                .FirstOrDefault(i => $"{i}".Equals(schema, StringComparison.OrdinalIgnoreCase));

            // If no matching custom field definition exists, return empty.
            if (customNode == null)
            {
                return string.Empty;
            }

            // The "custom" node is usually nested.
            // We navigate back to the parent object to extract "customId".
            var customId = customNode.Parent?.Parent?["customId"];

            // Without a customId, a field cannot be constructed.
            if (customId == null)
            {
                return string.Empty;
            }

            // Construct and return the Jira field name in its standard format.
            return $"customfield_{customId}";
        }

        /// <summary>
        /// Retrieves the raw field metadata for a specific field path in the given issue.
        /// </summary>
        /// <param name="idOrKey">The identifier or key associated with the Jira issue type or option (for example, "Medium" or "10018"), as it appears in Jira's metadata or allowed values.</param>
        /// <param name="path">The field path to resolve from the issue type metadata.</param>
        /// <returns>The JSON field definition as a string, or an empty string if not found.</returns>
        public JsonElement GetFieldDefinition(string project, string idOrKey, string path)
        {
            // Delegate lookup to the overload that includes project resolution.
            var definition = ExtractFieldDefinition(project, idOrKey, path);

            // Log the resolved field metadata for debugging.
            _logger?.LogDebug(
                message: "Resolved issue type fields for path {Path} in issue {IdOrKey}: {Fields}",
                path, idOrKey, definition);

            // Return the field metadata.
            return JsonElement.Parse(definition);
        }

        /// <summary>
        /// Retrieves a Jira issue and returns it as a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="idOrKey">The issue ID or key.</param>
        /// <param name="fields">Optional field names to limit the response. If empty, Jira returns all available fields.</param>
        /// <returns>The root JSON element of the returned issue.</returns>
        public JsonElement GetIssue(string idOrKey, params string[] fields) => JiraCommands
            .GetIssue(idOrKey, fields)
            .Send(Invoker)
            .ConvertToJsonDocument()
            .RootElement;

        /// <summary>
        /// Executes a JQL query and returns the matching Jira issues.
        /// </summary>
        /// <param name="jql">A valid JQL expression used to filter issues.</param>
        /// <returns>An enumerable sequence of issue JSON elements.</returns>
        public IEnumerable<JsonElement> GetIssues(string jql)
        {
            // Delegate the query execution to the underlying search helper.
            return FindByQuery(Invoker, jql);
        }

        /// <summary>
        /// Retrieves multiple Jira issues by their IDs or keys.
        /// </summary>
        /// <param name="idsOrKeys">A collection of issue identifiers or keys.</param>
        /// <returns>An enumerable sequence of issue JSON elements.</returns>
        public IEnumerable<JsonElement> GetIssues(params string[] idsOrKeys)
        {
            // Log the set of issue identifiers being requested.
            _logger?.LogDebug(
                message: "Fetching issues for IDs/Keys {IdsOrKeys}: ",
                string.Join(", ", idsOrKeys));

            // Perform the lookup using batched requests.
            return FindByIdsOrKeys(Invoker, _bucketSize, [.. idsOrKeys]);
        }

        /// <summary>
        /// Returns the issue type name for the specified Jira issue.
        /// </summary>
        /// <param name="idOrKey">The issue identifier or key.</param>
        /// <returns>The issue type name, or <c>null</c> if the issue is not found.</returns>
        public string GetIssueType(string idOrKey)
        {
            // Fetch a single issue using a minimal batch size.
            var issue = FindByIdsOrKeys(Invoker, bucketSize: 1, idOrKey).FirstOrDefault();

            // If the issue does not exist, return null.
            if (!issue.ValueKind.Equals(JsonValueKind.Object))
            {
                return null;
            }

            // Navigate to fields.issuetype.name.
            return issue
                .GetProperty("fields")
                .GetProperty("issuetype")
                .GetProperty("name")
                .GetString();
        }

        /// <summary>
        /// Returns the available workflow transitions for the specified issue.
        /// </summary>
        /// <param name="idOrKey">The issue identifier or key.</param>
        /// <returns>A collection of transitions represented as dictionaries.</returns>
        public IEnumerable<IDictionary<string, string>> GetTransitions(string idOrKey)
        {
            // Delegate to the shared transition resolver.
            return GetTransitions(Invoker, idOrKey);
        }

        /// <summary>
        /// Retrieves a Jira user assigned to the specified project or issue, matching
        /// either the display name or the email address.
        /// </summary>
        /// <param name="key">The project key or issue key used to scope assignable users.</param>
        /// <param name="nameOrEmail">The display name or email to match.</param>
        /// <returns>The matching user JSON element, or <c>default</c> if no match is found.</returns>
        public JsonElement GetUser(string key, string nameOrEmail)
        {
            // Compares a user element against a name or email value.
            static bool AssertUsername(JsonElement userData, string nameOrEmail)
            {
                // Define a case-insensitive string comparison.
                const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

                try
                {
                    // Compare the "emailAddress" property to the requested name or email.
                    var isEmail = $"{userData.GetProperty("emailAddress")}".Equals(nameOrEmail, Compare);

                    // Compare the "displayName" property to the requested name or email.
                    var isName = $"{userData.GetProperty("displayName")}".Equals(nameOrEmail, Compare);

                    // Return true if either property matches; otherwise false.
                    return isEmail || isName;
                }
                catch
                {
                    // Any missing field or unexpected JSON shape means "no match".
                    return false;
                }
            }

            // Retrieve users assignable to the given project/issue.
            var users = JiraCommands
                .GetAssignableUsers(key)
                .Send(Invoker)
                .ConvertToJsonDocument()
                .RootElement
                .EnumerateArray();

            // Return the first user whose email or display name matches.
            return users.FirstOrDefault(i => AssertUsername(i, nameOrEmail));
        }

        /// <summary>
        /// Adds a new comment to the specified Jira issue.
        /// </summary>
        /// <param name="idOrKey">The issue identifier or key.</param>
        /// <param name="comment">The comment text to add.</param>
        /// <returns><c>true</c> if Jira returned a 204 (success without content); otherwise <c>false</c>.</returns>
        public bool NewComment(string idOrKey, string comment)
        {
            // Execute the comment command and read the raw response payload.
            var response = JiraCommands
                .AddComment(idOrKey, comment)
                .Send(Invoker)
                .ConvertToJsonDocument()
                .RootElement;

            // Jira uses "code": "204" to indicate successful completion.
            return response.GetProperty("code").GetInt16() == 204;
        }

        /// <summary>
        /// Creates a new Jira issue using the provided payload.
        /// </summary>
        /// <param name="data">The issue creation data sent to Jira.</param>
        /// <returns>The JSON element returned by the create-issue operation.</returns>
        public JsonElement NewIssue(object data)
        {
            // Pass an empty key to indicate creation rather than update.
            return NewOrUpdate(
                Invoker,
                idOrKey: string.Empty,
                data: data,
                comment: string.Empty);
        }

        /// <summary>
        /// Creates a new Jira issue and optionally adds an initial comment.
        /// </summary>
        /// <param name="data">The issue creation data sent to Jira.</param>
        /// <param name="comment">An optional comment added immediately after creation.</param>
        /// <returns>The JSON element returned by the create-issue operation.</returns>
        public JsonElement NewIssue(object data, string comment)
        {
            // Create the issue and provide an initial comment in a single operation.
            return NewOrUpdate(
                Invoker,
                idOrKey: string.Empty,
                data: data,
                comment: comment);
        }

        /// <summary>
        /// Creates a relationship (link) between two Jira issues using the specified link type.
        /// </summary>
        /// <param name="linkType">The Jira link type to create (for example, "Blocks", "Relates", "Cloners").</param>
        /// <param name="inward">The key of the inward issue (the issue that receives the link direction).</param>
        /// <param name="outward">The key of the outward issue (the issue targeted by the link direction).</param>
        /// <returns>A <see cref="JsonElement"/> representing the Jira API response for the newly created issue link.</returns>
        public JsonElement NewIssueLink(string linkType, string inward, string outward)
        {
            // Invoke the Jira REST command to create an issue link.
            // A default comment (_createMessage) is also attached to the link creation.
            var response = JiraCommands
                .NewIssueLink(linkType, inward, outward, comment: _createMessage)
                .Send(Invoker);

            // Parse and return the JSON result as a JsonElement.
            return JsonDocument.Parse(response).RootElement;
        }

        /// <summary>
        /// Creates a link between two Jira issues using the specified link type,
        /// with an optional comment attached to the link creation.
        /// </summary>
        /// <param name="linkType">The Jira link type to create (for example: "Blocks", "Relates", "Cloners").</param>
        /// <param name="inward">The key of the inward issue (the issue receiving the link direction).</param>
        /// <param name="outward">The key of the outward issue (the target of the link direction).</param>
        /// <param name="comment">An optional comment that will be added as part of the issue link creation. If null or empty, Jira will create the link without a comment.</param>
        /// <returns>A <see cref="JsonElement"/> representing the Jira API response describing the newly created link.</returns>
        public JsonElement NewIssueLink(string linkType, string inward, string outward, string comment)
        {
            // Execute the Jira REST command to create the issue link.
            // The comment (if provided) is included in the link creation payload.
            var response = JiraCommands
                .NewIssueLink(linkType, inward, outward, comment)
                .Send(Invoker);

            // Parse the JSON response and return it as a JsonElement.
            return JsonDocument.Parse(response).RootElement;
        }

        /// <summary>
        /// Executes a workflow transition on the specified issue with a resolution
        /// and applies the default creation message as the comment.
        /// </summary>
        /// <param name="idOrKey">The issue identifier or key.</param>
        /// <param name="transition">The transition name or ID.</param>
        /// <param name="resolution">The resolution value to apply.</param>
        /// <returns><c>true</c> if the transition succeeded; otherwise <c>false</c>.</returns>
        public bool NewTransition(string idOrKey, string transition, string resolution)
        {
            // Delegate to the full overload with the default comment.
            return NewTransition(idOrKey, transition, resolution, comment: _createMessage);
        }

        /// <summary>
        /// Executes a workflow transition on the specified Jira issue,
        /// applying the given resolution and optional comment.
        /// </summary>
        /// <param name="idOrKey">The issue identifier or key.</param>
        /// <param name="transition">The target transition name (matching the "to" value).</param>
        /// <param name="resolution">The resolution to apply during the transition.</param>
        /// <param name="comment">A comment added as part of the transition.</param>
        /// <returns><c>true</c> if Jira reports a successful transition (code 204); otherwise <c>false</c>.</returns>
        public bool NewTransition(string idOrKey, string transition, string resolution, string comment)
        {
            // Get all available transitions for this issue.
            var transitions = GetTransitions(Invoker, idOrKey);

            // If no transitions are available, there is nothing to apply.
            if (transitions.Count == 0)
            {
                return false;
            }

            // Find the transition whose "to" value matches the requested target.
            var onTransition = transitions
                .FirstOrDefault(i => i["to"].Equals(transition, StringComparison.OrdinalIgnoreCase));

            if (onTransition == default)
            {
                // Log that no matching transition was found and exit.
                _logger?.LogInformation("");
                return false;
            }

            // Execute the transition with the resolved transition id, resolution, and comment.
            var response = JiraCommands
                .NewTransition(idOrKey, onTransition["id"], resolution, comment)
                .Send(Invoker)
                .ConvertToJsonDocument()
                .RootElement;

            // Jira uses "code": "204" to indicate a successful transition with no content.
            return $"{response.GetProperty("code")}" == "204";
        }

        /// <summary>
        /// Creates a new worklog entry on the specified Jira issue.
        /// </summary>
        /// <param name="id">The issue identifier or key.</param>
        /// <param name="timeSpent">The actual time spent on the work item.</param>
        /// <returns>The worklog identifier returned by Jira.</returns>
        public string NewWorklog(string id, TimeSpan timeSpent)
        {
            // Jira does not accept values below one minute, so enforce a minimum of 60 seconds.
            var timeSpentSeconds = timeSpent.TotalSeconds < 60
                ? 60
                : timeSpent.TotalSeconds;

            // Build the comment describing the actual runtime and applied worklog value.
            var comment =
                "Worklog recorded by G4 MCP Service.\n" +
                $"Actual runtime: {timeSpent.TotalSeconds:N0} seconds\n" +
                $"Applied worklog: {timeSpentSeconds:N0} seconds (minimum unit enforced)";


            // Prepare and send the worklog command.
            var command = JiraCommands.NewWorklog(id, timeSpentSeconds, comment);
            var response = command.Send(Invoker).ConvertToJsonDocument();

            // Return the ID of the newly created worklog.
            return response.RootElement.GetProperty("id").GetString();
        }

        /// <summary>
        /// Sets the assignee of the specified Jira issue to the current authenticated user.
        /// </summary>
        /// <param name="key">The issue key whose assignee will be updated.</param>
        public void SetAssignee(string key)
        {
            // Delegate to the overload that accepts an explicit assignee.
            // Here we use the username from the authentication context.
            SetAssignee(key, Authentication.Username);
        }

        /// <summary>
        /// Sets the assignee of the specified Jira issue using either a display name
        /// or an account ID, depending on which is available for the user.
        /// </summary>
        /// <param name="key">The issue key to update.</param>
        /// <param name="nameOrEmail">A display name or email used to resolve the target Jira user.</param>
        public void SetAssignee(string key, string nameOrEmail)
        {
            // Resolve user information from Jira.
            var userData = GetUser(key, nameOrEmail);

            // Jira Cloud prefers accountId; Jira Server may still use "name".
            // If "accountId" is missing, fall back to the display name.
            var isByName = !userData.TryGetProperty("accountId", out _);

            // Extract either the display name or the accountId.
            var user = isByName
                ? userData.GetProperty("displayName").GetString()
                : userData.GetProperty("accountId").GetString();

            // Log the assignment strategy being used.
            if (isByName)
            {
                // Jira Server-style assignment (legacy): assign by username.
                var data = new
                {
                    Fields = new
                    {
                        Assignee = new
                        {
                            Name = user
                        }
                    }
                };

                // Perform the update using the constructed payload.
                JiraCommands
                    .UpdateIssue(idOrKey: key, data)
                    .Send(Invoker);
            }
            else if (userData.TryGetProperty("accountId", out _))
            {
                // Jira Cloud-style assignment: assign by accountId.
                JiraCommands
                    .SetAssignee(idOrKey: key, $"{user}")
                    .Send(Invoker);
            }
        }

        /// <summary>
        /// Updates an existing Jira issue with the specified data.
        /// </summary>
        /// <param name="idOrKey">The issue identifier or key.</param>
        /// <param name="data">The fields to update.</param>
        public void UpdateIssue(string idOrKey, object data)
        {
            // Forward to the shared create/update handler without adding a comment.
            NewOrUpdate(Invoker, idOrKey, data: data, comment: string.Empty);
        }

        /// <summary>
        /// Updates an existing Jira issue and optionally appends a comment.
        /// </summary>
        /// <param name="idOrKey">The issue identifier or key.</param>
        /// <param name="data">The fields to update.</param>
        /// <param name="comment">A comment added along with the update.</param>
        public void UpdateIssue(string idOrKey, object data, string comment)
        {
            // Forward the update request with the supplied data and comment.
            NewOrUpdate(Invoker, idOrKey, data: data, comment);
        }

        /// <summary>
        /// Removes all attachments from the specified Jira issue.
        /// </summary>
        /// <param name="idOrKey">The issue identifier or key.</param>
        public void RemoveAttachments(string idOrKey)
        {
            // Fetch the issue with only the attachment field populated.
            var issue = JiraCommands
                .GetIssue(idOrKey, fields: "attachment")
                .Send(Invoker)
                .ConvertToJsonDocument();

            // If the issue payload does not contain an "id", treat this as a failed fetch.
            if (!issue.RootElement.TryGetProperty("id", out _))
            {
                _logger?.LogWarning("Get-Issue = false");
                return;
            }

            // Build a sequence of "remove attachment" commands for each attachment on the issue.
            var commands = issue.RootElement
                .GetProperty("fields")
                .GetProperty("attachment")
                .EnumerateArray()
                .Select(i => JiraCommands.RemoveAttachment(id: i.GetProperty("id").GetString()));

            // Configure parallel execution with the resolved bucket size.
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _bucketSize
            };

            // Execute all remove commands in parallel.
            Parallel.ForEach(commands, options, command => command.Send(Invoker));
        }

        // Finds issues by their IDs or keys using batched JQL queries and returns
        // a concurrent collection of issue JSON elements.
        private static ConcurrentBag<JsonElement> FindByIdsOrKeys(
            JiraCommandInvoker invoker,
            int bucketSize,
            params string[] idsOrKeys)
        {
            // Split the keys into fixed-size buckets to build manageable JQL queries.
            var buckets = idsOrKeys.Split(10);
            var jqls = new List<string>();

            // Build JQL queries for each bucket.
            foreach (var bucket in buckets)
            {
                // Build a JQL expression for the current bucket.
                jqls.Add($"key in ({string.Join(",", bucket)})");
            }

            // Shared collection to accumulate issues from all queries.
            var objectCollection = new ConcurrentBag<JsonElement>();

            // Control parallel execution with the provided bucket size.
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = bucketSize
            };

            // Execute each JQL query in parallel and add results to the shared bag.
            Parallel.ForEach(jqls, parallelOptions, jql =>
            {
                var items = FindByQuery(invoker, jql);
                objectCollection.AddRange(items);
            });

            // Return the aggregated collection of issues.
            return objectCollection;
        }

        // TODO: Implement paging support for large result sets.
        // Executes a JQL query and returns the matching issues as JSON elements.
        private static IEnumerable<JsonElement> FindByQuery(
            JiraCommandInvoker invoker,
            string jql)
        {
            // Execute the JQL search request and retrieve the raw issue array.
            var issues = JiraCommands
                .FindIssues(jql)
                .Send(invoker)
                .ConvertToJsonDocument()
                .RootElement
                .GetProperty("issues")
                .EnumerateArray();

            // Return each issue as-is.
            return issues.Select(i => i);
        }

        // Retrieves a field definition from the cached project metadata for the specified
        // issue type and path.
        private static string ExtractFieldDefinition(string project, string idOrKey, string path)
        {
            // Ensure the project exists and has cached metadata.
            if (string.IsNullOrEmpty(project) ||
                !_projectMetaCache.TryGetValue(project, out JObject projectMeta))
            {
                return string.Empty;
            }

            // Locate the issue type token by matching its "name" property.
            var issueTypeToken = projectMeta.SelectToken("$.projects[0].issuetypes")
                .FirstOrDefault(i => $"{i.SelectToken("name")}".Equals(idOrKey, StringComparison.OrdinalIgnoreCase));

            // If the issue type does not exist, exit early.
            if (issueTypeToken == default)
            {
                return string.Empty;
            }

            // When no specific field path is provided, return the entire issue type JSON.
            if (string.IsNullOrEmpty(path))
            {
                return $"{issueTypeToken}";
            }

            // Resolve the field using the provided JSONPath.
            // First() is used here because multiple matches are not expected.
            var issueFieldToken = issueTypeToken.SelectTokens(path).First();

            // Return the raw JSON text, falling back to an empty object if null.
            return issueFieldToken == null
                ? "{}"
                : $"{issueFieldToken}";
        }

        // Retrieves the project metadata used for issue creation, as returned by Jira's
        // createmeta endpoint.
        private static JsonElement GetProjectMeta(JiraCommandInvoker invoker, string project)
        {
            // Request the creation metadata for the specified project.
            var jsonDocument = JiraCommands
                .GetCreateMeta(project)
                .Send(invoker)
                .ConvertToJsonDocument();

            // Extract the "projects" array which typically contains a single entry.
            var projects = jsonDocument
                .RootElement
                .GetProperty("projects")
                .EnumerateArray();

            // Return the first metadata object (if any).
            return projects.FirstOrDefault();
        }

        // Retrieves all workflow transitions available for the specified Jira issue.
        private static List<Dictionary<string, string>> GetTransitions(
            JiraCommandInvoker invoker,
            string idOrKey)
        {
            // Request the issue transitions from Jira.
            var transitions = JiraCommands
                .GetTransitions(idOrKey)
                .Send(invoker)
                .ConvertToJsonDocument()
                .RootElement
                .GetProperty("transitions")
                .EnumerateArray();

            // Convert the raw JSON transitions to simple dictionaries.
            var onTransitions = new List<Dictionary<string, string>>();

            // Iterate over each transition element.
            foreach (var transition in transitions)
            {
                // Extract the "to" status name. Some transitions may omit this field.
                var toName = transition
                    .GetProperty("to")
                    .GetProperty("name")
                    .GetString();

                // Build a dictionary representing the transition.
                var onTransition = new Dictionary<string, string>
                {
                    ["id"] = $"{transition.GetProperty("id")}",
                    ["name"] = $"{transition.GetProperty("name")}",
                    ["to"] = string.IsNullOrEmpty(toName) ? "N/A" : toName
                };

                // Add the transition dictionary to the list.
                onTransitions.Add(onTransition);
            }

            // Return the list of transitions.
            return onTransitions;
        }

        // Creates a new Jira issue or updates an existing one, and optionally adds a comment.
        private static JsonElement NewOrUpdate(
            JiraCommandInvoker invoker,
            string idOrKey,
            object data,
            string comment)
        {
            // Determine whether this is an update (existing key) or a new issue.
            var isUpdate = !string.IsNullOrEmpty(idOrKey);

            // If the input is a JSON string and is valid, parse it into a JsonElement.
            data = data is string && $"{data}".ConfirmJson()
                ? JsonElement.Parse($"{data}")
                : data;

            // Build the appropriate Jira command based on the operation type.
            var command = isUpdate
                ? JiraCommands.UpdateIssue(idOrKey, data)
                : JiraCommands.NewIssue(data);

            // Send the command and capture the root element of the response.
            var response = command
                .Send(invoker)
                .ConvertToJsonDocument()
                .RootElement;

            var isCode = response.TryGetProperty("code", out var codeOut);
            var code = isCode ? codeOut.GetInt16() : 0;

            var isGenericResponse = code != 0 && code < 400;
            var isFail = $"{response.GetProperty("id")}" == "-1";

            // If Jira reports a valid code but an internal failure, return only the key.
            if (isGenericResponse && isFail)
            {
                return JsonElement.Parse(@"{""key"":""" + idOrKey + @"""}");
            }
            // If there is a failure and no valid code, return an empty object.
            else if (!isCode && isFail)
            {
                return JsonElement.Parse("{}");
            }

            // Extract the issue key from the response.
            var key = $"{response.GetProperty("key")}";

            // Add a comment if provided and the key could be resolved.
            if (!string.IsNullOrEmpty(comment))
            {
                JiraCommands
                    .AddComment(idOrKey: key, comment)
                    .Send(invoker);
            }

            // Return the full response to the caller.
            return response;
        }
        #endregion
    }
}
