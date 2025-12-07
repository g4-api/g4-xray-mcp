using System.Collections.Generic;

namespace Mcp.Xray.Domain.Models
{
    public class JiraAuthenticationModel
    {
        public bool AsOsUser { get; set; } = false;
        public string Collection { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = [];
        public string Username { get; set; } = string.Empty;
    }
}
