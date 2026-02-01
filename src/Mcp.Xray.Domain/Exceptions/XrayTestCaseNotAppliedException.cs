using System;

namespace Mcp.Xray.Domain.Exceptions
{
    /// <summary>
    /// Represents an error that occurs when an Xray test case
    /// cannot be applied to a target container.
    /// This exception is thrown when a test case fails to be added
    /// to an Xray Test Plan, Test Set, or Test Execution.
    /// It serves as a domain-level signal indicating that the operation
    /// reached the Xray integration layer but could not be completed.
    /// The exception typically wraps a lower-level failure while
    /// preserving meaningful context for upstream callers.
    /// </summary>
    internal class XrayTestCaseNotAppliedException(
        string message,
        Exception innerException
    ) : Exception(message, innerException)
    {
        /// <inheritdoc />
        public XrayTestCaseNotAppliedException()
            : this(message: string.Empty, innerException: default)
        {
        }

        /// <inheritdoc />
        public XrayTestCaseNotAppliedException(string message)
            : this(message: message, innerException: default)
        {
        }
    }
}
