using System;

namespace Mcp.Xray.Domain.Exceptions
{
    /// <summary>
    /// Represents an error that occurs when a Jira issue expected to be created
    /// cannot be successfully created by the system.
    /// This exception is intended to wrap lower-level failures originating
    /// from Jira or Xray interactions while providing a domain-specific
    /// semantic meaning to callers.
    /// </summary>
    public class JiraIssueNotCreatedException(
        string message,
        Exception innerException
    ) : Exception(message, innerException)
    {
        /// <inheritdoc />
        public JiraIssueNotCreatedException()
            : this(message: string.Empty, innerException: default)
        {
        }

        /// <inheritdoc />
        public JiraIssueNotCreatedException(string message)
            : this(message: message, innerException: default)
        {
        }
    }
}
