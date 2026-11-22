using EnterpriseRag.Infrastructure.ExternalServices.FileStorage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Infrastructure.ExternalServices;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalFileStorageService _sut;

    public LocalFileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"rag-storage-test-{Guid.NewGuid():N}");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:BasePath"] = _tempDir
            })
            .Build();

        _sut = new LocalFileStorageService(config, new Mock<ILogger<LocalFileStorageService>>().Object);
    }

    [Fact]
    public async Task SaveGetAndDeleteAsync_ShouldWorkAsynchronouslyWithinTenantDirectory()
    {
        var tenantId = Guid.NewGuid();
        await using var input = new MemoryStream("contenido de prueba"u8.ToArray());

        var path = await _sut.SaveFileAsync(input, "../../evil-name.txt", tenantId);

        Path.GetFullPath(path).Should().StartWith(Path.GetFullPath(_tempDir));
        Path.GetFileName(path).Should().EndWith("_evil-name.txt");

        await using (var readStream = await _sut.GetFileAsync(path))
        {
            readStream.Should().NotBeNull();
            using var reader = new StreamReader(readStream!);
            var text = await reader.ReadToEndAsync();
            text.Should().Be("contenido de prueba");
        }

        var deleted = await _sut.DeleteFileAsync(path);
        deleted.Should().BeTrue();

        var deletedAgain = await _sut.DeleteFileAsync(path);
        deletedAgain.Should().BeFalse();
    }

    [Fact]
    public async Task GetAndDeleteAsync_WhenPathIsOutsideBaseDirectory_ShouldThrowUnauthorizedAccessException()
    {
        var outsidePath = Path.Combine(Path.GetTempPath(), "outside-secret.txt");

        Func<Task> getAct = () => _sut.GetFileAsync(outsidePath);
        Func<Task> delAct = () => _sut.DeleteFileAsync(outsidePath);

        await getAct.Should().ThrowAsync<UnauthorizedAccessException>();
        await delAct.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
