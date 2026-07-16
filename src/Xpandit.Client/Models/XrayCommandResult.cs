using System.Collections.Generic;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Returns non-fatal Xray warnings for commands that do not produce a new domain entity.
    /// </summary>
    public class XrayCommandResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets non-fatal messages returned after Xray completed the command.
        /// </summary>
        public IReadOnlyCollection<string> Warnings { get; set; } = [];
        #endregion
    }
}
