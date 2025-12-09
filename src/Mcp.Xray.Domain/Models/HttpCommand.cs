using System.Collections.Generic;
using System.Net.Http;

namespace Mcp.Xray.Domain.Models
{
    /// <summary>
    /// Represents a lightweight HTTP instruction used by the Xray and Jira command layer.
    /// The model describes the target route, HTTP method, headers, payload, and content type
    /// required to build and send an outbound request.
    /// </summary>
    public class HttpCommand
    {
        /// <summary>
        /// The media type of the payload that will be sent with the request.
        /// Defaults to <c>application/json</c> for all JSON-based commands.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// The request body associated with the command. This value may be an anonymous object,
        /// a JSON-serializable structure, or null when the request requires no body.
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// An optional collection of custom headers that should be applied to the outgoing request.
        /// Keys are compared using their native case, and values represent the header content.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The HTTP method used by the command, such as GET, POST, PUT, or DELETE.
        /// </summary>
        public HttpMethod Method { get; set; }

        /// <summary>
        /// The relative route or endpoint that determines where the command will be sent.
        /// This value is appended to the base URL of the target service.
        /// </summary>
        public string Route { get; set; }
    }
}
