using Mcp.Xray.Domain.Models;

using System;

namespace Mcp.Xray.Domain.Repositories
{
    public class XrayRavenRepository : IXrayRepository
    {
        public object AddTestsToFolder(string idOrKey, string path, string jql)
        {
            throw new NotImplementedException();
        }

        public object NewTest(string project, TestCaseModel testCase)
        {
            throw new NotImplementedException();
        }

        public object NewTestRepositoryFolder(string idOrKey, string name, string parentId)
        {
            throw new NotImplementedException();
        }

        public string ResolveFolderPath(string idOrKey, string path)
        {
            throw new NotImplementedException();
        }

        public object UpdateTest(string key, TestCaseModel testCase)
        {
            throw new NotImplementedException();
        }

        public object UpdateTest(string project, string key, TestCaseModel testCase)
        {
            throw new NotImplementedException();
        }
    }
}
