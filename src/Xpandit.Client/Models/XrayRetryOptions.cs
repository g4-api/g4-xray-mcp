using System;

namespace Xpandit.Client.Models
{
    /// <summary>
    /// Defines the repeatable-send policy used for transient Xray authentication and GraphQL failures.
    /// </summary>
    /// <remarks>
    /// The defaults match the existing Xray repository behavior while keeping retry ownership inside
    /// the standalone client. Callers can shorten the delay for interactive use or tests without
    /// replacing the transport implementation.
    /// </remarks>
    public class XrayRetryOptions
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the delay between attempts when the response does not provide a Retry-After value.
        /// </summary>
        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets or sets the total number of send attempts, including the initial request.
        /// </summary>
        public int MaxAttempts { get; set; } = 3;
        #endregion
    }
}
