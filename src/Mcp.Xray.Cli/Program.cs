using Mcp.Xray.Domain.Clients;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;


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

xpand.AddTestsToExecution("10393", "XDP-2");

var c = "";