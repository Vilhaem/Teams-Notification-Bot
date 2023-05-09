using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using NotificationBot.Bot;
using NotificationBot.SpeechService;
using Swashbuckle.AspNetCore.Annotations;
using static NotificationBot.SpeechService.SpeechService;

namespace NotificationBot.Controllers
{
    [ApiController]
    [Route("call")]
    public class CallController : ControllerBase
    {
        private readonly ICallBot _bot;
        private readonly ILogger<CallController> _logger;
        private readonly IOptions<SpeechServiceOptions> _speechOptions;
        private readonly IOptions<BotOptions> _botoptions;

        public CallController(ICallBot bot, IOptions<SpeechServiceOptions> speechOptions, IOptions<BotOptions> botoptions, ILogger<CallController> logger)
        {
            _bot = bot;
            _speechOptions = speechOptions;
            _botoptions = botoptions;
            _logger = logger;
        }

        [HttpGet("BotInfo")]
        [SwaggerOperation(Summary = "Get App Settings and base URL")]
        public string GetBotOptions()
        {
            // Used for debugging the application settings
            return 
                $"AppId: {_botoptions.Value.AppId}\n" +
                $"AppSecret: {_botoptions.Value.AppSecret}\n" +
                $"BaseURL: {_botoptions.Value.BaseURL}\n" +
                $"DurationBeforeVoicemail: {_botoptions.Value.DurationBeforeVoicemail} seconds\n" +
                $"TuningDurationForCorrectVoicemail: {_botoptions.Value.TuningDurationForCorrectVoicemail} seconds\n" +
                $"Speech Key: {_speechOptions.Value.Key}\n" +
                $"Speech Endpoint: {_speechOptions.Value.Endpoint}\n" +
                $"Speech STSUri: {_speechOptions.Value.STSUri}\n";
        }


        [HttpPost("raise")]
        [SwaggerOperation(Summary = "Send a message to a user.")]
        [SwaggerResponse(200, "The message was sent successfully.")]
        [SwaggerResponse(400, "The request was invalid.")]
        public async Task<IActionResult> ControllerCallUserAsync([FromBody] MessageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text) || string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.TenantId))
            {
                return BadRequest("The text, userId and tenantId parameters are required.");
            }
            try
            {
                _logger.LogInformation("\n\n## Init Text-to-Speech service");

                // Get Token for Azure Speech Service First as "var token"
                var token = await FetchTokenAsync(STSUri: _speechOptions.Value.STSUri.ToString(), subscriptionKey: _speechOptions.Value.Key,logger:_logger);

                // Using "var token" and text from POST to generate text-to-speech file and return name of .wav file
                var objectId = await GenerateTextToSpeechAudioFile(text: request.Text, token: token, endPointUri: _speechOptions.Value.Endpoint, logger: _logger);

                _logger.LogInformation(
                    "\n\n## From CallController" +
                    "\n\n## objectId: {objectId}",
                    objectId);

                await _bot.CallUserAsync(userId:request.UserId, TenantId:request.TenantId, text:request.Text, filename:objectId);
            }
            catch (ServiceException ex)
            {
                _logger.LogError("\n\n## This is a ServiceException");
                if (ex.InnerException != null) 
                { return BadRequest(ex.InnerException.Message); }
                else
                { return BadRequest(ex.Message); }
                
            }
            catch (NullReferenceException ex)
            {
                _logger.LogError("\n\n## This is a NullReferenceException");
                if (ex.InnerException != null)
                { return BadRequest(ex.InnerException.Message); }
                else
                { return BadRequest(ex.Message); }
            }
            catch (Exception ex)
            {
                _logger.LogError("\n\n## This is a Exception of no particular type");
                if (ex.InnerException != null)
                { return BadRequest(ex.InnerException.Message); }
                else
                { return BadRequest(ex.Message); }
            }

            return Ok(
                $"Calling User '{_bot.UserName}'\n" +
                $"with UserId: {request.UserId}\n" +
                $"at TenantId: {request.TenantId}\n" +
                $"Successful");
        }
    }

    public class MessageRequest
    {
        [SwaggerSchema(Description = "The text of the message to be played as text-to-speech.")]
        public string? Text { get; set; }

        [SwaggerSchema(Description = "The ID of the user to send the message to.")]
        public string? UserId { get; set; }
        [SwaggerSchema(Description = "The TenantID of the User")]
        public string? TenantId { get; set; }
    }
}

