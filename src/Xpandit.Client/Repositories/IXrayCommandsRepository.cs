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
        /// Adds Test Executions to an existing Test Plan without caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Test Plan and Test Execution issue identifiers.</param>
        /// <returns>The Test Execution identifiers accepted by Xray and non-fatal warnings.</returns>
        Task<XrayTestExecutionsAssociationResult> AddTestExecutionsToTestPlanAsync(
            AddTestExecutionsToTestPlanRequest request);

        /// <summary>
        /// Adds Test Executions to an existing Test Plan with caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Test Plan and Test Execution issue identifiers.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The Test Execution identifiers accepted by Xray and non-fatal warnings.</returns>
        Task<XrayTestExecutionsAssociationResult> AddTestExecutionsToTestPlanAsync(
            AddTestExecutionsToTestPlanRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Adds a manual step to an existing Xray Test without caller cancellation.
        /// </summary>
        /// <param name="request">Test identity, optional version, and step content.</param>
        /// <returns>The manual step persisted by Xray.</returns>
        Task<XrayTestStepResult> AddTestStepAsync(
            AddTestStepRequest request);

        /// <summary>
        /// Adds a manual step to an existing Xray Test with caller cancellation.
        /// </summary>
        /// <param name="request">Test identity, optional version, and step content.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The manual step persisted by Xray.</returns>
        Task<XrayTestStepResult> AddTestStepAsync(
            AddTestStepRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Adds Tests to an existing Test Execution without caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Test Execution and Test issue identifiers.</param>
        /// <returns>The Test identifiers accepted by Xray and non-fatal warnings.</returns>
        Task<XrayTestsAssociationResult> AddTestsToTestExecutionAsync(
            AddTestsToTestExecutionRequest request);

        /// <summary>
        /// Adds Tests to an existing Test Execution with caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Test Execution and Test issue identifiers.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The Test identifiers accepted by Xray and non-fatal warnings.</returns>
        Task<XrayTestsAssociationResult> AddTestsToTestExecutionAsync(
            AddTestsToTestExecutionRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Adds Tests to an existing Test Plan without caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Test Plan and Test issue identifiers.</param>
        /// <returns>The Test identifiers accepted by Xray and non-fatal warnings.</returns>
        Task<XrayTestsAssociationResult> AddTestsToTestPlanAsync(
            AddTestsToTestPlanRequest request);

        /// <summary>
        /// Adds Tests to an existing Test Plan with caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Test Plan and Test issue identifiers.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The Test identifiers accepted by Xray and non-fatal warnings.</returns>
        Task<XrayTestsAssociationResult> AddTestsToTestPlanAsync(
            AddTestsToTestPlanRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets one Test Repository folder and its recursive child data without caller cancellation.
        /// </summary>
        /// <param name="request">Project identity and folder path; root returns the full testing tree.</param>
        /// <returns>The selected folder, or null when the path does not exist.</returns>
        Task<XrayFolder> GetFoldersAsync(
            GetFoldersRequest request);

        /// <summary>
        /// Gets one Test Repository folder and its recursive child data with caller cancellation.
        /// </summary>
        /// <param name="request">Project identity and folder path; root returns the full testing tree.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The selected folder, or null when the path does not exist.</returns>
        Task<XrayFolder> GetFoldersAsync(
            GetFoldersRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets one Xray Test definition by its numeric Jira issue identifier without caller cancellation.
        /// </summary>
        /// <param name="issueId">Numeric Jira issue identifier of the Xray Test.</param>
        /// <returns>The Test identity, type, definition, Jira key, and ordered manual steps.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when Xray returns no Test for the supplied issue.</exception>
        Task<XrayTestResult> GetTestAsync(
            string issueId);

        /// <summary>
        /// Gets one Xray Test definition by its numeric Jira issue identifier with caller cancellation.
        /// </summary>
        /// <param name="issueId">Numeric Jira issue identifier of the Xray Test.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The Test identity, type, definition, Jira key, and ordered manual steps.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when Xray returns no Test for the supplied issue.</exception>
        Task<XrayTestResult> GetTestAsync(
            string issueId,
            CancellationToken cancellationToken);

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
        /// Moves an existing Test to an existing Test Repository folder without caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Test ID and normalized destination path.</param>
        /// <returns>Command warnings; an empty collection represents a clean mutation.</returns>
        Task<XrayCommandResult> MoveTestToFolderAsync(
            MoveTestToFolderRequest request);

        /// <summary>
        /// Moves an existing Test to an existing Test Repository folder with caller cancellation.
        /// </summary>
        /// <param name="request">Numeric Test ID and normalized destination path.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>Command warnings; an empty collection represents a clean mutation.</returns>
        Task<XrayCommandResult> MoveTestToFolderAsync(
            MoveTestToFolderRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Ensures every segment of a Test Repository folder path exists without caller cancellation.
        /// </summary>
        /// <param name="request">Project identity and complete path to create.</param>
        /// <returns>The existing or newly created leaf folder.</returns>
        Task<XrayFolder> NewFolderAsync(
            NewFolderRequest request);

        /// <summary>
        /// Ensures every segment of a Test Repository folder path exists with caller cancellation.
        /// </summary>
        /// <param name="request">Project identity and complete path to create.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The existing or newly created leaf folder.</returns>
        Task<XrayFolder> NewFolderAsync(
            NewFolderRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Creates one Xray Test together with its Jira issue and ordered manual steps without caller cancellation.
        /// </summary>
        /// <param name="request">Jira fields, Xray Test type, and initial step definition.</param>
        /// <returns>The registered Test identity, confirmed type, persisted steps, and warnings.</returns>
        Task<XrayCreatedTestResult> NewTestAsync(
            NewTestRequest request);

        /// <summary>
        /// Creates one Xray Test together with its Jira issue and ordered manual steps with caller cancellation.
        /// </summary>
        /// <param name="request">Jira fields, Xray Test type, and initial step definition.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The registered Test identity, confirmed type, persisted steps, and warnings.</returns>
        Task<XrayCreatedTestResult> NewTestAsync(
            NewTestRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Creates a Test Execution and optionally associates Tests and environments without caller cancellation.
        /// </summary>
        /// <param name="request">Jira fields, Test IDs, and environment names.</param>
        /// <returns>The new Test Execution identity, created environments, and warnings.</returns>
        Task<XrayTestExecutionResult> NewTestExecutionAsync(
            NewTestExecutionRequest request);

        /// <summary>
        /// Creates a Test Execution and optionally associates Tests and environments with caller cancellation.
        /// </summary>
        /// <param name="request">Jira fields, Test IDs, and environment names.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The new Test Execution identity, created environments, and warnings.</returns>
        Task<XrayTestExecutionResult> NewTestExecutionAsync(
            NewTestExecutionRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Creates a Test Plan and optionally associates Tests without caller cancellation.
        /// </summary>
        /// <param name="request">Jira fields and optional numeric Test IDs.</param>
        /// <returns>The new Test Plan identity and warnings.</returns>
        Task<XrayCreatedIssueResult> NewTestPlanAsync(
            NewTestPlanRequest request);

        /// <summary>
        /// Creates a Test Plan and optionally associates Tests with caller cancellation.
        /// </summary>
        /// <param name="request">Jira fields and optional numeric Test IDs.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The new Test Plan identity and warnings.</returns>
        Task<XrayCreatedIssueResult> NewTestPlanAsync(
            NewTestPlanRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Creates a Test Set and optionally associates Tests without caller cancellation.
        /// </summary>
        /// <param name="request">Jira fields and optional numeric Test IDs.</param>
        /// <returns>The new Test Set identity and warnings.</returns>
        Task<XrayCreatedIssueResult> NewTestSetAsync(
            NewTestSetRequest request);

        /// <summary>
        /// Creates a Test Set and optionally associates Tests with caller cancellation.
        /// </summary>
        /// <param name="request">Jira fields and optional numeric Test IDs.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The new Test Set identity and warnings.</returns>
        Task<XrayCreatedIssueResult> NewTestSetAsync(
            NewTestSetRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Resolves one Jira issue key to its numeric Xray Test identifier without caller cancellation.
        /// </summary>
        /// <param name="issueKey">Human-readable Jira key such as <c>GAR-22</c>.</param>
        /// <returns>The numeric Test issue identifier.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when Xray returns no Test for the key.</exception>
        Task<string> ResolveTestIssueIdAsync(
            string issueKey);

        /// <summary>
        /// Resolves one Jira issue key to its numeric Xray Test identifier with caller cancellation.
        /// </summary>
        /// <param name="issueKey">Human-readable Jira key such as <c>GAR-22</c>.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The numeric Test issue identifier.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when Xray returns no Test for the key.</exception>
        Task<string> ResolveTestIssueIdAsync(
            string issueKey,
            CancellationToken cancellationToken);

        /// <summary>
        /// Updates one Test Run status without caller cancellation.
        /// </summary>
        /// <param name="request">Opaque Test Run identity and Xray status name or identifier.</param>
        /// <returns>The status value confirmed by Xray.</returns>
        Task<string> UpdateTestRunStatusAsync(
            UpdateTestRunStatusRequest request);

        /// <summary>
        /// Updates one Test Run status with caller cancellation.
        /// </summary>
        /// <param name="request">Opaque Test Run identity and Xray status name or identifier.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>The status value confirmed by Xray.</returns>
        Task<string> UpdateTestRunStatusAsync(
            UpdateTestRunStatusRequest request,
            CancellationToken cancellationToken);

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
        /// Applies a partial update to an existing Xray manual test step without caller cancellation.
        /// </summary>
        /// <param name="request">Step identity and replacement values.</param>
        /// <returns>Command warnings; an empty collection represents a clean mutation.</returns>
        Task<XrayCommandResult> UpdateTestStepAsync(
            UpdateTestStepRequest request);

        /// <summary>
        /// Applies a partial update to an existing Xray manual test step with caller cancellation.
        /// </summary>
        /// <param name="request">Step identity and replacement values.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, retry delay, or HTTP work.</param>
        /// <returns>Command warnings; an empty collection represents a clean mutation.</returns>
        Task<XrayCommandResult> UpdateTestStepAsync(
            UpdateTestStepRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Waits for Xray to expose a newly associated Test Run without caller cancellation.
        /// </summary>
        /// <param name="request">Composite Test Run identity and bounded logical polling settings.</param>
        /// <returns>The registered Test Run identity, status, and ordered manual step snapshots.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the Test Run remains absent after all attempts.</exception>
        Task<XrayTestRunResult> WaitForTestRunAsync(
            WaitForTestRunRequest request);

        /// <summary>
        /// Waits for Xray to expose a newly associated Test Run with caller cancellation.
        /// </summary>
        /// <param name="request">Composite Test Run identity and bounded logical polling settings.</param>
        /// <param name="cancellationToken">Token that stops pending authentication, polling delay, or HTTP work.</param>
        /// <returns>The registered Test Run identity, status, and ordered manual step snapshots.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the Test Run remains absent after all attempts.</exception>
        Task<XrayTestRunResult> WaitForTestRunAsync(
            WaitForTestRunRequest request,
            CancellationToken cancellationToken);
        #endregion
    }
}
