using Mcp.Xray.Domain.Clients;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;


/*
 * TOOLS:
 * Create test plan (empty, jql)
 * Add tests to test plan (ids, jql)
 * Add sets to test plan (ids, jql)
 * Add executions to test plan (ids, jql)
 * remove tests from test plan (ids, jql)
 * 
 * create test execution (empty, jql)
 * create test execution from test plan
 * create test execution from test set
 * add tests to test execution (ids, jql)
 * add sets to test execution (ids, jql)
 * start test execution
 * update test execution status (pass, fail, etc)
 * update step status in test execution
 * add defect to test execution
 * 
 * create test set (empty, jql)
 * add tests to test set (ids, jql)
 * 
 * create test case ()
 * add steps to test case
 * add preconditions to test case
 * remove steps from test case
 * update test steps in test case
 * 
 * create bug from test case
 * update bug from test case
 */

// Setup Jira client
var auth = new JiraAuthenticationModel
{
    Username = AppSettings.JiraOptions.Username,
    Password = AppSettings.JiraOptions.ApiKey,
    Collection = AppSettings.JiraOptions.BaseUrl,
};
var jiraClient = new JiraClient(auth);

//var attachments = jiraClient.AddAttachments("BRIEF-6", "C:\\temp\\1.jpg");
//var allowed = jiraClient.GetAllowedValueId("BRIEF", "Task", "..priority", "Medium");
//var customField = jiraClient.GetCustomField("BRIEF", "com.atlassian.jira.plugin.system.customfieldtypes:atlassian-team");
//var fieldDefinition = jiraClient.GetFieldDefinition("BRIEF", "Task", "..priority");
//var issue = jiraClient.GetIssue("BRIEF-6");
//var issues = jiraClient.GetIssues(jql: "issuekey ~ \"BRIEF*\"");
//var issuesCollection = jiraClient.GetIssues("BRIEF-6", "BRIEF-7");
//var issueType = jiraClient.GetIssueType("BRIEF-6");
//var transitions = jiraClient.GetTransitions("BRIEF-6");
//var user = jiraClient.GetUser("BRIEF-6", "gravity.api@outlook.com");
//var comment = jiraClient.NewComment("BRIEF-6", "This is a test comment from the API.");
//var newIssue = jiraClient.NewIssue(data: new
//{
//    Fields = new
//    {
//        Project = new { Key = "BRIEF" },
//        Issuetype = new { Name = "Task" },
//        Summary = "Minimal issue"
//    }
//});

//var newIssueWithComment = jiraClient.NewIssue(data: new
//{
//    Fields = new
//    {
//        Project = new { Key = "BRIEF" },
//        Issuetype = new { Name = "Task" },
//        Summary = "Minimal issue"
//    }
//}, "Some Comment on This Issue");

//var issueLink = jiraClient.NewIssueLink("Blocks", inward: "BRIEF-6", outward: "BRIEF-13");
//var issueLinkWithComment = jiraClient.NewIssueLink("Blocks", inward: "BRIEF-7", outward: "BRIEF-13", "Some Comment");


var xpand = new XpandClient(auth); //10393

//xpand.AddTestsToExecution("GXP-2", "GXP-1");

var issue = jiraClient.GetIssue("DTP-1");

var a = xpand.NewTestStep(("10004", "DTP-1"), "some action to perform", "some result to assert", 1);
var b = $"{a}";
xpand.RemoveTestStep(("10004", "DTP-1"), "cdfc459a-dd08-4d98-8b58-59676e0a0c47");

var c = "";