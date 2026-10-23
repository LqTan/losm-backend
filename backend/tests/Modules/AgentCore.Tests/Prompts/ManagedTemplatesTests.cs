using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using AgentCore.Infrastructure.PromptsManagement;
using AgentCore.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace AgentCore.Tests.Prompts;

public sealed class ManagedTemplatesTests
{
    [Fact]
    public void CatalogDefaultsHaveExactlyTheirDeclaredVariablesAndUniqueKeys()
    {
        var definitions = new PromptCatalog([]).GetAll();
        Assert.Equal(definitions.Count, definitions.Select(d => d.Key).Distinct().Count());
        foreach (var definition in definitions)
        {
            PromptCatalog.Validate(definition, definition.DefaultContent);
            var rendered = ManagedTemplates.Render(definition, definition.DefaultContent,
                definition.Variables.Select(v => (v, (object?)"sample")).ToArray());
            foreach (var variable in definition.Variables) Assert.DoesNotContain("{" + variable + "}", rendered);
        }
        Assert.Contains(definitions, d => d.Group == "Meeting");
        Assert.Contains(definitions, d => d.Group == "Messages");
        Assert.Contains(definitions, d => d.Group == "Email");
    }

    [Fact]
    public void RenderingDoesNotInterpretBracesInsideUserData()
    {
        var definition = new PromptDefinition("test", "Messages", "{name}: {message}", ["name", "message"]);
        Assert.Equal("{message}: done", ManagedTemplates.Render(definition, definition.DefaultContent,
            ("name", "{message}"), ("message", "done")));
    }

    [Fact]
    public void HtmlVariablesAreEncodedButPlainTextIsPreserved()
    {
        var html = new PromptDefinition("test.html", "Email", "<p>{name}</p>", ["name"]);
        Assert.Equal("<p>&lt;script&gt;&amp;&quot;</p>", ManagedTemplates.Render(html, html.DefaultContent, ("name", "<script>&\"")));
        var plain = html with { Key = "test.text" };
        Assert.Equal("<p><script>&\"</p>", ManagedTemplates.Render(plain, plain.DefaultContent, ("name", "<script>&\"")));
    }

    [Fact]
    public async Task PublishedMessageOverridesDefaultWithoutCallingModel()
    {
        var store = new Mock<IPromptStore>();
        store.Setup(s => s.GetAsync("message.agent.empty", default))
            .ReturnsAsync(new PromptRevision { Content = "Please choose another location." });
        var provider = new PromptProvider(store.Object, new PromptCache(TimeProvider.System));
        Assert.Equal("Please choose another location.",
            await ManagedTemplates.RenderAsync(provider, "message.agent.empty", default));
    }

    [Fact]
    public async Task PreviewUsesSameHtmlEncodingAsDelivery()
    {
        var controller = new PromptsAdminController(new PromptCatalog([]), Mock.Of<IPromptStore>(), new PromptCache(TimeProvider.System));
        var definition = ManagedTemplates.All.Single(d => d.Key == "email.friend.html");
        var values = definition.Variables.ToDictionary(v => v, _ => "<b>unsafe</b>");
        var preview = Assert.IsType<OkObjectResult>(controller.Preview(definition.Key, new(definition.DefaultContent, values)));
        var json = JsonSerializer.SerializeToElement(preview.Value);
        var delivered = await ManagedTemplates.RenderAsync(null, definition.Key, default, values.Select(v => (v.Key, (object?)v.Value)).ToArray());
        Assert.Equal(delivered, json.GetProperty("content").GetString());
        Assert.DoesNotContain("<b>unsafe</b>", delivered);
    }

    [Fact]
    public void PreviewWithoutExplicitVariablesUsesSampleVariablesOrFallback()
    {
        var controller = new PromptsAdminController(new PromptCatalog([]), Mock.Of<IPromptStore>(), new PromptCache(TimeProvider.System));
        var definition = ManagedTemplates.All.Single(d => d.Key == "email.schedule.calendar");
        var preview = Assert.IsType<OkObjectResult>(controller.Preview(definition.Key, new(definition.DefaultContent)));
        var json = JsonSerializer.SerializeToElement(preview.Value);
        var content = json.GetProperty("content").GetString();
        Assert.Contains("Highlands Coffee Hàm Nghi", content);
        Assert.Contains("123 Hàm Nghi, Quận 1", content);
    }

    [Fact]
    public async Task MeetingValidationReturnsEnglishMessageAndDoesNotSendEmail()
    {
        var sender = new Mock<IEmailSender>();
        var controller = new MeetingsController(sender.Object,
            Mock.Of<AgentCore.Application.Abstractions.IPendingActionStore>(), NullLogger<MeetingsController>.Instance);
        var request = new ScheduleMeetingRequest("", "Cafe", null, null, null, DateTime.UtcNow, 60, ["test@example.com"], null, null);
        var response = Assert.IsType<BadRequestObjectResult>(await controller.Schedule(request, default));
        Assert.Equal("Meeting title is required.", JsonSerializer.SerializeToElement(response.Value).GetProperty("message").GetString());
        sender.Verify(s => s.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MeetingEmailUsesPublishedTemplateAndPreservesUserNames()
    {
        var store = new Mock<IPromptStore>();
        store.Setup(s => s.GetAsync("email.schedule.subject", default))
            .ReturnsAsync(new PromptRevision { Content = "Invite {title} at {place}" });
        var sender = new Mock<IEmailSender>();
        EmailRequest? sent = null;
        sender.Setup(s => s.SendAsync(It.IsAny<EmailRequest>(), default))
            .Callback<EmailRequest, CancellationToken>((r, _) => sent = r).ReturnsAsync(new EmailResult(true, null));
        var controller = new MeetingsController(sender.Object,
            Mock.Of<AgentCore.Application.Abstractions.IPendingActionStore>(), NullLogger<MeetingsController>.Instance,
            prompts: new PromptProvider(store.Object, new PromptCache(TimeProvider.System)));
        var request = new ScheduleMeetingRequest("Gặp bạn <script>", "Cà phê", null, null, null, DateTime.UtcNow, 60, ["test@example.com"], null, null);
        Assert.IsType<OkObjectResult>(await controller.Schedule(request, default));
        Assert.NotNull(sent);
        Assert.Equal("Invite Gặp bạn <script> at Cà phê", sent.Subject);
        Assert.DoesNotContain("<script>", sent.HtmlBody);
        Assert.Contains("Gặp bạn", sent.PlainTextBody);
        Assert.NotEmpty(sent.Attachments!);
    }
}
