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

                // Set default for NumberOfRetries if not set or invalid.
                jiraOptions.RetryOptions.MaxAttempts = jiraOptions.RetryOptions.MaxAttempts <= 0
                    ? 3
                    : jiraOptions.RetryOptions.MaxAttempts;

                // Each setting attempts to read its environment override in a single expression.
                jiraOptions.ApiKey = GetOrDefault("JIRA_API_KEY", jiraOptions.ApiKey);
                jiraOptions.ApiVersion = GetOrDefault("JIRA_API_VERSION", jiraOptions.ApiVersion);
                jiraOptions.BaseUrl = GetOrDefault("JIRA_BASE_URL", jiraOptions.BaseUrl).TrimEnd('/');
                jiraOptions.BucketSize = GetOrDefault("JIRA_BUCKET_SIZE", jiraOptions.BucketSize);
                jiraOptions.IsCloud = GetOrDefault("JIRA_IS_CLOUD", jiraOptions.IsCloud);
                jiraOptions.ResolveCustomFields = GetOrDefault("JIRA_RESOLVE_CUSTOM_FIELDS", jiraOptions.ResolveCustomFields);
                jiraOptions.Username = GetOrDefault("JIRA_USERNAME", jiraOptions.Username);

                // Xray Cloud specific settings.
                jiraOptions.XrayOptions.BaseUrl = GetOrDefault(
                    environmentParameter: "XRAY_CLOUD_BASE_URL",
                    defaultValue: jiraOptions.XrayOptions.BaseUrl
                ).TrimEnd('/');

                // Override the configured retry delay with an environment variable value when present.
                // This allows operational tuning without modifying application configuration files.
                jiraOptions.RetryOptions.DelayMilliseconds = GetOrDefault(
                    environmentParameter: "JIRA_DELAY_MILLISECONDS",
                    defaultValue: jiraOptions.RetryOptions.DelayMilliseconds
                );

                // Override the maximum number of retry attempts using an environment variable.
                // When the variable is not defined or cannot be converted, the existing configuration is preserved.
                jiraOptions.RetryOptions.MaxAttempts = GetOrDefault(
                    environmentParameter: "JIRA_MAX_ATTEMPTS",
                    defaultValue: jiraOptions.RetryOptions.MaxAttempts
                );

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

        // Reads an environment variable and attempts to convert its value to the specified type,
        // returning a fallback value when the variable is missing or cannot be converted.
        private static T GetOrDefault<T>(string environmentParameter, T defaultValue)
        {
            // Read the raw value of the environment variable from the process environment.
            var value = Environment.GetEnvironmentVariable(environmentParameter);

            // If the variable is not set or contains only whitespace, return the provided default.
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            try
            {
                // Resolve the effective target type.
                // Nullable types are unwrapped so conversion is performed against the underlying type.
                var underlyingType = Nullable.GetUnderlyingType(typeof(T));
                var targetType = underlyingType ?? typeof(T);

                // Apply explicit handling for boolean values.
                // This supports common representations such as textual and numeric forms.
                if (targetType == typeof(bool))
                {
                    if (bool.TryParse(value, out bool boolResult))
                    {
                        return (T)(object)boolResult;
                    }

                    return value.ToLowerInvariant() switch
                    {
                        "1" => (T)(object)true,
                        "0" => (T)(object)false,
                        "true" => (T)(object)true,
                        "false" => (T)(object)false,
                        _ => defaultValue
                    };
                }

                // Attempt to convert the string value to the target type using the system conversion facilities.
                // Whitespace is trimmed to avoid format-related conversion failures.
                return (T)Convert.ChangeType(value.Trim(), targetType);
            }
            catch
            {
                // If any conversion error occurs, return the default value to ensure safe fallback behavior.
                return defaultValue;
            }
        }
        #endregion
    }
}
