using System;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Supplies Xray Cloud credentials, endpoints, and retry behavior to the standalone client.
    /// </summary>
    /// <remarks>
    /// The client reads these values but never mutates them. Credentials remain caller-owned, and the
    /// transport uses them only when acquiring or refreshing an Xray bearer token.
    /// </remarks>
    public class XrayClientOptions
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the absolute endpoint used to exchange client credentials for an Xray token.
        /// </summary>
        public Uri AuthenticationEndpoint { get; set; } =
            new("https://xray.cloud.getxray.app/api/v2/authenticate");

        /// <summary>
        /// Gets or sets the client identifier generated in Xray API Keys settings.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client secret generated with the configured Xray client identifier.
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute Xray Cloud GraphQL endpoint that receives every repository command.
        /// </summary>
        public Uri GraphQlEndpoint { get; set; } =
            new("https://xray.cloud.getxray.app/api/v2/graphql");

        /// <summary>
        /// Gets or sets the repeatable-send policy applied to transient authentication and command failures.
        /// </summary>
        public XrayRetryOptions Retry { get; set; } = new();
        #endregion
    }
}
