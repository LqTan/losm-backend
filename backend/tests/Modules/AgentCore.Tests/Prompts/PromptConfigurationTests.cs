using AgentCore.Domain.Entities;
using AgentCore.Infrastructure.Persistence;
using AgentCore.Infrastructure.PromptsManagement;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentCore.Tests.Prompts;

public sealed class PromptConfigurationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Execute the plan")]
    [InlineData("Execute {approvedPlan} with {unknown}")]
    public void ApprovedPlanRejectsMissingOrUnknownVariables(string content) =>
        Assert.Throws<ArgumentException>(() => PromptCatalog.Validate(
            new("approved-plan-system", "System", "", ["approvedPlan"]), content));

    [Fact]
    public void AreaContextIsDiscoverableAndRequiresDynamicAreasVariable()
    {
        var definition = new PromptCatalog([]).Get("area-context-system");
        Assert.Equal("System", definition.Group);
        Assert.Equal(new[] { "areasDesc" }, definition.Variables);
        PromptCatalog.Validate(definition, definition.DefaultContent);
        Assert.Throws<ArgumentException>(() => PromptCatalog.Validate(definition, "Không có biến khu vực"));
        PromptCatalog.Validate(definition, "Khu vực hỗ trợ: {areasDesc}");
    }

    [Fact]
    public void ApprovedPlanAcceptsRequiredVariable() => PromptCatalog.Validate(
        new("approved-plan-system", "System", "", ["approvedPlan"]), "Execute {approvedPlan}");

    [Fact]
    public async Task CacheAvoidsRepeatedReadsAndInvalidationLoadsPublishedRevision()
    {
        var store = new Mock<IPromptStore>();
        store.Setup(s => s.GetAsync("agent-system", default)).ReturnsAsync(new PromptRevision { Content = "first" });
        var cache = new PromptCache(TimeProvider.System);
        var provider = new PromptProvider(store.Object, cache);
        Assert.Equal("first", await provider.ResolveAsync("agent-system", "default", default));
        Assert.Equal("first", await provider.ResolveAsync("agent-system", "default", default));
        store.Verify(s => s.GetAsync("agent-system", default), Times.Once);
        store.Setup(s => s.GetAsync("agent-system", default)).ReturnsAsync(new PromptRevision { Content = "second" });
        await cache.InvalidateAsync("agent-system");
        Assert.Equal("second", await provider.ResolveAsync("agent-system", "default", default));
    }

    [Fact]
    public async Task CacheExpiresForUpdatesFromOtherInstances()
    {
        var clock = new TestClock();
        var store = new Mock<IPromptStore>();
        store.SetupSequence(s => s.GetAsync("agent-system", default))
            .ReturnsAsync((PromptRevision?)null).ReturnsAsync(new PromptRevision { Content = "published" });
        var provider = new PromptProvider(store.Object, new PromptCache(clock));
        Assert.Equal("fallback", await provider.ResolveAsync("agent-system", "fallback", default));
        clock.Now += PromptCache.Lifetime;
        Assert.Equal("published", await provider.ResolveAsync("agent-system", "fallback", default));
    }

    [Fact]
    public async Task InvalidationWaitsForInFlightReadSoStaleValueCannotSurvivePublication()
    {
        var cache = new PromptCache(TimeProvider.System);
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource<PromptRevision?>();
        var read = cache.GetAsync("agent-system", () => { started.SetResult(); return release.Task; }, default);
        await started.Task;
        var invalidation = cache.InvalidateAsync("agent-system");
        release.SetResult(new PromptRevision { Content = "old" });
        await read;
        await invalidation;
        Assert.Equal("new", await cache.GetAsync("agent-system", () => Task.FromResult<PromptRevision?>(new() { Content = "new" }), default));
    }

    [Fact]
    public void MigrationSnapshotMatchesCurrentModel()
    {
        using var db = new AgentCoreDbContext(new DbContextOptionsBuilder<AgentCoreDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Contains("20261020000000_AddAgentPromptRevisions", db.Database.GetMigrations());
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
