using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Mcp.Xray.Domain.Models
{
    public class TestCaseModel
    {
        public string Actual { get; set; }

        public string[] Categories { get; set; } = [];

        public ConcurrentDictionary<string, object> Context { get; set; } = [];

        public ConcurrentDictionary<string, string> CustomFields { get; set; } = [];

        public Dictionary<string, object>[] DataSource { get; set; } = [];

        public DateTime End { get; set; }

        public string Identifier { get; set; } = string.Empty;

        public bool Inconclusive { get; set; }
        
        public TimeSpan InvocationTime { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Link { get; set; } = string.Empty;
        
        public string Priority { get; set; } = "0";
        
        public double QualityRank { get; set; }
        
        public string ReasonPhrase { get; set; } = string.Empty;
        
        public TimeSpan RunTime { get; set; }

        public string Scenario { get; set; } = string.Empty;
        
        public TimeSpan SetupTime { get; set; }

        public string Severity { get; set; } = "0";
        
        public DateTime Start { get; set; }

        public virtual TestStepModel[] Steps { get; set; } = [];
        
        public TimeSpan TeardownTime { get; set; }

        public string TestSpecifications { get; set; }

        public string TestRunKey { get; set; } = string.Empty;

        public TestStepModel[] TestSetup { get; set; }

        public TestStepModel[] TestTeardown { get; set; }

        public double Tolerance { get; set; }
        
        public int TotalSteps { get; set; }

        public class TestStepModel
        {
            public string Action { get; set; } = string.Empty;
            
            public bool Actual { get; set; }

            public string[] ExpectedResults { get; set; } = [];
        }
    }
}
