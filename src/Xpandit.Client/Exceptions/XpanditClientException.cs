using System;
using System.Collections.Generic;
using System.Net;

using Xpandit.Client.Models;

namespace Xpandit.Client.Exceptions
{
    /// <summary>
    /// Reports an unrecoverable Xray authentication, HTTP, serialization, or GraphQL command failure.
    /// </summary>
    /// <remarks>
    /// The transport throws this exception only after its configured retry and token-refresh lifecycle ends.
    /// Response details are retained for diagnostics, but credentials and bearer tokens are never included.
    /// </remarks>
    public class XpanditClientException : Exception
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes an Xray client failure with optional protocol and GraphQL diagnostic details.
        /// </summary>
        /// <param name="message">Contextual description of the failed client operation.</param>
        /// <param name="statusCode">HTTP status returned by Xray, or the enum default when no response was received.</param>
        /// <param name="responseBody">Response content safe for caller diagnostics, or null when unavailable.</param>
        /// <param name="errors">GraphQL errors returned with a successful HTTP response.</param>
        /// <param name="innerException">Underlying transport or serialization failure, or null when absent.</param>
        public XpanditClientException(
            string message,
            HttpStatusCode statusCode = default,
            string responseBody = null,
            IReadOnlyCollection<XrayGraphQlError> errors = null,
            Exception innerException = null)
            : base(message, innerException)
        {
            Errors = errors ?? [];
            ResponseBody = responseBody;
            StatusCode = statusCode;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the structured GraphQL errors that prevented command completion.
        /// </summary>
        public IReadOnlyCollection<XrayGraphQlError> Errors { get; }

        /// <summary>
        /// Gets the Xray response body retained for diagnostics.
        /// </summary>
        /// <remarks>
        /// Null indicates that the request failed before Xray returned content.
        /// </remarks>
        public string ResponseBody { get; }

        /// <summary>
        /// Gets the HTTP status returned by Xray.
        /// </summary>
        /// <remarks>
        /// The enum default indicates a transport or serialization failure without an HTTP response.
        /// </remarks>
        public HttpStatusCode StatusCode { get; }
        #endregion
    }
}
