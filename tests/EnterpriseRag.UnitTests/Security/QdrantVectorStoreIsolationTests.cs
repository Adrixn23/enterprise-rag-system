using EnterpriseRag.Core.Domain.Common.Errors.VectorStore;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.VectorStore.VectorStoreService;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Qdrant.Client;
using Xunit;

namespace EnterpriseRag.UnitTests.Security;

public class QdrantVectorStoreIsolationTests
{
    private readonly QdrantSettings _settings = new()
    {
        Host = "localhost",
        Port = 6334,
        CollectionName = "enterprise-documents"
    };

    [Fact]
    public async Task SearchAsync_WhenTenantIdIsEmpty_ShouldReturnInvalidTenantId_WithoutCallingQdrant()
    {
        // Arrange
        var client = new QdrantClient("localhost", 6334);
        var sut = new QdrantVectorStoreService(client, Options.Create(_settings), new Mock<ILogger<QdrantVectorStoreService>>().Object);
        var vector = new ReadOnlyMemory<float>(new float[768]);

        // Act
        var result = await sut.SearchAsync(vector, Guid.Empty, 5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(VectorStoreErrors.InvalidTenantId);
    }

    [Fact]
    public async Task UpsertPointAsync_WhenTenantIdIsEmpty_ShouldReturnInvalidTenantId_WithoutCallingQdrant()
    {
        var client = new QdrantClient("localhost", 6334);
        var sut = new QdrantVectorStoreService(client, Options.Create(_settings), new Mock<ILogger<QdrantVectorStoreService>>().Object);
        var points = new[]
        {
            new EnterpriseRag.Core.Application.DTOs.Vector.VectorPointDto
            {
                PointId = Guid.NewGuid(),
                TenantId = Guid.Empty,
                DocumentId = Guid.NewGuid(),
                ChunkIndex = 0,
                Content = "test",
                Vector = new ReadOnlyMemory<float>(new float[768])
            }
        };

        var result = await sut.UpsertPointAsync(points);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(VectorStoreErrors.InvalidTenantId);
    }

    [Fact]
    public async Task UpsertPointAsync_WhenDocumentIdIsEmpty_ShouldReturnInvalidDocumentId_WithoutCallingQdrant()
    {
        var client = new QdrantClient("localhost", 6334);
        var sut = new QdrantVectorStoreService(client, Options.Create(_settings), new Mock<ILogger<QdrantVectorStoreService>>().Object);
        var points = new[]
        {
            new EnterpriseRag.Core.Application.DTOs.Vector.VectorPointDto
            {
                PointId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                DocumentId = Guid.Empty,
                ChunkIndex = 0,
                Content = "test",
                Vector = new ReadOnlyMemory<float>(new float[768])
            }
        };

        var result = await sut.UpsertPointAsync(points);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(VectorStoreErrors.InvalidDocumentId);
    }

    [Fact]
    public async Task DeleteByDocumentAsync_WhenTenantIdIsEmpty_ShouldReturnInvalidTenantId_WithoutCallingQdrant()
    {
        // Arrange
        var client = new QdrantClient("localhost", 6334);
        var sut = new QdrantVectorStoreService(client, Options.Create(_settings), new Mock<ILogger<QdrantVectorStoreService>>().Object);

        // Act
        var result = await sut.DeleteByDocumentAsync(Guid.NewGuid(), Guid.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(VectorStoreErrors.InvalidTenantId);
    }

    [Fact]
    public void BuildTenantSearchFilter_ShouldEnforceExactTenantIdKeywordCondition()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var filter = QdrantVectorStoreService.BuildTenantSearchFilter(tenantId);

        // Assert
        filter.Must.Should().ContainSingle();
        filter.Must[0].Field.Key.Should().Be("tenantId");
        filter.Must[0].Field.Match.Keyword.Should().Be(tenantId.ToString());
    }

    [Fact]
    public void BuildDocumentDeleteFilter_ShouldEnforceBothTenantIdAndDocumentIdConditions()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        // Act
        var filter = QdrantVectorStoreService.BuildDocumentDeleteFilter(documentId, tenantId);

        // Assert
        filter.Must.Should().HaveCount(2);
        filter.Must.Should().Contain(c => c.Field.Key == "tenantId" && c.Field.Match.Keyword == tenantId.ToString());
        filter.Must.Should().Contain(c => c.Field.Key == "documentId" && c.Field.Match.Keyword == documentId.ToString());
    }

    [Fact]
    public async Task Methods_WhenClientCancels_ShouldPropagateOperationCanceledException()
    {
        // Arrange
        var client = new QdrantClient("localhost", 6334);
        var sut = new QdrantVectorStoreService(client, Options.Create(_settings), new Mock<ILogger<QdrantVectorStoreService>>().Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var points = new[]
        {
            new EnterpriseRag.Core.Application.DTOs.Vector.VectorPointDto
            {
                PointId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                DocumentId = Guid.NewGuid(),
                ChunkIndex = 0,
                Content = "test",
                Vector = new ReadOnlyMemory<float>(new float[768])
            }
        };

        // Act & Assert
        Func<Task> upsertAct = () => sut.UpsertPointAsync(points, cts.Token);
        Func<Task> searchAct = () => sut.SearchAsync(new ReadOnlyMemory<float>(new float[768]), Guid.NewGuid(), 5, cts.Token);
        Func<Task> deleteAct = () => sut.DeleteByDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), cts.Token);

        await upsertAct.Should().ThrowAsync<OperationCanceledException>();
        await searchAct.Should().ThrowAsync<OperationCanceledException>();
        await deleteAct.Should().ThrowAsync<OperationCanceledException>();
    }
}
