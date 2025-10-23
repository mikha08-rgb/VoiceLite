using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceLite.Tests.Helpers
{
    /// <summary>
    /// Mock HTTP message handler for testing HTTP client calls
    /// Allows injecting custom responses for testing
    /// </summary>
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        }

        /// <summary>
        /// Synchronous version for simpler test setup
        /// </summary>
        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            if (responseFactory == null) throw new ArgumentNullException(nameof(responseFactory));
            _responseFactory = req => Task.FromResult(responseFactory(req));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _responseFactory(request);
        }
    }
}
