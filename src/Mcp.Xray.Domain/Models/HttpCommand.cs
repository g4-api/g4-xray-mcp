using System.Collections.Generic;
using System.Net.Http;

namespace Mcp.Xray.Domain.Models
{
    public class HttpCommand
    {
        public string ContentType { get; set; } = "application/json";

        public object Data { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public HttpMethod Method { get; set; }

        public string Route { get; set; }
    }
}
