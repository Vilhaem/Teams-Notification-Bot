using Microsoft.Bot.Builder;
//using Microsoft.Bot.Schema;
//using System.Collections.Concurrent;
// Planned Implementation of Bot that sends Message
namespace NotificationBot.Bot
{
    public class MessageBot : ActivityHandler, IBot
    {
        //        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;
        //        public MessageBot(ConcurrentDictionary<string, ConversationReference> conversationReferences)
        //        {
        //            _conversationReferences = conversationReferences;
        //        }

        //        private void AddConversationReference(Activity activity)
        //        {
        //            var conversationReference = activity.GetConversationReference();
        //            _conversationReferences.AddOrUpdate(conversationReference.User.Id, conversationReference, (key, newValue) => conversationReference);
        //        }

        //        protected override Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        //        {
        //            AddConversationReference(turnContext.Activity as Activity);

        //            return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
        //        }
    }
}
