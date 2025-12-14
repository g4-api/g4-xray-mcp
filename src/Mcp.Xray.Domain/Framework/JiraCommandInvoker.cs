using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mcp.Xray.Domain.Framework
{
    /// <summary>
    /// Provides the internal execution layer for Jira and Xray HTTP commands. The invoker
    /// builds request messages, applies authentication, resolves Xray routing when required,
    /// and dispatches the command using the correct HTTP verb handler.
    /// </summary>
    /// <param name="authentication">The authentication model that defines the Jira credentials and project context.</param>
    public class JiraCommandInvoker(JiraAuthenticationModel authentication)
    {
        private static long _lastUsed;
        private static string _lastToken;

        #region *** Fields       ***
        // The Jira REST API version used for constructing default API routes.
        private static readonly string _apiVersion = AppSettings.JiraOptions.ApiVersion;

        // The Jira authentication context that supplies credentials and project context
        // for all outgoing HTTP requests.
        private readonly JiraAuthenticationModel _authentication = authentication;

        // The configured bucket size used for controlling batch and parallel execution behavior.
        private static readonly int _bucketSize = AppSettings.JiraOptions.BucketSize;

        // The root Jira base URL used for constructing all REST API endpoints.
        private static readonly string _jiraBaseAddress = AppSettings.JiraOptions.BaseUrl;

        // The JSON serializer options applied when serializing Jira and Xray HTTP payloads.
        private static readonly JsonSerializerOptions _jsonOptions = AppSettings.JsonOptions;

        // The legacy Xray Cloud Xpand-It endpoint used for JWT-secured operations.
        // This value typically points to <c>https://xray.cloud.xpand-it.com</c>.
        private static readonly string _xpandBaseAddress = AppSettings.JiraOptions.XrayCloudOptions.BaseUrl;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Uploads one or more attachments to a Jira issue. Each file is added to a multipart
        /// HTTP request and sent to the Jira attachments endpoint. The method returns the
        /// parsed JSON response produced by Jira after processing the upload.
        /// </summary>
        /// <param name="idOrKey">The issue identifier or key that will receive the uploaded attachments.</param>
        /// <param name="files">A collection of file descriptors where each entry contains the full file path and the associated content type.</param>
        /// <returns>A <see cref="JsonElement"/> representing the JSON response returned by Jira.</returns>
        public JsonElement AddAttachments(string idOrKey, params (string Path, string ContentType)[] files)
        {
            // Builds the Jira REST endpoint for uploading attachments.
            var urlPath = $"{_jiraBaseAddress}/rest/api/{_apiVersion}/issue/{idOrKey}/attachments";

            // Prepares the HTTP request message for the upload operation.
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, urlPath);
            requestMessage.Headers.ExpectContinue = false;

            // Applies Jira authentication headers.
            requestMessage.Headers.Authorization = _authentication.NewAuthenticationHeader();

            // Required to bypass the default attachment validation in Jira.
            requestMessage.Headers.Add("X-Atlassian-Token", "no-check");

            // Creates the multipart payload that will contain the file streams.
            var multiPartContent = new MultipartFormDataContent($"----{Guid.NewGuid()}");

            // Iterates over all requested files and adds them to the multipart form.
            foreach (var (Path, ContentType) in files)
            {
                var file = new FileInfo(Path);
                var buffer = File.ReadAllBytes(file.FullName);
                var byteArrayContent = new ByteArrayContent(buffer);

                // Marks the content type provided by the caller.
                byteArrayContent.Headers.Add("Content-Type", ContentType);

                // Maintains Jira compliance for file uploads.
                byteArrayContent.Headers.Add("X-Atlassian-Token", "no-check");

                // Adds the file to the multipart form under the name "file".
                multiPartContent.Add(byteArrayContent, "file", file.Name);
            }

            // Assigns the multipart content to the request message.
            requestMessage.Content = multiPartContent;

            // Sends the request and returns the parsed JSON body.
            return requestMessage
                .Send()
                .ConvertToJsonDocument()
                .RootElement;
        }

        /// <summary>
        /// Sends a single HTTP command using the current invoker context. The method delegates
        /// to the static overload so the invoker instance can be passed explicitly.
        /// </summary>
        /// <param name="command">The HTTP command containing the route, method, headers, and payload.</param>
        /// <returns>The raw string response returned by the remote service.</returns>
        public string SendCommand(HttpCommand command)
        {
            // Delegates to the static execution path, using this instance as the invoker.
            return SendCommand(this, command);
        }

        /// <summary>
        /// Sends a collection of HTTP commands concurrently and returns the parsed JSON responses.
        /// Each command is executed using the current invoker context, and the level of concurrency
        /// is limited by the configured bucket size.
        /// </summary>
        /// <param name="commands">The set of HTTP commands to execute. Each command represents a single outgoing request.</param>
        /// <returns>A collection of <see cref="JsonElement"/> instances, each representing the JSON body returned by one of the executed commands.</returns>
        public IEnumerable<JsonElement> SendCommands(params HttpCommand[] commands)
        {
            // Stores each parsed response as it arrives from the parallel execution loop.
            var results = new ConcurrentBag<JsonElement>();

            // Controls how many commands may run in parallel.
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _bucketSize
            };

            // Executes the commands concurrently using the configured parallelism.
            Parallel.ForEach(commands, options, command =>
            {
                // Sends the command and parses the response into a JsonElement.
                var result = SendCommand(this, command)
                    .ConvertToJsonDocument()
                    .RootElement;

                // Adds the parsed response to the results collection.
                results.Add(result);
            });

            // Returns all collected results to the caller.
            return results;
        }

        // Sends the specified HTTP command using the internal <see cref="JiraCommandInvoker"/> instance.
        // The method resolves the appropriate handler based on the HTTP method description attribute
        // and invokes it. When no matching handler is found, a synthetic 404 response is returned.
        private static string SendCommand(JiraCommandInvoker instance, HttpCommand command)
        {
            // Compares method descriptions using a case-insensitive comparison.
            const StringComparison Comapre = StringComparison.OrdinalIgnoreCase;

            // Retrieves all non-public instance methods that are decorated with DescriptionAttribute.
            // These methods represent the available HTTP handlers (for example GET, POST, PUT, DELETE).
            var methods = typeof(MethodFactory)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(i => i.GetCustomAttribute<DescriptionAttribute>() != null);

            // Selects the first method whose DescriptionAttribute value matches the command HTTP method.
            var method = methods
                .FirstOrDefault(i => i
                    .GetCustomAttribute<DescriptionAttribute>()
                    .Description
                    .Equals($"{command.Method}", Comapre));

            // When a handler is found, it is invoked with the HttpCommand instance and the raw string
            // response from the handler is returned to the caller.
            if (method != default)
            {
                return (string)method.Invoke(instance, [instance._authentication, command]);
            }

            // When no handler matches the requested HTTP method, a synthetic 404 response is created
            // so that the caller receives a consistent error payload.
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                ReasonPhrase = string.Empty,
                Content = new StringContent(string.Empty)
            };

            // Converts the response message to a generic response format.
            return response.NewGenericResponse();
        }
        
        private static void TestToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            //lock(_lastToken)
            //{
            //    if (_lastToken == token && (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _lastUsed) < 60)
            //    {
            //        return;
            //    }
            //    var jwtToken = handler.ReadJwtToken(token);
            //    var exp = jwtToken.Claims.First(i => i.Type == "exp").Value;
            //    var expSeconds = long.Parse(exp);
            //    var expDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            //    Console.WriteLine($"Token expires at {expDate:u}");
            //    _lastUsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            //    _lastToken = token;
            //}
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Provides factory-style HTTP method handlers used by the Jira command invocation layer.
        /// Each handler corresponds to a specific HTTP verb and is resolved dynamically using
        /// the <see cref="DescriptionAttribute"/> applied to the method.
        /// </summary>
        private static class MethodFactory
        {
            // Sends an HTTP DELETE request using the Jira authentication model together with
            // the route and metadata defined in the <see cref="HttpCommand"/> instance.
            // The method constructs the request, applies authentication, enriches it with
            // Xray-specific routing when required, and returns the raw response body.
            [Description("DELETE")]
            public static string SendDeleteRequest(JiraAuthenticationModel authentication, HttpCommand command)
            {
                // Builds the full DELETE endpoint URL.
                var requestUri = $"{_jiraBaseAddress}{command.Route}";

                // Creates the DELETE request message.
                var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
                request.Headers.Authorization = authentication.NewAuthenticationHeader();

                // Applies Xray-specific headers and routing when the command
                // includes an X-acpt value.
                request = NewXpandRequest(authentication, request, command);

                // Sends the request and returns the resulting raw response body.
                return request.Send();
            }

            // Sends an HTTP GET request using the Jira authentication model and the details
            // provided in the HttpCommand instance. The method constructs the
            // request URI using the Jira base address, applies authentication, optionally
            // enriches the request for Xray-specific behavior, and returns the raw response body.
            [Description("GET")]
            public static string SendGetRequest(JiraAuthenticationModel authentication, HttpCommand command)
            {
                // Builds the full request URL by combining the Jira base address with the
                // route defined in the command.
                var requestUri = $"{_jiraBaseAddress}{command.Route}";

                // Creates the outgoing GET request.
                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

                // Applies the basic Authorization header derived from the authentication model.
                request.Headers.Authorization = authentication.NewAuthenticationHeader();

                // Applies Xray-specific routing and headers when the X-acpt header is defined.
                request = NewXpandRequest(authentication, request, command);

                // Sends the request and returns the resulting body or fallback error response.
                return request.Send();
            }

            // Sends an HTTP POST request using the Jira authentication model and the information
            // provided in the HttpCommand instance. The method serializes the payload,
            // builds the request message, applies authentication, enriches the request with Xray
            // routing when necessary, and returns the raw response string.
            [Description("POST")]
            public static string SendPostRequest(JiraAuthenticationModel authentication, HttpCommand command)
            {
                // Serializes the Data object using the configured JSON options.
                var content = JsonSerializer.Serialize(command.Data, _jsonOptions);

                // When the Data object is already a JSON element or a raw string,
                // serialize by direct string conversion instead.
                if (command.Data is JsonElement || command.Data is string)
                {
                    content = $"{command.Data}";
                }

                // Builds the full URI for the outgoing request.
                var requestUri = $"{_jiraBaseAddress}{command.Route}";

                // Creates the outgoing POST request.
                var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                request.Headers.Authorization = authentication.NewAuthenticationHeader();

                // Attaches the serialized body to the outgoing request.
                request.Content = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json);

                // Applies Xray routing and authentication when required
                // (via the presence of an X-acpt header).
                request = NewXpandRequest(authentication, request, command);

                // Sends the request and returns the response body.
                return request.Send();
            }

            // Sends an HTTP PUT request using the Jira authentication model together with the
            // configuration supplied in the <see cref="HttpCommand"/> instance. The method serializes
            // the payload, constructs the PUT request, applies authentication, optionally enriches
            // the request for Xray routing, and returns the raw response body.
            [Description("PUT")]
            public static string SendPutRequest(JiraAuthenticationModel authentication, HttpCommand command)
            {
                // Serializes the Data object using the configured JSON serializer.
                var content = JsonSerializer.Serialize(command.Data, _jsonOptions);

                // If the Data payload is already a JSON element or a raw string,
                // bypass normal serialization and use the direct representation instead.
                if (command.Data is JsonElement || command.Data is string)
                {
                    content = $"{command.Data}";
                }

                // Builds the fully qualified request URI for the PUT operation.
                var requestUri = $"{_jiraBaseAddress}{command.Route}";

                // Creates the PUT request message that will be sent to Jira or Xray.
                var request = new HttpRequestMessage(HttpMethod.Put, requestUri);
                request.Headers.Authorization = authentication.NewAuthenticationHeader();

                // Assigns the serialized payload to the request body.
                request.Content = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json);

                // Enriches the request with Xray routing and JWT authentication when the command
                // includes the X-acpt header.
                request = NewXpandRequest(authentication, request, command);

                // Sends the request and returns the resulting string response.
                return request.Send();
            }

            // TODO: Handle 429 Too Many Requests responses with retry-after logic.
            // Enriches the given HTTP request message with Xray-specific authentication and routing
            // based on the provided command headers. When a valid X-acpt header value is present,
            // the method obtains an Xray JWT token and updates the request accordingly.
            private static HttpRequestMessage NewXpandRequest(
                JiraAuthenticationModel authentication,
                HttpRequestMessage requestMessage,
                HttpCommand command)
            {
                // X-acpt header constant.
                const string Xacpt = "X-acpt";

                // Returns the original request when no headers are defined on the command.
                if (command.Headers == default)
                {
                    return requestMessage;
                }
                // Returns the original request when the X-acpt header is not present.
                else if (!command.Headers.TryGetValue(Xacpt, out string value))
                {
                    return requestMessage;
                }
                // Returns the original request when the X-acpt header is empty.
                else if (string.IsNullOrEmpty(value))
                {
                    return requestMessage;
                }

                // TODO: cache token and check expiration - if about to expire, issue new one
                // Resolves the Xray JWT token using the header value as the issue key context.
                var token = authentication.GetJwt(issueKey: command.Headers[Xacpt]).Result;

                // Adds the resolved token to the outgoing request headers.
                requestMessage.Headers.Add(name: Xacpt, value: token);

                // Points the request URI to the Xray base address combined with the command route.
                requestMessage.RequestUri = new Uri($"{_xpandBaseAddress}{command.Route}");

                // returns the enriched request message.
                return requestMessage;
            }
        }
        #endregion
    }
}
