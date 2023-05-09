using Microsoft.AspNetCore.Mvc;
using NotificationBot.Bot;

namespace NotificationBot.Controllers
{
    [Route("/callback")]
    public class CallbackController : Controller
    {
        private readonly ILogger<CallbackController> _logger;
        private readonly ICallBot _bot;
        public CallbackController(ICallBot bot, ILogger<CallbackController> logger) 
        {
            _bot = bot;
            _logger = logger;
        }
        [HttpPost,HttpGet]
        public async Task HandleCallbackRequestAsync()
        {
            _logger.LogInformation("\n\n## Handling callback");
            await _bot.BotProcessNotificationAsync(Request, Response);
        }
    }
}
