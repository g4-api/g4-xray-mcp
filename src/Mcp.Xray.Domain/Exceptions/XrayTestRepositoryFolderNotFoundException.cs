using System;

namespace Mcp.Xray.Domain.Exceptions
{
    /// <summary>
    /// Represents an error that occurs when an Xray Test Repository folder
    /// cannot be found for a given path or identifier.
    /// This exception is raised when folder resolution fails against the
    /// Xray Test Repository structure, typically due to an invalid path
    /// or a missing repository node.
    /// </summary>
    public class XrayTestRepositoryFolderNotFoundException(
        string message,
        Exception innerException
    ) : Exception(message, innerException)
    {
        /// <inheritdoc />
        public XrayTestRepositoryFolderNotFoundException()
            : this(message: string.Empty, innerException: default)
        {
        }

        /// <inheritdoc />
        public XrayTestRepositoryFolderNotFoundException(string message)
            : this(message: message, innerException: default)
        {
        }
    }
}
