using G4.Converters;

using Mcp.Xray.Settings.Models;

using Microsoft.Extensions.Configuration;

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Xray.Settings
{
    /// <summary>
    /// Represents the application settings including configuration, JSON options, and LiteDB connection.
    /// </summary>
    public static class AppSettings
    {
        #region *** Fields    ***
        /// <summary>
        /// Represents the current version of the API supported by this client.
        /// </summary>
        public const string ApiVersion = "4";

        /// <summary>
        /// Gets the application configuration.
        /// </summary>
        public static readonly IConfigurationRoot Configuration = NewConfiguraion();

        /// <summary>
        /// Provides a shared instance of <see cref="System.Net.Http.HttpClient"/> configured with a 30-minute timeout for HTTP
        /// operations. This instance is intended for reuse to avoid socket exhaustion and improve performance.
        /// The timeout for all requests sent using this client is set to 30 minutes. Modifying the timeout
        /// or other properties may affect all consumers of this shared client.
        /// </summary>
        public static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        /// <summary>
        /// Gets the Jira options populated from configuration and environment variables.
        /// </summary>
        public static readonly JiraOptionsModel JiraOptions = NewJiraOptions();

        /// <summary>
        /// Gets the JSON serialization options.
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = NewJsonOptions();
        #endregion

        #region *** Methods   ***
        // Creates a new instance of IConfigurationRoot by configuring it with settings from appsettings.json and environment variables.
        private static IConfigurationRoot NewConfiguraion()
        {
            // Create a new ConfigurationBuilder instance
            new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(path: "appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            // Create a new ConfigurationBuilder instance
            var configurationBuilder = new ConfigurationBuilder();

            // Set the base path for the configuration file to the current directory
            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());

            // Add the appsettings.json file as a configuration source, if it exists (optional), without reloading it on change
            configurationBuilder.AddJsonFile(path: "appsettings.json", optional: true, reloadOnChange: false);

            // Add environment variables as a configuration source
            configurationBuilder.AddEnvironmentVariables();

            // Build and return the IConfigurationRoot instance
            return configurationBuilder.Build();
        }

        // Creates and populates a JiraOptionsModel by reading configuration values 
        // from appsettings.json and environment variables. Environment variables override the file values.
        private static JiraOptionsModel NewJiraOptions()
        {
            try
            {
                // Build configuration and access the Jira options section.
                var section = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(path: "appsettings.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build()
                    .GetSection("G4:JiraOptions");

                // Bind the configuration section or fall back to a default model.
                var jiraOptions = section.Get<JiraOptionsModel>() ?? new JiraOptionsModel();

                // Ensure nested XrayCloudOptions is not null.
                jiraOptions.XrayOptions ??= new JiraOptionsModel.XrayOptionsModel();

                // Set default for ApiVersion if not set.
                jiraOptions.ApiVersion = string.IsNullOrWhiteSpace(jiraOptions.ApiVersion)
                    ? "latest"
                    : jiraOptions.ApiVersion;

                // Set default for BucketSize if not set or invalid.
                jiraOptions.BucketSize = jiraOptions.BucketSize <= 0
                    ? 4
                    : jiraOptions.BucketSize;

                // Each setting attempts to read its environment override in a single expression.
                jiraOptions.ApiKey = GetOrDefault("JIRA_API_KEY", jiraOptions.ApiKey);
                jiraOptions.ApiVersion = GetOrDefault("JIRA_API_VERSION", jiraOptions.ApiVersion);
                jiraOptions.BaseUrl = GetOrDefault("JIRA_BASE_URL", jiraOptions.BaseUrl).TrimEnd('/');
                jiraOptions.BucketSize = GetOrDefault("JIRA_BUCKET_SIZE", jiraOptions.BucketSize);
                jiraOptions.Username = GetOrDefault("JIRA_USERNAME", jiraOptions.Username);

                // Xray Cloud specific settings.
                jiraOptions.XrayOptions.BaseUrl = GetOrDefault(
                    "XRAY_CLOUD_BASE_URL",
                    jiraOptions.XrayOptions.BaseUrl).TrimEnd('/');

                // Return the populated Jira options model.
                return jiraOptions;
            }
            catch
            {
                // In case of any error, return a new default instance.
                return new();
            }
        }

        // Creates a new instance of JsonSerializerOptions with custom settings and converters.
        private static JsonSerializerOptions NewJsonOptions()
        {
            // Initialize JSON serialization options.
            var jsonOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // Add a custom exception converter
            jsonOptions.Converters.Add(new ExceptionConverter());

            // Add a custom method base converter
            jsonOptions.Converters.Add(new MethodBaseConverter());

            // Add a custom type converter
            jsonOptions.Converters.Add(new TypeConverter());

            // Add a custom DateTime converter for ISO 8601 format (yyyy-MM-ddTHH:mm:ss.ffffffK)
            jsonOptions.Converters.Add(new DateTimeIso8601Converter());

            // Return the JSON options with custom settings and converters added
            return jsonOptions;
        }

        // Retrieves a value from an environment variable and converts it to the specified type <typeparamref name="T"/>.
        // If the environment variable is not found, empty, or cannot be converted, returns the provided default value.
        private static T GetOrDefault<T>(string environmentParameter, T defaultValue)
        {
            // Attempt to read the environment variable value
            var envValue = Environment.GetEnvironmentVariable(environmentParameter);

            // If the environment variable is missing or blank, use the default value
            if (string.IsNullOrWhiteSpace(envValue))
            {
                return defaultValue;
            }

            try
            {
                // Check if T is a nullable type and get the underlying type
                var underlyingType = Nullable.GetUnderlyingType(typeof(T));
                var targetType = underlyingType ?? typeof(T);

                // Special handling for booleans to support "true", "false", "1", and "0"
                if (targetType == typeof(bool))
                {
                    if (bool.TryParse(envValue, out bool boolResult))
                    {
                        return (T)(object)boolResult;
                    }

                    // Support numeric boolean representation
                    if (envValue.Trim() == "1") return (T)(object)true;
                    if (envValue.Trim() == "0") return (T)(object)false;
                }

                // Attempt to convert the string value to the target type using system conversion
                return (T)Convert.ChangeType(envValue.Trim(), targetType);
            }
            catch
            {
                // If conversion fails (e.g., invalid format), fall back to the default value
                return defaultValue;
            }
        }
        #endregion
    }
}
