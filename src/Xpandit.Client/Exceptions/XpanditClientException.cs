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
        /// Initializes an Xray client failure with a contextual message.
        /// </summary>
        /// <param name="message">Contextual description of the failed client operation.</param>
        public XpanditClientException(string message)
            : this(
                  message,
                  statusCode: default,
                  responseBody: null,
                  errors: null,
                  innerException: null)
        { }

        /// <summary>
        /// Initializes an Xray client failure with a contextual message and underlying exception.
        /// </summary>
        /// <param name="message">Contextual description of the failed client operation.</param>
        /// <param name="innerException">Underlying transport or serialization failure.</param>
        public XpanditClientException(
            string message,
            Exception innerException)
            : this(
                  message,
                  statusCode: default,
                  responseBody: null,
                  errors: null,
                  innerException)
        { }

        /// <summary>
        /// Initializes an Xray client failure with HTTP diagnostics.
        /// </summary>
        /// <param name="message">Contextual description of the failed client operation.</param>
        /// <param name="statusCode">HTTP status returned by Xray, or the enum default when no response was received.</param>
        /// <param name="responseBody">Response content safe for caller diagnostics.</param>
        public XpanditClientException(
            string message,
            HttpStatusCode statusCode,
            string responseBody)
            : this(
                  message,
                  statusCode,
                  responseBody,
                  errors: null,
                  innerException: null)
        { }

        /// <summary>
        /// Initializes an Xray client failure with HTTP diagnostics and an underlying exception.
        /// </summary>
        /// <param name="message">Contextual description of the failed client operation.</param>
        /// <param name="statusCode">HTTP status returned by Xray, or the enum default when no response was received.</param>
        /// <param name="responseBody">Response content safe for caller diagnostics.</param>
        /// <param name="innerException">Underlying transport or serialization failure.</param>
        public XpanditClientException(
            string message,
            HttpStatusCode statusCode,
            string responseBody,
            Exception innerException)
            : this(
                  message,
                  statusCode,
                  responseBody,
                  errors: null,
                  innerException)
        { }

        /// <summary>
        /// Initializes an Xray client failure with HTTP and GraphQL diagnostics.
        /// </summary>
        /// <param name="message">Contextual description of the failed client operation.</param>
        /// <param name="statusCode">HTTP status returned by Xray, or the enum default when no response was received.</param>
        /// <param name="responseBody">Response content safe for caller diagnostics.</param>
        /// <param name="errors">GraphQL errors returned with a successful HTTP response.</param>
        public XpanditClientException(
            string message,
            HttpStatusCode statusCode,
            string responseBody,
            IReadOnlyCollection<XrayGraphQlError> errors)
            : this(
                  message,
                  statusCode,
                  responseBody,
                  errors,
                  innerException: null)
        { }

        /// <summary>
        /// Initializes an Xray client failure with complete protocol and GraphQL diagnostic details.
        /// </summary>
        /// <param name="message">Contextual description of the failed client operation.</param>
        /// <param name="statusCode">HTTP status returned by Xray, or the enum default when no response was received.</param>
        /// <param name="responseBody">Response content safe for caller diagnostics, or null when unavailable.</param>
        /// <param name="errors">GraphQL errors returned with a successful HTTP response.</param>
        /// <param name="innerException">Underlying transport or serialization failure, or null when absent.</param>
        public XpanditClientException(
            string message,
            HttpStatusCode statusCode,
            string responseBody,
            IReadOnlyCollection<XrayGraphQlError> errors,
            Exception innerException)
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
