using System;

namespace Mcp.Xray.Domain.Exceptions
{
    /// <summary>
    /// Represents an error that occurs when an Xray test step
    /// cannot be successfully created.
    /// This exception provides a domain-level signal that a test step
    /// creation operation failed after reaching the Xray integration layer.
    /// It is intended to wrap lower-level failures while preserving
    /// meaningful context for callers.
    /// </summary>
    internal class XrayTestStepNotCreatedException(
        string message,
        Exception innerException
    ) : Exception(message, innerException)
    {
        /// <inheritdoc />
        public XrayTestStepNotCreatedException()
            : this(message: string.Empty, innerException: default)
        {
        }

        /// <inheritdoc />
        public XrayTestStepNotCreatedException(string message)
            : this(message: message, innerException: default)
        {
        }
    }
}
