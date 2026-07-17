using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Xpandit.Client.Models;

namespace Xpandit.Client.Repositories
{
    /// <summary>
    /// Defines typed asynchronous commands for Xray Cloud Test entities, manual steps, and repository folders.
    /// </summary>
    /// <remarks>
    /// All identifiers accepted by command methods are numeric Jira IDs. Issue keys are converted explicitly
    /// through <see cref="ResolveTestIssueIdAsync"/> so callers can see when an additional network query occurs.
    /// </remarks>
    public interface IXrayCommandsRepository
    {
        #region *** Methods      ***
        /// <summary>
        /// Adds a manual step to an existing Xray Test and returns its stable step identifier.
        /// </summary>
        /// <param name="request">Test identity, optional version, and step content.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The manual step persisted by Xray.</returns>
        Task<XrayTestStepResult> AddTestStepAsync(
            AddTestStepRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets one Test Repository folder and the recursive child data returned by Xray.
        /// </summary>
        /// <param name="request">Project identity and folder path; root returns the full testing tree.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The selected folder, or null when the path does not exist.</returns>
        Task<XrayFolder> GetFoldersAsync(
            GetFoldersRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the manual Test Run identified by its Test and Test Execution issues without caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Jira issue identifiers that form the Xray Test Run identity.</param>
        /// <returns>The Test Run identity, status, and ordered manual step snapshots.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when Xray has no Test Run for the supplied issues.</exception>
        Task<XrayTestRunResult> GetTestRunAsync(
            GetTestRunRequest request);

        /// <summary>
        /// Gets the manual Test Run identified by its Test and Test Execution issues with caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Jira issue identifiers that form the Xray Test Run identity.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The Test Run identity, status, and ordered manual step snapshots.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when Xray has no Test Run for the supplied issues.</exception>
        Task<XrayTestRunResult> GetTestRunAsync(
            GetTestRunRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Moves an existing Test to an existing Test Repository folder.
        /// </summary>
        /// <param name="request">Numeric Test ID and normalized destination path.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>Command warnings; an empty collection represents a clean mutation.</returns>
        Task<XrayCommandResult> MoveTestToFolderAsync(
            MoveTestToFolderRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensures every segment of a Test Repository folder path exists and returns the leaf folder.
        /// </summary>
        /// <param name="request">Project identity and complete path to create.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The existing or newly created leaf folder.</returns>
        Task<XrayFolder> NewFolderAsync(
            NewFolderRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates one Xray Test together with its Jira issue and ordered manual steps.
        /// </summary>
        /// <param name="request">Jira fields, Xray Test type, and initial step definition.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The registered Test identity, confirmed type, persisted steps, and warnings.</returns>
        Task<XrayCreatedTestResult> NewTestAsync(
            NewTestRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a Test Execution and optionally associates Tests and environments during creation.
        /// </summary>
        /// <param name="request">Jira fields, Test IDs, and environment names.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The new Test Execution identity, created environments, and warnings.</returns>
        Task<XrayTestExecutionResult> NewTestExecutionAsync(
            NewTestExecutionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a Test Plan and optionally associates Tests during creation.
        /// </summary>
        /// <param name="request">Jira fields and optional numeric Test IDs.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The new Test Plan identity and warnings.</returns>
        Task<XrayCreatedIssueResult> NewTestPlanAsync(
            NewTestPlanRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a Test Set and optionally associates Tests during creation.
        /// </summary>
        /// <param name="request">Jira fields and optional numeric Test IDs.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The new Test Set identity and warnings.</returns>
        Task<XrayCreatedIssueResult> NewTestSetAsync(
            NewTestSetRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves one Jira issue key to the numeric Xray Test identifier required by GraphQL commands.
        /// </summary>
        /// <param name="issueKey">Human-readable Jira key such as <c>GAR-22</c>.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The numeric Test issue identifier.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when Xray returns no Test for the key.</exception>
        Task<string> ResolveTestIssueIdAsync(
            string issueKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies sparse manual execution values to one Test Run Step without caller cancellation.
        /// </summary>
        /// <param name="request">Opaque Test Run identities, optional iteration rank, and selected outcome values.</param>
        /// <returns>Command warnings; an empty collection represents a clean mutation.</returns>
        Task<XrayCommandResult> UpdateTestRunStepAsync(
            UpdateTestRunStepRequest request);

        /// <summary>
        /// Applies sparse manual execution values to one Test Run Step with caller cancellation.
        /// </summary>
        /// <param name="request">Opaque Test Run identities, optional iteration rank, and selected outcome values.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>Command warnings; an empty collection represents a clean mutation.</returns>
        Task<XrayCommandResult> UpdateTestRunStepAsync(
            UpdateTestRunStepRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Applies a partial update to an existing Xray manual test step.
        /// </summary>
        /// <param name="request">Step identity and replacement values.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>Command warnings; an empty collection represents a clean mutation.</returns>
        Task<XrayCommandResult> UpdateTestStepAsync(
            UpdateTestStepRequest request,
            CancellationToken cancellationToken = default);
        #endregion
    }
}
