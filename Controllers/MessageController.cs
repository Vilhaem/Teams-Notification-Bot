//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Bot.Builder.Integration.AspNet.Core;
//using Microsoft.Bot.Schema;
//using System.Collections.Concurrent;
//using WebApplicationForBot.Bot;
//using Microsoft.Extensions.Options;
//using Microsoft.Bot.Builder;
//using System.Net;

//namespace WebApplicationForBot.Controllers
//{
//    [Route("api/notify")]
//    [ApiController]
//    public class MessageController : ControllerBase
//    {
//        private readonly CloudAdapter _adapter;
//        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;
//        private readonly string _appId;

//        public MessageController(CloudAdapter adapter, ConcurrentDictionary<string, ConversationReference> conversationReferences, IOptions<BotOptions> options)
//        {
//            _adapter = adapter;
//            _conversationReferences = conversationReferences;
//            _appId = options.Value.AppId;
//        }

//        [HttpGet]
//        public async Task<IActionResult> Get()
//        {
//            foreach (var conversationReference in _conversationReferences.Values)
//            {
//                await _adapter.ContinueConversationAsync(_appId, conversationReference, BotCallback, default);
//            }

//            // Let the caller know proactive messages have been sent
//            return new ContentResult()
//            {
//                Content = "<html><body><h1>Proactive messages have been sent.</h1></body></html>",
//                ContentType = "text/html",
//                StatusCode = (int)HttpStatusCode.OK,
//            };
//        }

//        private async Task BotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
//        {
//            await turnContext.SendActivityAsync("proactive hello");
//        }
//    }
//}
