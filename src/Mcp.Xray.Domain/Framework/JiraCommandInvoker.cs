using Mcp.Xray.Domain.Extensions;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using Microsoft.Extensions.Logging;

//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json.Serialization;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mcp.Xray.Domain.Framework
{
    public class JiraCommandInvoker
    {
        private const string _mediaType = "application/json";


        // API version used for Jira REST API, read from application settings.
        private static readonly string _apiVersion = AppSettings.JiraOptions.ApiVersion;

        // Bucket size used for batched operations, read from application settings.
        private static readonly int _bucketSize = AppSettings.JiraOptions.BucketSize;

        // Shared HttpClient instance from application settings.
        private static readonly HttpClient _httpClient = AppSettings.HttpClient;

        private static readonly string _jiraBaseAddress = AppSettings.JiraOptions.BaseUrl;

        private static readonly JsonSerializerOptions _jsonOptions = AppSettings.JsonOptions;

        // Xray Cloud Xpand-It endpoint used for specific operations.
        // Legacy endpoint: https://xray.cloud.xpand-it.com
        private static readonly string _xpandBaseAddress = AppSettings.JiraOptions.XrayCloudOptions.BaseUrl;

        private readonly JiraAuthenticationModel _authentication;
        
        private readonly ILogger _logger;

        #region *** Constructors ***
        public JiraCommandInvoker(JiraAuthenticationModel authentication)
            : this(authentication, logger: default)
        { }

        public JiraCommandInvoker(JiraAuthenticationModel authentication, ILogger logger)
        {
            _authentication = authentication;
            _logger = logger;
        }
        #endregion

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
            return SendRequest(requestMessage)
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

            // Resolves the Xray JWT token using the header value as the issue key context.
            var token = authentication.GetJwt(issueKey: command.Headers[Xacpt]).Result;

            // Adds the resolved token to the outgoing request headers.
            requestMessage.Headers.Add(name: Xacpt, value: token);

            // Points the request URI to the Xray base address combined with the command route.
            requestMessage.RequestUri = new Uri($"{_xpandBaseAddress}{command.Route}");

            // returns the enriched request message.
            return requestMessage;
        }

        // Sends the specified HTTP command using the internal <see cref="JiraCommandInvoker"/> instance.
        // The method resolves the appropriate handler based on the HTTP method description attribute
        // and invokes it. When no matching handler is found, a synthetic 404 response is returned.
        [SuppressMessage(
            category: "Major Code Smell",
            checkId: "S3011",
            Justification = "Needed for accessing private members to generate metadata")]
        private static string SendCommand(JiraCommandInvoker instance, HttpCommand command)
        {
            // Compares method descriptions using a case-insensitive comparison.
            const StringComparison Comapre = StringComparison.OrdinalIgnoreCase;

            // Retrieves all non-public instance methods that are decorated with DescriptionAttribute.
            // These methods represent the available HTTP handlers (for example GET, POST, PUT, DELETE).
            var methods = instance
                .GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
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
                return (string)method.Invoke(instance, [command]);
            }

            // When no handler matches the requested HTTP method, a synthetic 404 response is created
            // so that the caller receives a consistent error payload.
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                ReasonPhrase = string.Empty,
                Content = new StringContent(string.Empty)
            };

            return response.NewGenericResponse();
        }

        // Sends the provided HTTP request using the shared HTTP client and returns the raw response body.
        // When the response indicates a failure or contains an empty body, a formatted custom response
        // string is returned instead.
        private static string SendRequest(HttpRequestMessage request)
        {
            // Sends the HTTP request synchronously and waits for the response.
            var response = _httpClient
                .SendAsync(request)
                .GetAwaiter()
                .GetResult();

            // When the status code represents a failure, a custom error response is returned.
            if (!response.IsSuccessStatusCode)
            {
                return response.NewGenericResponse();
            }

            // Reads the response body as a string.
            var responseBody = response
                .Content
                .ReadAsStringAsync()
                .GetAwaiter()
                .GetResult();

            // Returns the raw body when available, otherwise falls back to a custom response wrapper.
            return string.IsNullOrEmpty(responseBody)
                ? response.NewGenericResponse()
                : responseBody;
        }

        #region *** Requests Factory ***
        // Sends an HTTP GET request using the Jira authentication model and the details
        // provided in the HttpCommand instance. The method constructs the
        // request URI using the Jira base address, applies authentication, optionally
        // enriches the request for Xray-specific behavior, and returns the raw response body.
        [Description("GET")]
        private static string SendGetRequest(JiraAuthenticationModel authentication, HttpCommand command)
        {
            // Builds the full request URL by combining the Jira base address with the
            // route defined in the command.
            var endpoint = $"{_jiraBaseAddress}{command.Route}";

            // Creates the outgoing GET request.
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            // Applies the basic Authorization header derived from the authentication model.
            request.Headers.Authorization = authentication.NewAuthenticationHeader();

            // Applies Xray-specific routing and headers when the X-acpt header is defined.
            request = NewXpandRequest(authentication, request, command);

            // Sends the request and returns the resulting body or fallback error response.
            return SendRequest(request);
        }

        [Description("POST")]
        private string Post(HttpCommand command)
        {

            // setup
            var request = GenericPostRequest(_authentication, command.Route, command.Data);

            // factor
            request = NewXpandRequest(request, command);

            // post
            return SendRequest(request);
        }

        [Description("PUT")]
        private string Put(HttpCommand command)
        {
            // setup
            var request = GenericPutRequest(_authentication, command.Route, command.Data);

            // factor
            request = NewXpandRequest(request, command);

            // post
            return SendRequest(request);
        }

        [Description("DELETE")]
        private string Delete(HttpCommand command)
        {
            // setup
            var request = GenericDeleteRequest(_authentication, command.Route);

            // factor
            request = NewXpandRequest(request, command);

            // get
            return SendRequest(request);
        }
        #endregion



        // gets a generic post request.
        private static HttpRequestMessage GenericPostRequest(JiraAuthenticationModel authentication, string route, object data)
        {
            //setup
            var onPayload = JsonSerializer.Serialize(data, _jsonOptions);
            if (data is JsonElement || data is string)
            {
                onPayload = $"{data}";
            }

            // post
            return GenericPostOrPutRequest(authentication, HttpMethod.Post, route, onPayload);
        }

        // gets a generic get request.
        private static HttpRequestMessage GenericDeleteRequest(JiraAuthenticationModel authentication, string route)
        {
            // address
            var baseAddress = authentication.Collection;
            var onRoute = route;
            var endpoint = baseAddress + onRoute;

            // setup: request
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            requestMessage.Headers.Authorization = authentication.NewAuthenticationHeader();

            // results
            return requestMessage;
        }

        // gets a generic put request.
        private static HttpRequestMessage GenericPutRequest(JiraAuthenticationModel authentication, string route, object data)
        {
            var onPayload = JsonSerializer.Serialize(data, _jsonOptions);
            
            if (data is JsonElement || data is string)
            {
                onPayload = $"{data}";
            }

            return GenericPostOrPutRequest(authentication, HttpMethod.Put, route, onPayload);
        }

        private static HttpRequestMessage GenericPostOrPutRequest(
            JiraAuthenticationModel authentication,
            HttpMethod method,
            string route,
            string data)
        {
            // address
            var baseAddress = authentication.Collection;
            var endpoint = baseAddress + route;

            // setup: request
            var requestMessage = new HttpRequestMessage(method, endpoint);
            requestMessage.Headers.Authorization = authentication.NewAuthenticationHeader();

            // set content
            requestMessage.Content = new StringContent(content: data, Encoding.UTF8, _mediaType);

            // results
            return requestMessage;
        }
    }
}
