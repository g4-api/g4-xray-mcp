namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Represents the authentication parameters required for connecting to a Jira instance.
    /// The model stores the credentials, collection URL, and optional project context used by
    /// Jira command invokers and API clients.
    /// </summary>
    public class JiraAuthenticationModel
    {
        /// <summary>
        /// Indicates whether authentication should use the operating system user context.
        /// When enabled, username and password fields may be ignored depending on the environment.
        /// </summary>
        public bool AsOsUser { get; set; } = false;

        /// <summary>
        /// The base collection URL of the Jira server or Jira Cloud instance.
        /// This value forms the root of all outbound HTTP routes.
        /// </summary>
        public string Collection { get; set; } = string.Empty;

        /// <summary>
        /// The API token or password used for authenticating with Jira.
        /// The value is typically consumed when generating a Basic Authorization header.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// The default Jira project associated with the authentication context.
        /// This value may be left empty when project-specific behavior is not required.
        /// </summary>
        public string Project { get; set; } = string.Empty;

        /// <summary>
        /// The username associated with the Jira account. 
        /// Required when creating Basic Authorization credentials for Jira API access.
        /// </summary>
        public string Username { get; set; } = string.Empty;
    }
}
