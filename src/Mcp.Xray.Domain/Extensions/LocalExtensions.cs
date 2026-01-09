using Mcp.Xray.Domain.Framework;
using Mcp.Xray.Domain.Models;
using Mcp.Xray.Settings;

using Microsoft.AspNetCore.Http;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

#pragma warning disable S2325
namespace Mcp.Xray.Domain.Extensions
{
    /// <summary>
    /// Provides local extension for JSON operations and HTTP utilities
    /// used internally across the application.
    /// </summary>
    internal static class LocalExtensions
    {
        // Shared HttpClient reference used for all HTTP operations.
        // HttpClient is intentionally static to avoid socket exhaustion and to
        // reuse underlying handlers. The configured instance from
        // AppSettings.HttpClient ensures consistent headers,
        // timeouts, TLS settings, and retry policies across the application.
        private static readonly HttpClient _httpClient = AppSettings.HttpClient;

        // Cached JsonSerializerOptions instance used for all JSON
        // serialization/deserialization operations in this extension class.
        // The instance is pulled from AppSettings.JsonOptions so that
        // the entire application has a unified JSON behavior (case sensitivity,
        // converters, naming policy, etc.).
        private static readonly JsonSerializerOptions _jsonOptions = AppSettings.JsonOptions;

        extension(Assembly assembly)
        {
            /// <summary>
            /// Reads an embedded resource from the provided assembly using the supplied name.
            /// The lookup is performed by matching the file name portion against the assembly's
            /// manifest resource list. Returns an empty string when the resource cannot be found.
            /// </summary>
            /// <param name="name">The resource name or file name to search for. Only the last path segment is used.</param>
            /// <returns>The full text content of the embedded resource, or an empty string when the resource does not exist or the name is invalid.</returns>
            public string ReadEmbeddedResource(string name)
            {
                // Returns an empty string when the resource name is missing.
                if (string.IsNullOrEmpty(name))
                {
                    return string.Empty;
                }

                // Searches the assembly resource names for a match by file name (case-insensitive).
                var fileReference = Array
                    .Find(
                        assembly.GetManifestResourceNames(),
                        i => i.EndsWith(Path.GetFileName(name), StringComparison.OrdinalIgnoreCase)
                    );

                // Returns an empty string when no resource name matches.
                if (string.IsNullOrEmpty(fileReference))
                {
                    return string.Empty;
                }

                // Retrieves the embedded resource stream.
                var stream = assembly.GetManifestResourceStream(fileReference);

                // Reads the resource content into a string.
                using StreamReader reader = new(stream);
                return reader.ReadToEnd();
            }
        }

        extension<T>(ConcurrentBag<T> collection)
        {
            /// <summary>
            /// Adds all items from the specified <paramref name="range"/> into the current
            /// <see cref="ConcurrentBag{T}"/> instance. The method enumerates the incoming
            /// sequence and inserts each item individually using <see cref="ConcurrentBag{T}.Add(T)"/>.
            /// </summary>
            /// <param name="range"> The sequence of items that should be added to the collection. When the sequence is null, the method performs no work.</param>
            public void AddRange(IEnumerable<T> range)
            {
                // Exit early when no items are provided.
                if (range is null)
                {
                    return;
                }

                // Adds each item to the concurrent bag.
                foreach (T item in range)
                {
                    collection.Add(item);
                }
            }
        }

        extension(HttpCommand command)
        {
            /// <summary>
            /// Sends an HTTP command to Jira using the provided executor and default authentication.
            /// </summary>
            /// <param name="command">The HTTP command to send.</param>
            /// <param name="executor">The JiraCommandsExecutor to use. If not provided, a new one with default authentication will be created.</param>
            /// <returns>The response from the Jira server.</returns>
            public string Send(JiraCommandInvoker executor)
            {
                // Create a new executor with default authentication if not provided
                executor ??= new JiraCommandInvoker(authentication: default);

                // Send the command and return the response
                return executor.SendCommand(command);
            }

            /// <summary>
            /// Sends an HTTP command to Jira using the provided authentication and default executor.
            /// </summary>
            /// <param name="command">The HTTP command to send.</param>
            /// <param name="authentication">The JiraAuthentication to use. If not provided, default authentication will be used.</param>
            /// <returns>The response from the Jira server.</returns>
            public string Send(JiraAuthenticationModel authentication)
            {
                // Create a new authentication if not provided
                authentication ??= new JiraAuthenticationModel();

                // Delegate to the Send method with the provided authentication and default executor
                return Send(command, authentication, executor: default);
            }

            /// <summary>
            /// Sends an HTTP command to Jira using the provided authentication and executor.
            /// </summary>
            /// <param name="command">The HTTP command to send.</param>
            /// <param name="authentication">The JiraAuthentication to use. If not provided, default authentication will be used.</param>
            /// <param name="executor">The JiraCommandsExecutor to use. If not provided, a new one with the specified authentication will be created.</param>
            /// <returns>The response from the Jira server.</returns>
            public string Send(JiraAuthenticationModel authentication, JiraCommandInvoker executor)
            {
                // Create a new executor if not provided
                executor ??= new JiraCommandInvoker(authentication ?? new());

                // Send the command and return the response
                return executor.SendCommand(command);
            }
        }


        extension(HttpRequest request)
        {
            /// <summary>
            /// Reads the request body asynchronously and deserializes it into the specified type.
            /// </summary>
            /// <typeparam name="T">The type to deserialize the request body into.</typeparam>
            /// <param name="request">The HTTP request containing the body to read.</param>
            /// <returns>A task that represents the asynchronous read operation. The task result contains the deserialized object of type <typeparamref name="T"/>.</returns>
            /// <exception cref="NotSupportedException">Thrown when the request body is not in JSON format.</exception>
            public async Task<T> ReadAsync<T>()
            {
                // Read the request body as a string.
                var requestBody = await ReadAsync(request);

                // Validate that the request body is in JSON format.
                if (!requestBody.ConfirmJson())
                {
                    throw new NotSupportedException("The request body must be JSON formatted.");
                }

                // Deserialize the JSON request body into the specified type.
                return JsonSerializer.Deserialize<T>(requestBody);
            }

            /// <summary>
            /// Reads the request body asynchronously as a string.
            /// </summary>
            /// <param name="request">The HTTP request containing the body to read.</param>
            /// <returns>A task that represents the asynchronous read operation. The task result contains the request body as a string.</returns>
            public async Task<string> ReadAsync()
            {
                // Create a StreamReader to read the request body stream.
                using var streamReader = new StreamReader(request.Body);

                // Read the entire request body asynchronously and return it as a string.
                return await streamReader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        extension(HttpRequestMessage request)
        {
            /// <summary>
            /// Sends the configured HTTP request using the shared HttpClient instance.
            /// Returns the raw response body on success, or a generic JSON error object
            /// when the request fails or the body is empty.
            /// </summary>
            /// <returns>The response body as a string, or a generic error response when the request fails.</returns>
            public string Send()
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
        }

        extension(HttpResponseMessage response)
        {
            /// <summary>
            /// Creates a standardized JSON response representation from an HTTP status code.
            /// The method extracts the status code, reason phrase, and response body, then wraps
            /// them into a serializable object for consistent error reporting.
            /// </summary>
            /// <param name="response">The HTTP response from which status and body information is extracted.</param>
            /// <returns>A JSON string containing the status code, reason phrase, raw body, and a fixed identifier used to indicate that the response originated from a status-only result.</returns>
            public string NewGenericResponse()
            {
                // Builds a simple JSON-compatible object that represents the error details.
                // -1 is a static identifier indicating a synthetic or fallback response.
                var responseObject = new
                {
                    Code = response.StatusCode,
                    Reason = response.ReasonPhrase,
                    Body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(),
                    Id = "-1"
                };

                // Serializes the structured response using the configured JSON options.
                return JsonSerializer.Serialize(responseObject, _jsonOptions);
            }
        }

        extension<T>(IEnumerable<T> collection)
        {
            /// <summary>
            /// Splits the source sequence into multiple subsequences (batches), each containing
            /// up to <paramref name="itemsPerSet"/> elements. The method yields each batch
            /// lazily as an <see cref="IEnumerable{T}"/>.
            /// </summary>
            /// <param name="itemsPerSet">The maximum number of items each returned batch may contain. Must be greater than zero; otherwise, the method yields no batches.</param>
            /// <returns>A sequence of batches, where each batch is an <see cref="IEnumerable{T}"/> representing a segment of the original sequence.</returns>
            public IEnumerable<IEnumerable<T>> Split(int itemsPerSet)
            {
                // Ensures a non-null sequence to iterate over.
                collection ??= [];

                // An invalid batch size produces no output.
                if (itemsPerSet <= 0)
                {
                    yield break;
                }

                // Materialize the input once to prevent re-enumeration.
                var sourceList = collection.ToList();

                // Iterate through the list in increments of the batch size.
                for (int index = 0; index < sourceList.Count; index += itemsPerSet)
                {
                    // Yield a batch of items starting at the current index.
                    yield return sourceList
                        .Skip(index)
                        .Take(itemsPerSet);
                }
            }
        }

        extension(JiraAuthenticationModel authenticationModel)
        {
            /// <summary>
            /// Retrieves the Xray JWT token associated with the specified Jira issue key.
            /// The method invokes the interactive issue token endpoint, parses the response,
            /// and extracts the contextJwt value when available.
            /// </summary>
            /// <param name="issueKey">The Jira issue key that provides the context for the JWT generation.</param>
            /// <returns>The JWT token string when it can be resolved successfully, or an empty string when the token is missing or an error occurs.</returns>
            public async Task<string> GetJwt(string issueKey)
            {
                // Retrieves an interactive issue token from Jira by executing the
                // <c>issueViewInteractiveQuery</c> operation using the embedded template file.
                static async Task<string> GetInteractiveIssueToken(JiraAuthenticationModel authenticationModel, string issue)
                {
                    // Read JSON request template from embedded resource.
                    var template = Assembly
                        .GetExecutingAssembly()
                        .ReadEmbeddedResource("get-interactive-token.txt");

                    // Cannot continue without a valid template.
                    if (string.IsNullOrEmpty(template))
                    {
                        return "{}";
                    }

                    // Prepare request body by injecting project key and issue key.
                    var data = template
                        .Replace("[project-key]", authenticationModel.Project)
                        .Replace("[issue-key]", issue);

                    // Build Jira route.
                    const string OperationRoute = "/rest/gira/1/?operation=issueViewInteractiveQuery";
                    var url = $"{authenticationModel.Collection.TrimEnd('/')}{OperationRoute}";

                    // Prepare the HTTP POST request.
                    var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url))
                    {
                        Content = new StringContent(data, Encoding.UTF8, "application/json")
                    };

                    // Attach authentication.
                    request.Headers.Authorization = authenticationModel.NewAuthenticationHeader();

                    // Send the request.
                    var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                    // Return body or "{}" on failure.
                    return response.IsSuccessStatusCode
                        ? await response.Content.ReadAsStringAsync().ConfigureAwait(false)
                        : "{}";
                }

                // Attempts to parse the response and extract the JWT token.
                try
                {
                    // Calls the interactive issue token API and parses the returned JSON into a JObject.
                    var response = (await GetInteractiveIssueToken(authenticationModel, issueKey))
                        .ConvertToJsonObject();

                    // Extracts the options JSON fragment from the nested structure.
                    var options = response
                        .SelectTokens("..options")
                        .FirstOrDefault()
                        ?.ToString();

                    // Parses the options fragment and selects the contextJwt property.
                    var token = Newtonsoft.Json.Linq.JObject
                        .Parse(options)
                        .SelectToken("contextJwt")
                        ?.ToString();

                    // Returns the resolved token or an empty string when not available.
                    return string.IsNullOrEmpty(token)
                        ? string.Empty
                        : token;
                }
                catch (Exception)
                {
                    // Returns an empty string when any error occurs during parsing or token extraction.
                    return string.Empty;
                }
            }

            /// <summary>
            /// Creates an HTTP authentication header using Jira authentication model.
            /// This method extracts the username and password from the provided model
            /// and generates a Basic Authentication header.
            /// </summary>
            /// <param name="authenticationModel">The Jira authentication model containing the username and password.</param>
            /// <returns>An AuthenticationHeaderValue containing the Basic Authentication header.</returns>
            public AuthenticationHeaderValue NewAuthenticationHeader()
            {
                // Extracting the username and password from the provided authentication model
                var username = authenticationModel.Username;
                var password = authenticationModel.Password;

                // Combining the username and password into the "username:password" format
                var header = $"{username}:{password}";

                // Encoding the combined string in Base64
                var encodedHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));

                // Returning the Authentication header in Basic format
                return new AuthenticationHeaderValue("Basic", encodedHeader);
            }
        }

        extension(JsonElement jsonElement)
        {
            /// <summary>
            /// Retrieves the value of the <c>"token"</c> property from a <see cref="JsonElement"/>, 
            /// or falls back to a default value provided by the factory function if the property does not exist 
            /// or cannot be converted to the target type.
            /// </summary>
            /// <typeparam name="T">The expected type of the "token" value. Supports <see cref="string"/>, <see cref="int"/>, and <see cref="bool"/>.</typeparam>
            /// <param name="propertyName"> The name of the property to retrieve.</param>
            /// <param name="defaultValue">A function that produces a default value when the "token" property is missing or not supported.</param>
            /// <returns>Returns the value of the "token" property if found and successfully converted; otherwise, returns the value produced by the <paramref name="defaultValue"/>.</returns>
            public T GetOrDefault<T>(string propertyName, Func<T> defaultValue)
            {
                // Try to get the "token" property from the JSON element.
                // This could represent an authentication token or API key.
                var isToken = jsonElement.TryGetProperty(propertyName, out var tokenOut);

                // If the "token" property does not exist, return the default value from the factory.
                if (!isToken)
                {
                    return defaultValue();
                }

                // If the "token" property exists, decide how to extract its value
                // based on its JSON value type.
                return tokenOut.ValueKind switch
                {
                    // If it's a string, cast to object first, then to generic type T.
                    JsonValueKind.String => (T)(object)tokenOut.GetString(),

                    // If it's a number, interpret it as a 32-bit integer.
                    JsonValueKind.Number => (T)(object)tokenOut.GetInt32(),

                    // If it's a boolean true, return true.
                    JsonValueKind.True => (T)(object)true,

                    // If it's a boolean false, return false.
                    JsonValueKind.False => (T)(object)false,

                    // If it's any other type (array, object, null, undefined, etc.), 
                    // fall back to the default value from the factory.
                    _ => defaultValue()
                };
            }
        }

        extension(string input)
        {
            /// <summary>
            /// Determines whether the current input string contains valid JSON.
            /// The method attempts to parse the string and returns a boolean result
            /// indicating whether the parsing succeeded.
            /// </summary>
            /// <returns>True when the input string represents valid JSON; otherwise false.</returns>
            public bool ConfirmJson()
            {
                try
                {
                    // Attempts to parse the input string as JSON.
                    // If parsing succeeds, the string is valid JSON.
                    JsonDocument.Parse(input);
                    return true;
                }
                catch
                {
                    // Any exception indicates invalid JSON.
                    return false;
                }
            }

            /// <summary>
            /// Converts the current string input into a <see cref="JsonDocument"/>.  
            /// When the input is empty or invalid JSON, the method safely falls back to an empty object.
            /// </summary>
            /// <returns>A <see cref="JsonDocument"/> instance representing the parsed JSON content,or an empty JSON object when parsing fails.</returns>
            public JsonDocument ConvertToJsonDocument()
            {
                // Uses "{}" when the input is null or empty.
                input = string.IsNullOrEmpty(input)
                    ? "{}"
                    : input;

                // Parses the input only when it contains valid JSON.
                // Falls back to an empty JSON object on invalid content.
                return input.ConfirmJson()
                    ? JsonDocument.Parse(input)
                    : JsonDocument.Parse("{}");
            }

            /// <summary>
            /// Converts the current string input into a JObject.
            /// When the input is empty or invalid JSON, the method returns an empty object instead
            /// of throwing an exception.
            /// </summary>
            /// <returns>A JObject created from the input string, or an empty object when the input is null, empty, or contains invalid JSON.</returns>
            public Newtonsoft.Json.Linq.JObject ConvertToJsonObject()
            {
                // Falls back to an empty JSON object when the input is null or empty.
                input = string.IsNullOrEmpty(input) ? "{}" : input;

                // Parses the string only when it contains valid JSON.
                // Returns an empty JSON object otherwise.
                return input.ConfirmJson()
                    ? Newtonsoft.Json.Linq.JObject.Parse(input)
                    : Newtonsoft.Json.Linq.JObject.Parse("{}");
            }
        }
    }
}
