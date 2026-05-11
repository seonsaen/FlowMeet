using FlowMeet.Server.Controllers;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class MeetingControllerTests
{
    [Fact]
    public async Task CreateMeeting_WithoutUser_ReturnsUnauthorized()
    {
        var service = new FakeMeetingService();
        var controller = new MeetingController(service);
        ControllerTestHelper.SetUser(controller);

        var result = await controller.CreateMeeting(new CreateMeetingRequest());

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CreateMeeting_ServiceErrorMapsToBadRequest()
    {
        var service = new FakeMeetingService
        {
            CreateResult = (false, "Время встречи пересекается с занятостью")
        };
        var controller = new MeetingController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.CreateMeeting(new CreateMeetingRequest
        {
            Title = "Sync",
            StartTime = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc),
            ParticipantIds = new List<Guid> { Guid.NewGuid() }
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateMeeting_SuccessReturnsOkMessage()
    {
        var userId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();
        var service = new FakeMeetingService
        {
            UpdateResult = (true, string.Empty)
        };
        var controller = new MeetingController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var request = new UpdateMeetingRequest
        {
            Title = "Updated sync",
            StartTime = new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            ParticipantIds = new List<Guid> { Guid.NewGuid() }
        };

        var result = await controller.UpdateMeeting(meetingId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastUpdateUserId);
        Assert.Equal(meetingId, service.LastUpdateMeetingId);
        Assert.Equal("Встреча обновлена", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    [Fact]
    public async Task DeleteMeeting_ServiceErrorMapsToNotFound()
    {
        var service = new FakeMeetingService
        {
            DeleteResult = (false, "Встреча не найдена")
        };
        var controller = new MeetingController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.DeleteMeeting(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetIncomingInvites_ReturnsOkList()
    {
        var userId = Guid.NewGuid();
        var service = new FakeMeetingService
        {
            IncomingInvites = new List<IncomingInviteDto>
            {
                new()
                {
                    MeetingId = Guid.NewGuid(),
                    OrganizerName = "Ваня",
                    Title = "Приглашение",
                    StartTime = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc)
                }
            }
        };
        var controller = new MeetingController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetIncomingInvites();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var invites = Assert.IsType<List<IncomingInviteDto>>(ok.Value);
        Assert.Equal(userId, service.LastIncomingUserId);
        Assert.Single(invites);
    }

    [Fact]
    public async Task GetOutgoingInvites_ReturnsOkList()
    {
        var userId = Guid.NewGuid();
        var service = new FakeMeetingService
        {
            OutgoingInvites = new List<OutgoingInviteDto>
            {
                new()
                {
                    MeetingId = Guid.NewGuid(),
                    Title = "Outgoing",
                    StartTime = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc)
                }
            }
        };
        var controller = new MeetingController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetOutgoingInvites();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var invites = Assert.IsType<List<OutgoingInviteDto>>(ok.Value);
        Assert.Equal(userId, service.LastOutgoingUserId);
        Assert.Single(invites);
    }

    [Fact]
    public async Task GetMyMeetings_ReturnsOkList()
    {
        var userId = Guid.NewGuid();
        var service = new FakeMeetingService
        {
            Meetings = new List<MeetingOverviewDto>
            {
                new()
                {
                    MeetingId = Guid.NewGuid(),
                    Title = "Mine",
                    StartTime = new DateTime(2026, 5, 10, 14, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 5, 10, 15, 0, 0, DateTimeKind.Utc),
                    Status = MeetingStatus.Confirmed,
                    OrganizerId = userId,
                    OrganizerName = "Me"
                }
            }
        };
        var controller = new MeetingController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetMyMeetings();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var meetings = Assert.IsType<List<MeetingOverviewDto>>(ok.Value);
        Assert.Equal(userId, service.LastMeetingsUserId);
        Assert.Single(meetings);
    }

    [Fact]
    public async Task GetMeetingHistory_ReturnsOkList()
    {
        var userId = Guid.NewGuid();
        var service = new FakeMeetingService
        {
            MeetingHistory = new List<MeetingOverviewDto>
            {
                new()
                {
                    MeetingId = Guid.NewGuid(),
                    Title = "Past",
                    StartTime = new DateTime(2026, 4, 10, 14, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 4, 10, 15, 0, 0, DateTimeKind.Utc),
                    Status = MeetingStatus.Confirmed,
                    OrganizerId = userId,
                    OrganizerName = "Me"
                }
            }
        };
        var controller = new MeetingController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetMeetingHistory();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var meetings = Assert.IsType<List<MeetingOverviewDto>>(ok.Value);
        Assert.Equal(userId, service.LastHistoryUserId);
        Assert.Single(meetings);
    }


    [Fact]
    public async Task RespondToInvite_ReturnsAcceptedMessage()
    {
        var userId = Guid.NewGuid();
        var request = new RespondToInviteRequest
        {
            MeetingId = Guid.NewGuid(),
            IsAccepted = true
        };
        var service = new FakeMeetingService
        {
            RespondResult = (true, string.Empty)
        };
        var controller = new MeetingController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.RespondToInvite(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastRespondUserId);
        Assert.Same(request, service.LastRespondRequest);
        Assert.Equal("Приглашение принято", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    private sealed class FakeMeetingService : IMeetingService
    {
        public (bool IsSuccess, string ErrorMessage) CreateResult { get; set; } = (true, string.Empty);
        public (bool IsSuccess, string ErrorMessage) UpdateResult { get; set; } = (true, string.Empty);
        public (bool IsSuccess, string ErrorMessage) DeleteResult { get; set; } = (true, string.Empty);
        public List<IncomingInviteDto> IncomingInvites { get; set; } = new();
        public List<OutgoingInviteDto> OutgoingInvites { get; set; } = new();
        public List<MeetingOverviewDto> Meetings { get; set; } = new();
        public List<MeetingOverviewDto> MeetingHistory { get; set; } = new();
        public (bool IsSuccess, string ErrorMessage) RespondResult { get; set; } = (true, string.Empty);
        public Guid LastUpdateUserId { get; private set; }
        public Guid LastUpdateMeetingId { get; private set; }
        public Guid LastIncomingUserId { get; private set; }
        public Guid LastOutgoingUserId { get; private set; }
        public Guid LastMeetingsUserId { get; private set; }
        public Guid LastHistoryUserId { get; private set; }
        public Guid LastRespondUserId { get; private set; }
        public RespondToInviteRequest? LastRespondRequest { get; private set; }

        public Task<(bool IsSuccess, string ErrorMessage)> CreateMeetingAsync(Guid userId, CreateMeetingRequest request)
            => Task.FromResult(CreateResult);

        public Task<(bool IsSuccess, string ErrorMessage)> UpdateMeetingAsync(Guid userId, Guid meetingId, UpdateMeetingRequest request)
        {
            LastUpdateUserId = userId;
            LastUpdateMeetingId = meetingId;
            return Task.FromResult(UpdateResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> DeleteMeetingAsync(Guid userId, Guid meetingId)
            => Task.FromResult(DeleteResult);

        public Task<List<IncomingInviteDto>> GetIncomingInvitesAsync(Guid userId)
        {
            LastIncomingUserId = userId;
            return Task.FromResult(IncomingInvites);
        }

        public Task<List<OutgoingInviteDto>> GetOutgoingInvitesAsync(Guid userId)
        {
            LastOutgoingUserId = userId;
            return Task.FromResult(OutgoingInvites);
        }

        public Task<List<MeetingOverviewDto>> GetMyMeetingsAsync(Guid userId)
        {
            LastMeetingsUserId = userId;
            return Task.FromResult(Meetings);
        }

        public Task<List<MeetingOverviewDto>> GetMeetingHistoryAsync(Guid userId)
        {
            LastHistoryUserId = userId;
            return Task.FromResult(MeetingHistory);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> RespondToInviteAsync(Guid userId, RespondToInviteRequest request)
        {
            LastRespondUserId = userId;
            LastRespondRequest = request;
            return Task.FromResult(RespondResult);
        }
    }
}
