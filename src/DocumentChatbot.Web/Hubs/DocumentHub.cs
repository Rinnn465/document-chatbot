using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DocumentChatbot.Web.Hubs;

[Authorize]
public sealed class DocumentHub : Hub
{
    public const int Prn222CourseId = 1;

    public static string CourseGroup(int courseId) => $"course:{courseId}";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Student") == true ||
            Context.User?.IsInRole("SubjectLeader") == true)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                CourseGroup(Prn222CourseId));
        }

        await base.OnConnectedAsync();
    }
}
