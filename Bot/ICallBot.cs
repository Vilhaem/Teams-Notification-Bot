using Microsoft.Bot.Builder;
using Microsoft.Graph.Communications.Core.Notifications;

namespace NotificationBot.Bot
{
    public interface ICallBot : IBot
    {

        string UserName { get; }

        Task BotProcessNotificationAsync(HttpRequest request, HttpResponse response);
        /// <summary>
        /// Calls phone number via GraphAPI
        /// </summary>
        Task CallPSTNAsync(string userId, string TenantId, string text, Guid filename);
        /// <summary>
        /// Calls Teams user via GraphAPI
        /// </summary>
        Task CallUserAsync(string userId, string TenantId, string text, Guid filename);
        Task NotificationProcessorOnReceivedAsync(NotificationEventArgs args);


    }
}