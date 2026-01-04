namespace Mcp.Xray.Settings.Models
{
    /// <summary>
    /// Represents the configuration settings required for connecting to a Jira instance.
    /// This model holds authentication details and endpoint information used by Jira-related services.
    /// </summary>
    public class JiraOptionsModel
    {
        #region *** Properties   ***
        /// <summary>
        /// The API token used for authenticating requests to Jira. 
        /// This value typically corresponds to a user-scoped token generated in the Atlassian account settings.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// The version of the Jira REST API that should be used when constructing request routes.
        /// </summary>
        public string ApiVersion { get; set; }

        /// <summary>
        /// The base URL of the Jira site or instance. This value usually includes the domain and protocol.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the number of items that each bucket can hold for batch processing operations.
        /// </summary>
        public int BucketSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Jira instance is hosted on Atlassian's cloud platform.
        /// </summary>
        public bool IsCloud { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether custom fields should be resolved during processing.
        /// </summary>
        public bool ResolveCustomFields { get; set; }

        /// <summary>
        /// Gets or sets the retry configuration used to control how transient
        /// failures are handled during operation execution.
        /// </summary>
        public RetryOptionsModel RetryOptions { get; set; }

        /// <summary>
        /// The username associated with the API token, used when forming authentication headers.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the configuration options for connecting to Xray Cloud.
        /// </summary>
        public XrayOptionsModel XrayOptions { get; set; }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Defines configuration options that control retry behavior for operations
        /// that may fail transiently.
        /// </summary>
        public class RetryOptionsModel
        {
            /// <summary>
            /// Gets or sets the delay, in milliseconds, that is applied between
            /// consecutive retry attempts.
            /// </summary>
            public int DelayMilliseconds { get; set; }

            /// <summary>
            /// Gets or sets the maximum number of attempts that will be made
            /// before the operation is considered to have failed.
            /// </summary>
            public int MaxAttempts { get; set; }
        }

        /// <summary>
        /// Represents configuration options for connecting to the Xray Cloud internal API.
        /// </summary>
        public class XrayOptionsModel
        {
            /// <summary>
            /// The base URL of the Xray Cloud internal API.
            /// </summary>
            public string BaseUrl { get; set; }
        }
        #endregion
    }
}
