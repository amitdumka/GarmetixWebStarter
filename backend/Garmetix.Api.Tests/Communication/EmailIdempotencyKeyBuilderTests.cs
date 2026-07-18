using Garmetix.Api.Communication;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

public sealed class EmailIdempotencyKeyBuilderTests
{
    [Fact]
    public void Build_IsDeterministic_ForIdenticalInputs()
    {
        var companyId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        var first = EmailIdempotencyKeyBuilder.Build(companyId, "Sales", "Invoice", sourceId, "sale-invoice", "Customer@Example.com", 1);
        var second = EmailIdempotencyKeyBuilder.Build(companyId, "Sales", "Invoice", sourceId, "sale-invoice", "customer@example.com ", 1);

        // Case/whitespace differences in the recipient address must not create a duplicate send.
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("Sales", "Invoice", "sale-invoice", "a@example.com", 1, "Sales", "Invoice", "sale-invoice", "a@example.com", 2)] // revision bump
    [InlineData("Sales", "Invoice", "sale-invoice", "a@example.com", 1, "Sales", "Invoice", "sale-invoice", "b@example.com", 1)] // different recipient
    [InlineData("Sales", "Invoice", "sale-invoice", "a@example.com", 1, "Purchase", "Invoice", "sale-invoice", "a@example.com", 1)] // different module
    public void Build_DiffersWhenAnyComponentChanges(
        string moduleA, string typeA, string templateA, string recipientA, int revisionA,
        string moduleB, string typeB, string templateB, string recipientB, int revisionB)
    {
        var companyId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        var keyA = EmailIdempotencyKeyBuilder.Build(companyId, moduleA, typeA, sourceId, templateA, recipientA, revisionA);
        var keyB = EmailIdempotencyKeyBuilder.Build(companyId, moduleB, typeB, sourceId, templateB, recipientB, revisionB);

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Build_DiffersBetweenNullAndConcreteCompanyId()
    {
        var sourceId = Guid.NewGuid();
        var globalKey = EmailIdempotencyKeyBuilder.Build(null, "Administration", "Invitation", sourceId, "invitation", "user@example.com", 1);
        var scopedKey = EmailIdempotencyKeyBuilder.Build(Guid.NewGuid(), "Administration", "Invitation", sourceId, "invitation", "user@example.com", 1);

        Assert.NotEqual(globalKey, scopedKey);
    }

    [Fact]
    public void Build_ProducesA64CharacterLowercaseHexString()
    {
        var key = EmailIdempotencyKeyBuilder.Build(Guid.NewGuid(), "HR", "Payslip", Guid.NewGuid(), "payslip", "employee@example.com", 3);

        Assert.Equal(64, key.Length);
        Assert.Equal(key, key.ToLowerInvariant());
        Assert.True(key.All(Uri.IsHexDigit));
    }
}
