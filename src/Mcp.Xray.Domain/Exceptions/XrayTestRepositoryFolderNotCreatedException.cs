using System;

namespace Mcp.Xray.Domain.Exceptions
{
    /// <summary>
    /// Represents an error that occurs when an Xray Test Repository folder
    /// cannot be successfully created.
    /// This exception is raised when the internal Xray repository folder
    /// creation operation fails or returns an invalid response. It provides
    /// a domain-specific signal that folder creation did not complete as expected.
    /// </summary>
    public class XrayTestRepositoryFolderNotCreatedException(
        string message,
        Exception innerException
    ) : Exception(message, innerException)
    {
        /// <inheritdoc />
        public XrayTestRepositoryFolderNotCreatedException()
            : this(message: string.Empty, innerException: default)
        {
        }

        /// <inheritdoc />
        public XrayTestRepositoryFolderNotCreatedException(string message)
            : this(message: message, innerException: default)
        {
        }
    }
}
