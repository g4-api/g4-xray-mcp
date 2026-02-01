using System;
using System.Collections.Generic;

namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Represents a complete test case definition and execution record, including its identity,
    /// classification, configuration context, execution timing, outcome state, associated steps,
    /// and metadata required for reporting, traceability, and external system integration.
    /// </summary>
    public class TestCaseModel : NewIssueModelBase
    {
        #region *** Constants    ***
        // Comparer for case-insensitive dictionary keys in ConcurrentDictionaries.
        private static readonly StringComparer _comparer = StringComparer.OrdinalIgnoreCase;
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the actual execution outcome or observed behavior of the test case.
        /// </summary>
        public string Actual { get; set; }

        /// <summary>
        /// Gets or sets the categorical labels associated with this test case for classification and filtering.
        /// </summary>
        public string[] Categories { get; set; } = [];

        /// <summary>
        /// Gets or sets the structured input data used by the test case,
        /// commonly representing parameterized or data-driven execution sources.
        /// </summary>
        public Dictionary<string, object>[] DataSource { get; set; } = [];

        /// <summary>
        /// Gets or sets the timestamp at which the test execution ended.
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// Gets or sets the internal or external identifier associated with this test case.
        /// </summary>
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the test outcome is inconclusive.
        /// </summary>
        public bool Inconclusive { get; set; }

        /// <summary>
        /// Gets or sets the time spent invoking the test execution infrastructure.
        /// </summary>
        public TimeSpan InvocationTime { get; set; }

        /// <summary>
        /// Gets or sets the external system key associated with this test case,
        /// such as a Jira or Xray identifier.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the direct link to the external system representation of this test case.
        /// </summary>
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the priority level assigned to this test case.
        /// </summary>
        public string Priority { get; set; } = "0";

        /// <summary>
        /// Gets or sets a computed quality ranking that reflects test reliability or importance.
        /// </summary>
        public double QualityRank { get; set; }

        /// <summary>
        /// Gets or sets the human-readable explanation describing the test outcome.
        /// </summary>
        public string ReasonPhrase { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total runtime of the test case execution.
        /// </summary>
        public TimeSpan RunTime { get; set; }

        /// <summary>
        /// Gets or sets the short human-readable scenario or title of the test case.
        /// </summary>
        public string Scenario { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the time spent performing test setup operations.
        /// </summary>
        public TimeSpan SetupTime { get; set; }

        /// <summary>
        /// Gets or sets the severity level assigned to this test case.
        /// </summary>
        public string Severity { get; set; } = "0";

        /// <summary>
        /// Gets or sets the timestamp at which the test execution started.
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// Gets or sets the ordered list of execution steps that define the test flow.
        /// </summary>
        public virtual TestStepModel[] Steps { get; set; } = [];

        /// <summary>
        /// Gets or sets the time spent performing teardown operations after execution.
        /// </summary>
        public TimeSpan TeardownTime { get; set; }

        /// <summary>
        /// Gets or sets the formal test specifications or detailed description of the test intent.
        /// </summary>
        public string TestSpecifications { get; set; }

        /// <summary>
        /// Gets or sets the execution key that uniquely identifies a specific test run.
        /// </summary>
        public string TestRunKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ordered list of setup steps executed before the main test flow.
        /// </summary>
        public TestStepModel[] TestSetup { get; set; }

        /// <summary>
        /// Gets or sets the ordered list of teardown steps executed after the main test flow.
        /// </summary>
        public TestStepModel[] TestTeardown { get; set; }

        /// <summary>
        /// Gets or sets the acceptable deviation threshold for determining test success.
        /// </summary>
        public double Tolerance { get; set; }

        /// <summary>
        /// Gets or sets the total number of steps associated with this test case.
        /// </summary>
        public int TotalSteps { get; set; }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents a single step within a test case, describing the action to be performed,
        /// the conditions that define a successful outcome, and the actual execution result
        /// when the step is run as part of a test flow.
        /// </summary>
        public class TestStepModel
        {
            /// <summary>
            /// Gets or sets the action to be performed for this test step.
            /// This value represents the primary instruction shown during execution.
            /// </summary>
            public string Action { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets a value indicating whether the step
            /// has been executed successfully.
            /// </summary>
            public bool Actual { get; set; }

            /// <summary>
            /// Gets or sets the expected results that define
            /// the success criteria for this step.
            /// </summary>
            public string[] ExpectedResults { get; set; } = [];
        }
        #endregion
    }
}
