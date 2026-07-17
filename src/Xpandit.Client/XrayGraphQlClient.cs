using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Xpandit.Client.Exceptions;
using Xpandit.Client.Models;

namespace Xpandit.Client
{
    /// <summary>
    /// Executes authenticated Xray Cloud GraphQL operations with shared token caching and repeatable HTTP sends.
    /// </summary>
    /// <remarks>
    /// The client owns bearer-token and retry state for its lifetime but does not own or dispose the supplied
    /// <see cref="HttpClient"/>. One instance can serve multiple repositories or direct callers concurrently.
    /// Authentication failures, exhausted retries, invalid responses, and GraphQL errors are reported through
    /// <see cref="XpanditClientException"/> without including credentials or bearer tokens.
    /// </remarks>
    public sealed class XrayGraphQlClient
    {
        #region *** Constants    ***
        private const int DefaultTokenLifetimeHours = 23;
        private const int JwtPayloadSegmentIndex = 1;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromMinutes(5);
        #endregion

        #region *** Fields       ***
        private readonly SemaphoreSlim _authenticationLock = new(1, 1);
        private readonly HttpClient _httpClient;
        private readonly XrayClientOptions _options;
        private readonly object _tokenStateLock = new();
        private string _accessToken;
        private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes an Xray GraphQL client with caller-owned HTTP infrastructure and immutable connection settings.
        /// </summary>
        /// <param name="httpClient">Reusable HTTP client retained and disposed by the caller.</param>
        /// <param name="options">Credentials, public endpoints, and repeatable-send configuration.</param>
        /// <exception cref="ArgumentException">Thrown when credentials, endpoints, or retry values are invalid.</exception>
        public XrayGraphQlClient(HttpClient httpClient, XrayClientOptions options)
        {
            AssertArguments(httpClient, options);

            // Reject incomplete options at construction so commands never fail after partially entering a retry flow.
            ConfirmOptions(options);

            // Retain caller-owned transport and validated settings for the complete authenticated client lifecycle.
            _httpClient = httpClient;
            _options = options;
            return;

            // Validates the constructor contract before option members are read or client state is retained.
            // The explicit parameter names keep failures tied to the public API rather than nested implementation data.
            static void AssertArguments(HttpClient httpClient, XrayClientOptions options)
            {
                // Require both constructor dependencies before validating or retaining either one.
                ArgumentNullException.ThrowIfNull(
                    argument: httpClient,
                    paramName: nameof(httpClient));

                ArgumentNullException.ThrowIfNull(
                    argument: options,
                    paramName: nameof(options));
            }
        }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Executes one GraphQL document without caller cancellation and returns the cloned <c>data</c> element.
        /// </summary>
        /// <param name="operationName">Diagnostic operation name included in failures without affecting GraphQL.</param>
        /// <param name="query">Complete GraphQL query or mutation document sent to Xray.</param>
        /// <param name="variables">JSON-compatible values serialized into the GraphQL variables object.</param>
        /// <returns>A detached JSON element containing the GraphQL response's <c>data</c> object.</returns>
        /// <remarks>
        /// The method reuses a current token, refreshes once after a 401, and applies the configured transient retry
        /// policy. The returned element is cloned so callers own it independently from internal response documents.
        /// </remarks>
        /// <exception cref="XpanditClientException">
        /// Thrown after authentication, transport, HTTP, JSON, or GraphQL processing cannot complete the operation.
        /// </exception>
        public async Task<JsonElement> InvokeAsync(
            string operationName,
            string query,
            object variables)
        {
            // Route convenience callers through the cancellation-aware transport lifecycle.
            return await InvokeAsync(
                operationName,
                query,
                variables,
                cancellationToken: default).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes one GraphQL document with caller cancellation and returns the cloned <c>data</c> element.
        /// </summary>
        /// <param name="operationName">Diagnostic operation name included in failures without affecting GraphQL.</param>
        /// <param name="query">Complete GraphQL query or mutation document sent to Xray.</param>
        /// <param name="variables">JSON-compatible values serialized into the GraphQL variables object.</param>
        /// <param name="cancellationToken">Token that stops authentication, retry delays, and pending HTTP work.</param>
        /// <returns>A detached JSON element containing the GraphQL response's <c>data</c> object.</returns>
        /// <remarks>
        /// The method reuses a current token, refreshes once after a 401, and applies the configured transient retry
        /// policy. The returned element is cloned so callers own it independently from internal response documents.
        /// </remarks>
        /// <exception cref="XpanditClientException">
        /// Thrown after authentication, transport, HTTP, JSON, or GraphQL processing cannot complete the operation.
        /// </exception>
        public async Task<JsonElement> InvokeAsync(
            string operationName,
            string query,
            object variables,
            CancellationToken cancellationToken)
        {
            AssertArguments(operationName, query, variables);

            // Acquire a current token before creating the authorized request so cached credentials are reused safely.
            var accessToken = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            // Send through the repeatable transport so transient protocol failures use the configured attempt policy.
            var response = await SendAuthorizedAsync(
                instance: this,
                accessToken,
                operationName,
                query,
                variables,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Release the unauthorized response before refreshing state so its connection can return to the pool.
                response.Dispose();

                // Invalidate only the rejected token so a concurrent successful refresh is never overwritten.
                ClearAccessToken(instance: this, accessToken);
                accessToken = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

                // Replay once with fresh authentication; a second 401 becomes the final command failure.
                response = await SendAuthorizedAsync(
                    instance: this,
                    accessToken,
                    operationName,
                    query,
                    variables,
                    cancellationToken).ConfigureAwait(false);
            }

            using (response)
            {
                // Convert the final protocol response into command data or a structured client exception.
                return await ReadGraphQlDataAsync(
                    response,
                    operationName,
                    cancellationToken).ConfigureAwait(false);
            }

            // Validates all values needed to build a repeatable GraphQL request before authentication starts.
            // Every guard names the declared public parameter so caller diagnostics remain analyzer compliant.
            static void AssertArguments(string operationName, string query, object variables)
            {
                // Require both GraphQL document identifiers before authentication or request allocation begins.
                ArgumentException.ThrowIfNullOrWhiteSpace(
                    argument: operationName,
                    paramName: nameof(operationName));

                ArgumentException.ThrowIfNullOrWhiteSpace(
                    argument: query,
                    paramName: nameof(query));

                // Require a serializable variables object because every repeatable send rebuilds it.
                ArgumentNullException.ThrowIfNull(
                    argument: variables,
                    paramName: nameof(variables));
            }
        }

        // Exchanges configured credentials for a bearer token after the synchronized cache admits one caller.
        private static async Task<string> NewAccessTokenAsync(
            XrayGraphQlClient instance,
            CancellationToken cancellationToken)
        {
            // Build a fresh credential request per attempt because HttpRequestMessage cannot be sent twice.
            var response = await SendRepeatableAsync(
                instance,
                requestFactory: () => NewAuthenticationRequest(instance),
                operationName: "Authenticate",
                cancellationToken).ConfigureAwait(false);

            using (response)
            {
                // Retain the response body for safe diagnostics when authentication is rejected.
                var responseBody = await response.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var message = "Xray authentication failed.";
                    throw new XpanditClientException(
                        message,
                        statusCode: response.StatusCode,
                        responseBody);
                }

                try
                {
                    // Accept Xray's documented JSON string and compatible token-object responses from regional gateways.
                    using var document = JsonDocument.Parse(responseBody);
                    var accessToken = GetAuthenticationToken(document.RootElement);

                    if (string.IsNullOrWhiteSpace(accessToken))
                    {
                        var message = "The authentication response did not contain an access token.";
                        throw new JsonException(message);
                    }

                    return accessToken;
                }
                catch (JsonException exception)
                {
                    // Add operation context without exposing the submitted client secret.
                    var message = "Xray authentication returned an invalid token response.";
                    throw new XpanditClientException(
                        message,
                        statusCode: response.StatusCode,
                        responseBody,
                        innerException: exception);
                }
            }
        }

        // Converts custom GraphQL error entries into stable public diagnostics without assuming extension schemas.
        private static List<XrayGraphQlError> GetGraphQlErrors(JsonElement rootElement)
        {
            if (!rootElement.TryGetProperty("errors", out var errorsElement) ||
                errorsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var errors = new List<XrayGraphQlError>();

            // Preserve every returned error so callers receive the complete GraphQL failure context.
            foreach (var errorElement in errorsElement.EnumerateArray())
            {
                var path = new List<string>();

                if (errorElement.TryGetProperty("path", out var pathElement) &&
                    pathElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pathPart in pathElement.EnumerateArray())
                    {
                        path.Add(pathPart.ToString());
                    }
                }

                var extensions = default(JsonElement);

                if (errorElement.TryGetProperty("extensions", out var extensionsElement))
                {
                    extensions = extensionsElement.Clone();
                }

                errors.Add(new XrayGraphQlError
                {
                    Extensions = extensions,
                    Message = GetOptionalString(errorElement, "message") ?? "Unknown GraphQL error.",
                    Path = path
                });
            }

            return errors;
        }

        // Reads either the documented string token or common object wrappers used by compatible gateways.
        private static string GetAuthenticationToken(JsonElement rootElement)
        {
            if (rootElement.ValueKind == JsonValueKind.String)
            {
                return rootElement.GetString();
            }

            if (rootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (rootElement.TryGetProperty("token", out var tokenElement))
            {
                return tokenElement.GetString();
            }

            return rootElement.TryGetProperty("access_token", out var accessTokenElement)
                ? accessTokenElement.GetString()
                : null;
        }

        // Returns a cached token when valid, otherwise serializes authentication so concurrent commands share one refresh.
        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var cachedToken = GetCachedAccessToken(instance: this);

            if (!string.IsNullOrWhiteSpace(cachedToken))
            {
                return cachedToken;
            }

            // Admit one authentication request at a time so concurrent commands do not create redundant tokens.
            await _authenticationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Recheck after waiting because another caller may have refreshed the shared token state.
                cachedToken = GetCachedAccessToken(instance: this);

                if (!string.IsNullOrWhiteSpace(cachedToken))
                {
                    return cachedToken;
                }

                var accessToken = await NewAccessTokenAsync(instance: this, cancellationToken).ConfigureAwait(false);
                var expiresAt = GetTokenExpiry(accessToken);

                // Publish token and expiry atomically so readers never observe mismatched authentication state.
                lock (_tokenStateLock)
                {
                    _accessToken = accessToken;
                    _accessTokenExpiresAt = expiresAt;
                }

                return accessToken;
            }
            finally
            {
                // Release the authentication gate for the next refresh even when the remote exchange fails.
                _authenticationLock.Release();
            }
        }

        // Reads token state under one lock and applies the refresh buffer before exposing it to a command.
        private static string GetCachedAccessToken(XrayGraphQlClient instance)
        {
            lock (instance._tokenStateLock)
            {
                var isCurrent = !string.IsNullOrWhiteSpace(instance._accessToken) &&
                    instance._accessTokenExpiresAt > DateTimeOffset.UtcNow.Add(TokenRefreshBuffer);

                return isCurrent ? instance._accessToken : null;
            }
        }

        // Extracts a string property while preserving null and non-string response values as absence.
        private static string GetOptionalString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var propertyElement) &&
                propertyElement.ValueKind == JsonValueKind.String
                ? propertyElement.GetString()
                : null;
        }

        // Parses JWT expiry without an identity package and falls back to a bounded cache lifetime for opaque tokens.
        private static DateTimeOffset GetTokenExpiry(string accessToken)
        {
            try
            {
                var segments = accessToken.Split('.');

                if (segments.Length <= JwtPayloadSegmentIndex)
                {
                    return DateTimeOffset.UtcNow.AddHours(DefaultTokenLifetimeHours);
                }

                var payload = segments[JwtPayloadSegmentIndex]
                    .Replace('-', '+')
                    .Replace('_', '/');
                var paddingLength = (4 - payload.Length % 4) % 4;
                payload = payload.PadRight(payload.Length + paddingLength, '=');

                // Decode only the unsigned payload metadata; the token remains validated by Xray on use.
                var payloadBytes = Convert.FromBase64String(payload);
                using var document = JsonDocument.Parse(payloadBytes);

                if (document.RootElement.TryGetProperty("exp", out var expiresElement) &&
                    expiresElement.TryGetInt64(out var expiresUnixSeconds))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(expiresUnixSeconds);
                }
            }
            catch (Exception)
            {
                // Treat malformed or opaque tokens as short-lived cache entries so authentication remains recoverable.
            }

            return DateTimeOffset.UtcNow.AddHours(DefaultTokenLifetimeHours);
        }

        // Removes a rejected token only when it still owns the shared cache slot.
        private static void ClearAccessToken(XrayGraphQlClient instance, string accessToken)
        {
            lock (instance._tokenStateLock)
            {
                if (!string.Equals(instance._accessToken, accessToken, StringComparison.Ordinal))
                {
                    return;
                }

                // Clear both values together so the next command enters the synchronized authentication path.
                instance._accessToken = null;
                instance._accessTokenExpiresAt = DateTimeOffset.MinValue;
            }
        }

        // Creates an authentication request with credentials confined to JSON content rather than shared headers.
        private static HttpRequestMessage NewAuthenticationRequest(XrayGraphQlClient instance)
        {
            return new HttpRequestMessage(HttpMethod.Post, instance._options.AuthenticationEndpoint)
            {
                Content = JsonContent.Create(
                    new
                    {
                        client_id = instance._options.ClientId,
                        client_secret = instance._options.ClientSecret
                    },
                    options: SerializerOptions)
            };
        }

        // Creates a single-use GraphQL request carrying the current bearer token and serialized variables.
        private static HttpRequestMessage NewGraphQlRequest(
            XrayGraphQlClient instance,
            string accessToken,
            string query,
            object variables)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, instance._options.GraphQlEndpoint)
            {
                Content = JsonContent.Create(
                    new
                    {
                        query,
                        variables
                    },
                    options: SerializerOptions)
            };

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return request;
        }

        // Parses a final GraphQL response and clones data before disposing the backing JSON document.
        private static async Task<JsonElement> ReadGraphQlDataAsync(
            HttpResponseMessage response,
            string operationName,
            CancellationToken cancellationToken)
        {
            var responseBody = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var message = $"Xray operation '{operationName}' " +
                    $"failed with HTTP {(int)response.StatusCode}.";
                throw new XpanditClientException(
                    message,
                    statusCode: response.StatusCode,
                    responseBody);
            }

            try
            {
                // Parse the complete envelope first because GraphQL can return HTTP 200 with command errors.
                using var document = JsonDocument.Parse(responseBody);
                var errors = GetGraphQlErrors(document.RootElement);

                if (errors.Count > 0)
                {
                    var message = $"Xray operation '{operationName}' returned GraphQL errors.";
                    throw new XpanditClientException(
                        message,
                        statusCode: response.StatusCode,
                        responseBody,
                        errors);
                }

                if (!document.RootElement.TryGetProperty("data", out var dataElement) ||
                    dataElement.ValueKind == JsonValueKind.Null)
                {
                    var message = "The GraphQL response did not contain a data object.";
                    throw new JsonException(message);
                }

                return dataElement.Clone();
            }
            catch (XpanditClientException)
            {
                // Preserve structured command failures without wrapping away their GraphQL diagnostics.
                throw;
            }
            catch (JsonException exception)
            {
                // Retain response context so schema drift can be diagnosed from the caller boundary.
                var message = $"Xray operation '{operationName}' returned invalid GraphQL JSON.";
                throw new XpanditClientException(
                    message,
                    statusCode: response.StatusCode,
                    responseBody,
                    innerException: exception);
            }
        }

        // Sends an authorized command through the common repeatable-send lifecycle.
        private static Task<HttpResponseMessage> SendAuthorizedAsync(
            XrayGraphQlClient instance,
            string accessToken,
            string operationName,
            string query,
            object variables,
            CancellationToken cancellationToken)
        {
            return SendRepeatableAsync(
                instance,
                requestFactory: () => NewGraphQlRequest(
                    instance,
                    accessToken,
                    query,
                    variables),
                operationName,
                cancellationToken);
        }

        // Repeats transient sends, respecting server delay guidance and preserving caller cancellation.
        private static async Task<HttpResponseMessage> SendRepeatableAsync(
            XrayGraphQlClient instance,
            Func<HttpRequestMessage> requestFactory,
            string operationName,
            CancellationToken cancellationToken)
        {
            Exception lastException = null;

            for (var attempt = 1; attempt <= instance._options.Retry.MaxAttempts; attempt++)
            {
                using var request = requestFactory.Invoke();

                try
                {
                    // Send a newly allocated request so every retry owns independent headers and content streams.
                    var response = await instance._httpClient
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false);
                    var hasAnotherAttempt = attempt < instance._options.Retry.MaxAttempts;
                    var isTransient = TestTransientStatus(response.StatusCode);

                    if (!isTransient || !hasAnotherAttempt)
                    {
                        return response;
                    }

                    var delay = GetRetryDelay(response, instance._options.Retry.Delay);

                    // Dispose the failed response before waiting so pooled connections remain available to retries.
                    response.Dispose();
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException exception)
                {
                    lastException = exception;

                    if (attempt >= instance._options.Retry.MaxAttempts)
                    {
                        break;
                    }

                    // Delay after a transport failure so the next attempt does not immediately amplify an outage.
                    await Task.Delay(instance._options.Retry.Delay, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    lastException = exception;

                    if (attempt >= instance._options.Retry.MaxAttempts)
                    {
                        break;
                    }

                    // Treat an HttpClient timeout as transient while preserving explicit caller cancellation above.
                    await Task.Delay(instance._options.Retry.Delay, cancellationToken).ConfigureAwait(false);
                }
            }

            var message = $"Xray operation '{operationName}' exhausted the repeatable-send policy.";
            throw new XpanditClientException(message, innerException: lastException);
        }

        // Selects server Retry-After guidance when available and falls back to configured client delay.
        private static TimeSpan GetRetryDelay(HttpResponseMessage response, TimeSpan fallbackDelay)
        {
            var retryAfter = response.Headers.RetryAfter;

            if (retryAfter?.Delta is TimeSpan delta && delta >= TimeSpan.Zero)
            {
                return delta;
            }

            if (retryAfter?.Date is DateTimeOffset retryDate)
            {
                var dateDelay = retryDate - DateTimeOffset.UtcNow;
                return dateDelay > TimeSpan.Zero ? dateDelay : TimeSpan.Zero;
            }

            return fallbackDelay;
        }

        // Identifies protocol responses that can succeed when repeated without changing caller intent.
        private static bool TestTransientStatus(HttpStatusCode statusCode)
        {
            var statusCodeValue = (int)statusCode;
            return statusCode == HttpStatusCode.RequestTimeout ||
                statusCode == HttpStatusCode.TooManyRequests ||
                statusCodeValue >= 500;
        }

        // Verifies endpoints, credentials, and retry bounds before the transport owns any authentication state.
        private static void ConfirmOptions(XrayClientOptions options)
        {
            AssertRequiredOptions(options);

            if (!options.AuthenticationEndpoint.IsAbsoluteUri)
            {
                var message = "XrayClientOptions.AuthenticationEndpoint must be absolute.";
                throw new ArgumentException(message, nameof(options));
            }

            if (!options.GraphQlEndpoint.IsAbsoluteUri)
            {
                var message = "XrayClientOptions.GraphQlEndpoint must be absolute.";
                throw new ArgumentException(message, nameof(options));
            }

            if (options.Retry.MaxAttempts < 1)
            {
                var message = "XrayClientOptions.Retry.MaxAttempts must be at least one.";
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    message);
            }

            if (options.Retry.Delay < TimeSpan.Zero)
            {
                var message = "XrayClientOptions.Retry.Delay cannot be negative.";
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    message);
            }

            return;

            // Validates required nested option values against the owning public parameter contract.
            // Nested member failures name options to satisfy CA2208 while their messages identify the member.
            static void AssertRequiredOptions(XrayClientOptions options)
            {
                // Require both credential components before any authentication request can be allocated.
                if (string.IsNullOrWhiteSpace(options.ClientId))
                {
                    var message = "XrayClientOptions.ClientId cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(options));
                }

                if (string.IsNullOrWhiteSpace(options.ClientSecret))
                {
                    var message = "XrayClientOptions.ClientSecret cannot be null or whitespace.";
                    throw new ArgumentException(message, nameof(options));
                }

                // Require both public endpoints before testing their absolute URI shape.
                if (options.AuthenticationEndpoint is null)
                {
                    var message = "XrayClientOptions.AuthenticationEndpoint cannot be null.";
                    throw new ArgumentException(message, nameof(options));
                }

                if (options.GraphQlEndpoint is null)
                {
                    var message = "XrayClientOptions.GraphQlEndpoint cannot be null.";
                    throw new ArgumentException(message, nameof(options));
                }

                // Require retry configuration because authentication and GraphQL sends share its lifecycle.
                if (options.Retry is null)
                {
                    var message = "XrayClientOptions.Retry cannot be null.";
                    throw new ArgumentException(message, nameof(options));
                }
            }
        }
        #endregion
    }
}
