using AgentCore.Infrastructure.Email;
using AgentCore.Presentation.Controllers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AgentCore.Tests;

public class MeetingsControllerTests
{
    [Fact]
    public async Task ScheduleMeeting_ShouldSucceed()
    {
        var mockEmail = new Mock<IEmailSender>();
        mockEmail.Setup(x => x.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailResult(true, null));

        var mockStore = new Mock<AgentCore.Application.Abstractions.IPendingActionStore>();

        var controller = new MeetingsController(mockEmail.Object, mockStore.Object, NullLogger<MeetingsController>.Instance);

        var request = new ScheduleMeetingRequest(
            Title: "Hẹn gặp tại CAFE SHOP",
            PlaceName: "CAFE SHOP",
            Address: "Quận Phú Nhuận, VN",
            Latitude: 10.79563,
            Longitude: 106.68302,
            StartAt: DateTime.UtcNow,
            DurationMinutes: 60,
            AttendeeEmails: new List<string> { "host@example.com", "lequantan1974@gmail.com" },
            Note: null,
            HostName: "Tôi"
        );

        var response = await controller.Schedule(request, CancellationToken.None);
        Assert.NotNull(response);
    }
}
