using Microsoft.Bot.Builder;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Core.Notifications;
using System.Diagnostics;

namespace NotificationBot.Bot
{
    public interface ICallBot : IBot
    {

        string UserName { get; }

        Task BotProcessNotificationAsync(HttpRequest request, HttpResponse response);
        Task CallUserAsync(string userId, string TenantId, string text, Guid filename);
        Task NotificationProcessorOnReceivedAsync(NotificationEventArgs args);


    }
}