using Mcp.Xray.Domain.Framework;
using Mcp.Xray.Domain.Models;

using Newtonsoft.Json.Linq;

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

#pragma warning disable S2325 // Unused private types or members should be removed
namespace Mcp.Xray.Domain.Extensions
{
    internal static class LocalExtensions
    {
        private static string s_interactiveJwt;
        private static readonly HttpClient s_httpClient = new();

        extension(Assembly assembly)
        {
            public string ReadEmbeddedResource(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return string.Empty;
                }

                var fileReference = Array
                    .Find(assembly
                    .GetManifestResourceNames(), i => i.EndsWith(Path.GetFileName(name), StringComparison.OrdinalIgnoreCase));
                
                if (string.IsNullOrEmpty(fileReference))
                {
                    return string.Empty;
                }

                var stream = assembly.GetManifestResourceStream(fileReference);
                
                using StreamReader reader = new(stream);
                
                return reader.ReadToEnd();
            }
        }

        extension(JiraAuthenticationModel authenticationModel)
        {
            public async Task<string> GetJwt(string issue)
            {
                try
                {
                    var response = (await GetInteractiveIssueToken(authenticationModel, issue)).ConvertToJsonToken();
                    var options = response.SelectTokens("..options").FirstOrDefault()?.ToString();
                    var token = JToken.Parse(options).SelectToken("contextJwt")?.ToString();

                    if (string.IsNullOrEmpty(token))
                    {
                        return s_interactiveJwt;
                    }

                    s_interactiveJwt = token;
                    return s_interactiveJwt;
                }
                catch (Exception)
                {
                    return s_interactiveJwt;
                }
            }

            public async Task<string> GetInteractiveIssueToken(string issue)
            {
                var data = Assembly
                    .GetExecutingAssembly()
                    .ReadEmbeddedResource("get_interactive_token.txt")
                    .Replace("[project-key]", authenticationModel.Project)
                    .Replace("[issue-key]", issue);

                var authorization = authenticationModel.NewAuthenticationHeader();
                var route = "/rest/gira/1/?operation=issueViewInteractiveQuery";
                var url = authenticationModel.Collection.TrimEnd('/') + route;
                var request = new HttpRequestMessage
                {
                    Content = new StringContent(data, Encoding.UTF8, "application/json"),
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(url)
                };
                request.Headers.Authorization = authorization;

                var response = await s_httpClient.SendAsync(request);

                return response.IsSuccessStatusCode
                    ? await response.Content.ReadAsStringAsync()
                    : "{}";
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

        extension(IDictionary<string, object> capabilities)
        {
            /// <summary>
            /// Retrieves a specific capability from a dictionary of capabilities.
            /// It attempts to find the capability by the given key and converts the result into the specified type.
            /// If the capability is not found or an error occurs during the process, it returns the provided default value.
            /// </summary>
            /// <typeparam name="T">The type to which the capability value will be converted.</typeparam>
            /// <param name="capabilities">A dictionary containing the available capabilities.</param>
            /// <param name="capability">The key or path to the desired capability.</param>
            /// <param name="defaultValue">The default value to return if the capability is not found or an error occurs.</param>
            /// <returns>The value of the capability if found, otherwise the default value.</returns>
            public T GetCapability<T>(string capability, T defaultValue)
            {
                try
                {
                    // Construct the path for selecting the capability from the capabilities dictionary
                    var path = $"..{capability}";

                    // If capabilities are provided, parse them as a JSON object; otherwise, use an empty JSON object
                    var capabilitiesDocument = capabilities != default
                        ? JObject.Parse(JsonSerializer.Serialize(capabilities))
                        : JObject.Parse("{}");

                    // Attempt to select the token for the specified capability path
                    var element = capabilitiesDocument.SelectToken(path);

                    // If the element is found, convert it to the desired type; otherwise, return the default value
                    return element == null ? defaultValue : element.ToObject<T>();
                }
                catch (Exception)
                {
                    // Return the default value if any exception occurs during the process
                    return defaultValue;
                }
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

            public JsonDocument ConvertToJsonDocument()
            {
                input = string.IsNullOrEmpty(input) ? "{}" : input;

                return input.ConfirmJson() ? JsonDocument.Parse(input) : JsonDocument.Parse("{}");
            }

            public JObject ConvertToJsonObject()
            {
                input = string.IsNullOrEmpty(input) ? "{}" : input;

                return input.ConfirmJson() ? JObject.Parse(input) : JObject.Parse("{}");
            }

            /// <summary>
            /// Converts the provided string to a JToken.
            /// </summary>
            /// <param name="token">The string to convert.</param>
            /// <returns>A JToken representation of the string.</returns>
            public JToken ConvertToJsonToken()
            {
                input = string.IsNullOrEmpty(input) ? "{}" : input;

                return input.ConfirmJson() ? JToken.Parse(input) : JToken.Parse("{}");
            }
        }

        extension (JToken token)
        {
            public JObject ConvertToJsonObject()
            {
                try
                {
                    // Convert JToken to JSON string or use an empty object if JToken is default
                    var json = token == default ? "{}" : $"{token}";

                    // Parse the JSON string into a JObject
                    return JObject.Parse(json);
                }
                catch (Exception)
                {
                    // Return an empty JObject in case of any exception during parsing
                    return JObject.Parse("{}");
                }
            }
        }

        extension (HttpCommand command)
        {
            /// <summary>
            /// Sends an HTTP command to Jira using the provided executor and default authentication.
            /// </summary>
            /// <param name="command">The HTTP command to send.</param>
            /// <param name="executor">The JiraCommandsExecutor to use. If not provided, a new one with default authentication will be created.</param>
            /// <returns>The response from the Jira server.</returns>
            public string Send(JiraCommandInvoker executor)
            {
                executor ??= new JiraCommandInvoker(authentication: default);

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
                authentication ??= new JiraAuthenticationModel();

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

        extension<T>(IEnumerable<T> collection)
        {
            public IEnumerable<IEnumerable<T>> Split(int itemsPerSet)
            {
                collection = collection ?? [];

                var sourceList = collection.ToList();

                for (int index = 0; index < sourceList.Count; index += itemsPerSet)
                {
                    yield return sourceList.Skip(index).Take(itemsPerSet);
                }
            }
        }

        extension<T>(ConcurrentBag<T> collection)
        {
            public void AddRange(IEnumerable<T> range)
            {
                foreach (T item in range)
                {
                    collection.Add(item);
                }
            }
        }
    }
}
