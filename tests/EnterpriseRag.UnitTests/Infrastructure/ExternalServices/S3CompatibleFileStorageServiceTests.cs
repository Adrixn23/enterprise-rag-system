using System.Net;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.ExternalServices.FileStorage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Infrastructure.ExternalServices;

public class S3CompatibleFileStorageServiceTests
{
    [Fact]
    public async Task SaveGetDelete_ShouldSignRequestsWithAwsSigV4_AndUseTenantObjectKey()
    {
        var capturedRequests = new List<(HttpMethod Method, string Uri, string? AuthHeader)>();
        var storedPayload = Array.Empty<byte>();

        var handler = new StubHandler(async (req, ct) =>
        {
            var auth = req.Headers.TryGetValues("Authorization", out var vals) ? string.Join(",", vals) : null;
            capturedRequests.Add((req.Method, req.RequestUri!.ToString(), auth));

            if (req.Method == HttpMethod.Put)
            {
                storedPayload = await req.Content!.ReadAsByteArrayAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            if (req.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(storedPayload)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var client = new HttpClient(handler);
        var settings = Options.Create(new FileStorageSettings
        {
            Provider = "S3",
            ServiceUrl = "http://minio:9000",
            BucketName = "rag-docs",
            AccessKey = "minioadmin",
            SecretKey = "miniosecret",
            Region = "us-east-1"
        });

        var sut = new S3CompatibleFileStorageService(client, settings, new Mock<ILogger<S3CompatibleFileStorageService>>().Object);
        var tenantId = Guid.NewGuid();

        await using var input = new MemoryStream("s3-distributed-content"u8.ToArray());
        var key = await sut.SaveFileAsync(input, "policy.pdf", tenantId);

        key.Should().StartWith($"{tenantId:N}/");
        key.Should().EndWith("_policy.pdf");

        await using var stream = await sut.GetFileAsync(key);
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!);
        (await reader.ReadToEndAsync()).Should().Be("s3-distributed-content");

        var deleted = await sut.DeleteFileAsync(key);
        deleted.Should().BeTrue();

        capturedRequests.Should().HaveCount(3);
        capturedRequests.Should().OnlyContain(r => r.AuthHeader != null && r.AuthHeader.StartsWith("AWS4-HMAC-SHA256 "));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responder(request, cancellationToken);
    }
}
