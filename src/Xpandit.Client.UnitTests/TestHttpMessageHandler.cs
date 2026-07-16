using System.Net;
using System.Text;

namespace Xpandit.Client.UnitTests
{
    // Provides deterministic queued responses and immutable request snapshots for repository unit tests.
    internal sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        #region *** Fields       ***
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = [];
        #endregion

        #region *** Properties   ***
        // Exposes captured requests after content has been read, before HttpClient disposes their messages.
        internal IList<RecordedRequest> Requests { get; } = [];
        #endregion

        #region *** Methods      ***
        // Adds one custom response stage for cancellation or protocol behavior that static JSON cannot represent.
        internal void AddResponse(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            ArgumentNullException.ThrowIfNull(responseFactory);
            _responses.Enqueue(responseFactory);
        }

        // Adds one JSON response consumed by the next HttpClient send.
        internal void AddResponse(HttpStatusCode statusCode, string json)
        {
            _responses.Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }));
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Read content while the request is alive so later assertions observe the complete serialized command.
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest
            {
                Authorization = request.Headers.Authorization?.ToString(),
                Body = body,
                Method = request.Method,
                Uri = request.RequestUri
            });

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued HTTP response is available for the request.");
            }

            // Consume exactly one response so the queue models the full authentication and retry sequence.
            var responseFactory = _responses.Dequeue();
            return await responseFactory.Invoke(request, cancellationToken);
        }
        #endregion

        #region *** Nested Types ***
        // Retains only values used by assertions so disposed request messages never leak into test state.
        internal sealed class RecordedRequest
        {
            #region *** Properties   ***
            // Gets or sets the authorization header sent with the request, or null for authentication calls.
            internal string Authorization { get; set; }

            // Gets or sets the serialized request content captured before disposal.
            internal string Body { get; set; } = string.Empty;

            // Gets or sets the HTTP method used for the request.
            internal HttpMethod Method { get; set; } = HttpMethod.Get;

            // Gets or sets the absolute request destination, or null when the request omitted one.
            internal Uri Uri { get; set; }
            #endregion
        }
        #endregion
    }
}
