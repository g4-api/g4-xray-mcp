namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Represents the data required to create a new Xray Test Plan.
    /// A Test Plan defines a collection of test cases selected
    /// either explicitly or dynamically using a JQL expression.
    /// This model extends <see cref="NewIssueModelBase"/> with
    /// Test Plan–specific configuration.
    /// </summary>
    public class NewTestPlanModel : NewIssueModelBase
    {
        /// <summary>
        /// Gets or sets the JQL expression used to dynamically
        /// select test cases for the Test Plan.
        /// When specified, the JQL is evaluated by Xray to determine
        /// which test cases are included in the Test Plan.
        /// If omitted, test cases must be added explicitly.
        /// </summary>
        public string Jql { get; set; }
    }
}
