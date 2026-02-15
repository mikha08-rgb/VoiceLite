using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Moq;
using Moq.Protected;
using Xunit;
using VoiceLite.Infrastructure.Resilience;
using VoiceLite.Services;

namespace VoiceLite.Tests.Resilience
{
    /// <summary>
    /// PHASE 3 - DAY 1: Tests for Polly retry policies
    ///
    /// Purpose: Verify that retry policies handle transient failures correctly
    /// Coverage: HTTP retries, exponential backoff, cache fallback
    /// </summary>
    public class RetryPolicyTests
    {
        [Fact]
        public async Task HttpRetryPolicy_TransientFailure_RetriesSuccessfully()
        {
            // Arrange: Mock HTTP handler that fails twice then succeeds
            var callCount = 0;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount <= 2)
                    {
                        // First 2 attempts: return 503 Service Unavailable (transient error)
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                        {
                            Content = new StringContent("Service temporarily unavailable")
                        };
                    }
                    else
                    {
                        // 3rd attempt: succeed
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"valid\": true, \"tier\": \"pro\"}")
                        };
                    }
                });

            var httpClient = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("https://voicelite.app")
            };

            // Act: Execute request with retry policy
            var response = await RetryPolicies.HttpRetryPolicy.ExecuteAsync(async () =>
                await httpClient.GetAsync("/api/licenses/validate"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            callCount.Should().Be(3, "should retry twice on 503 errors before succeeding");
        }

        [Fact]
        public async Task HttpRetryPolicy_PermanentFailure_FailsAfter3Retries()
        {
            // Arrange: Mock HTTP handler that always returns 500 Internal Server Error
            var callCount = 0;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("Server error")
                    };
                });

            var httpClient = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("https://voicelite.app")
            };

            // Act: Execute request with retry policy
            var response = await RetryPolicies.HttpRetryPolicy.ExecuteAsync(async () =>
                await httpClient.GetAsync("/api/licenses/validate"));

            // Assert: Should fail after 3 retries (1 initial + 3 retries = 4 total attempts)
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            callCount.Should().Be(4, "should make 1 initial attempt + 3 retries = 4 total");
        }

        [Fact]
        public async Task HttpRetryPolicy_ClientError_DoesNotRetry()
        {
            // Arrange: Mock HTTP handler that returns 404 Not Found (client error)
            var callCount = 0;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = new StringContent("Not found")
                    };
                });

            var httpClient = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("https://voicelite.app")
            };

            // Act: Execute request with retry policy
            var response = await RetryPolicies.HttpRetryPolicy.ExecuteAsync(async () =>
                await httpClient.GetAsync("/api/licenses/validate"));

            // Assert: Should NOT retry on 4xx client errors
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            callCount.Should().Be(1, "should not retry on 4xx client errors");
        }

        [Fact]
        public async Task HttpRetryPolicy_FirstSuccess_DoesNotRetry()
        {
            // Arrange: Mock HTTP handler that succeeds immediately
            var callCount = 0;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"valid\": true}")
                    };
                });

            var httpClient = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("https://voicelite.app")
            };

            // Act: Execute request with retry policy
            var response = await RetryPolicies.HttpRetryPolicy.ExecuteAsync(async () =>
                await httpClient.GetAsync("/api/licenses/validate"));

            // Assert: Should succeed on first attempt without retries
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            callCount.Should().Be(1, "should not retry when first attempt succeeds");
        }

        [Fact]
        public async Task HttpRetryPolicy_NetworkException_RetriesSuccessfully()
        {
            // Arrange: Mock HTTP handler that throws exception twice then succeeds
            var callCount = 0;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount <= 2)
                    {
                        // First 2 attempts: throw network exception
                        throw new HttpRequestException("Network unreachable");
                    }
                    else
                    {
                        // 3rd attempt: succeed
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"valid\": true}")
                        };
                    }
                });

            var httpClient = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("https://voicelite.app")
            };

            // Act: Execute request with retry policy
            var response = await RetryPolicies.HttpRetryPolicy.ExecuteAsync(async () =>
                await httpClient.GetAsync("/api/licenses/validate"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            callCount.Should().Be(3, "should retry twice on HttpRequestException before succeeding");
        }

        [Fact]
        public async Task ProcessRetryPolicy_TransientFailure_RetriesSuccessfully()
        {
            // Arrange: Simulate process that fails once then succeeds
            var callCount = 0;
            Func<Task<string>> operation = () =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new System.ComponentModel.Win32Exception("Process failed to start");
                }
                return Task.FromResult("Success");
            };

            // Act: Execute with process retry policy
            var result = await RetryPolicies.ProcessRetryPolicy.ExecuteAsync(operation);

            // Assert
            result.Should().Be("Success");
            callCount.Should().Be(2, "should retry once on process failure");
        }

        [Fact]
        public async Task FileIORetryPolicy_TransientFailure_RetriesSuccessfully()
        {
            // Arrange: Simulate file operation that fails twice then succeeds
            var callCount = 0;
            Func<Task<string>> operation = () =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    throw new System.IO.IOException("File is locked by another process");
                }
                return Task.FromResult("File written successfully");
            };

            // Act: Execute with file I/O retry policy
            var result = await RetryPolicies.FileIORetryPolicy.ExecuteAsync(operation);

            // Assert
            result.Should().Be("File written successfully");
            callCount.Should().Be(3, "should retry twice on IOException before succeeding");
        }
    }
}
