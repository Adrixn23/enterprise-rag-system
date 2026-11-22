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
}
