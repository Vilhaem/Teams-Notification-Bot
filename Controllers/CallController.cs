using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using NotificationBot.ApiKey;
using NotificationBot.Bot;
using NotificationBot.SpeechService;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

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
        private readonly IOptions<ApiKeyOption> _apiOptions;

        /// <summary>
        /// <see cref="CallController"/> Constructor<br></br>
        /// Inits <see cref="ICallBot"/> interface and various appsetting instances
        /// </summary>
        /// <param name="bot"></param>
        /// <param name="speechOptions"></param>
        /// <param name="botOptions"></param>
        /// <param name="apiOptions"></param>
        /// <param name="logger"></param>
        public CallController(ICallBot bot, IOptions<SpeechServiceOptions> speechOptions, IOptions<BotOptions> botOptions, IOptions<ApiKeyOption> apiOptions,ILogger<CallController> logger)
        {
            _bot = bot;
            _speechOptions = speechOptions;
            _botoptions = botOptions;
            _apiOptions = apiOptions;
            _logger = logger;
        }
        /// <summary>
        /// Gets App settings
        /// </summary>
        /// <returns>App settings</returns>
        /// <response code = "200">Returns App Settings</response>
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
                $"BotTeamsDisplayName: {_botoptions.Value.BotTeamsDisplayName}\n" +
                $"BotTeamsId: {_botoptions.Value.BotTeamsId}\n" +
                $"UsingHeaderAuth: {_botoptions.Value.UsingHeaderAuth}\n" +
                $"ApiKeyName: {_apiOptions.Value.ApiKeyName}\n" +
                $"ApiKeyValue: {_apiOptions.Value.ApiKeyValue}\n" +
                $"Speech Key: {_speechOptions.Value.Key}\n" +
                $"Speech Endpoint: {_speechOptions.Value.Endpoint}\n" +
                $"Speech STSUri: {_speechOptions.Value.STSUri}\n" +
                $"SpeechSpeedPercentage: {_speechOptions.Value.SpeechSpeedPercentage}";
        }


        /// <summary>
        /// Calls user via Teams and plays prompt
        /// </summary>
        /// <returns>Operation Status</returns>
        /// <param name="AuthenticationKey">Used for authentication (if toggled)</param>
        /// <param name="request">
        /// # Fill in the corresponding info in the request body
        /// ### <b>text</b> field will be used for text-to-speech prompt in call<br></br>
        /// <b>userId</b> field is the user to be called<br></br>
        /// <b>tenantId</b> field is the tenantId of the App<br></br>
        /// </param>
        /// <response code="200">Graph API call successful</response>
        /// <response code="400">See response body for error</response>
        [HttpPost("raise")]
        public async Task<IActionResult> ControllerCallUserAsync(
            [FromBody] TeamsMessageRequest request,
            [FromHeader] string? AuthenticationKey = null)
        {
            try
            {
                AuthenticateHeader(isUsingHeaderAuth: _botoptions.Value.UsingHeaderAuth, header: AuthenticationKey);
                _logger.LogInformation("\n\n## Init Text-to-Speech service");

                // Get Token for Azure Speech Service First as "var token"
                var token = await SpeechServices.FetchTokenAsync(STSUri: _speechOptions.Value.STSUri.ToString(), subscriptionKey: _speechOptions.Value.Key, logger:_logger);

                // Using "var token" and text from POST to generate text-to-speech file and return name of .wav file
                var objectId = await SpeechServices.GenerateTextToSpeechAudioFile(text: request.Text, token: token, endPointUri: _speechOptions.Value.Endpoint, logger: _logger);

                _logger.LogInformation(
                    "\n\n## From CallController.ControllerCallUserAsync" +
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
            catch (InvalidOperationException ex)
            {
                _logger.LogError("\n\n## This is a InvalidOperationException");
                return BadRequest(ex.Message);
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

        /// <summary>
        /// Calls user via phone number and plays prompt
        /// </summary>
        /// <returns>Operation Status</returns>
        /// <param name="AuthenticationKey">Used for authentication (if toggled)</param>
        /// <param name="request">
        /// # Fill in the corresponding info in the request body
        /// ### <b>text</b> field will be used for text-to-speech prompt in call<br></br>
        /// <b>phoneNumber</b> field is the phone number to be called<br></br>
        /// <b>tenantId</b> field is the tenantId of the App<br></br>
        /// </param>
        /// <response code="200">Graph API call successful</response>
        /// <response code="400">See response body for error</response>
        [HttpPost("pstn")]
        public async Task<IActionResult> ControllerCallPSTNAsync(
            [FromBody] PSTNMessageRequest request,
            [FromHeader] string? AuthenticationKey = null)
        {
            try
            {
                AuthenticateHeader(isUsingHeaderAuth: _botoptions.Value.UsingHeaderAuth, header: AuthenticationKey);
                _logger.LogInformation("\n\n## Init Text-to-Speech service");

                // Get Token for Azure Speech Service First as "var token"
                var token = await SpeechServices.FetchTokenAsync(STSUri: _speechOptions.Value.STSUri.ToString(), subscriptionKey: _speechOptions.Value.Key, logger: _logger);

                // Using "var token" and text from POST to generate text-to-speech file and return name of .wav file
                var objectId = await SpeechServices.GenerateTextToSpeechAudioFile(text: request.Text, token: token, endPointUri: _speechOptions.Value.Endpoint, logger: _logger);

                _logger.LogInformation(
                    "\n\n## From CallController.ControllerCallPSTNAsync" +
                    "\n\n## objectId: {objectId}",
                    objectId);

                await _bot.CallPSTNAsync(userId: request.PhoneNumber, TenantId: request.TenantId, text: request.Text, filename: objectId);
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
            catch(InvalidOperationException ex)
            {
                _logger.LogError("\n\n## This is a InvalidOperationException");
                return BadRequest(ex.Message);
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
                $"Calling number '{request.PhoneNumber}'\n" +
                $"with TenantId: {request.TenantId}\n" +
                $"Successful");
        }
        private void AuthenticateHeader(bool isUsingHeaderAuth,string header)
        {
            if (isUsingHeaderAuth)
            {
                _logger.LogInformation("\n\n## Authentication Request ##");
                _logger.LogInformation("\n\n## Header entered: {apiKey} ##", header);
                if (header != _apiOptions.Value.ApiKeyValue)
                {
                    _logger.LogError("\n\n## Authentication Failed: Header missing or incorrect ##");
                    throw new InvalidOperationException("Authentication Failed: Header missing or incorrect");
                }
                _logger.LogInformation("\n\n## Authentication Successful ##");
            }
            else
            {
                _logger.LogInformation("\n\n## Not using header authentication ##");
            }
        }
    }
    /// <summary>
    /// Class of Teams call api request body
    /// </summary>
    public class TeamsMessageRequest
    {
        /// <summary>
        /// The text of the message to be played as text-to-speech.
        /// </summary>
        [Required]
        public string? Text { get; set; }
        /// <summary>
        /// The ID of the user to call
        /// </summary>
        [Required]
        public string? UserId { get; set; }
        /// <summary>
        /// The TenantID of the AAD
        /// </summary>
        [Required]
        public string? TenantId { get; set; }
    }
    /// <summary>
    /// Class of phone call api request body
    /// </summary>
    public class PSTNMessageRequest
    {
        /// <summary>
        /// The text of the message to be played as text-to-speech.
        /// </summary>
        [Required]
        public string? Text { get; set; }
        /// <summary>
        /// The phone number to call
        /// </summary>
        [Required]
        public string? PhoneNumber { get; set; }
        /// <summary>
        /// The TenantID of the AAD
        /// </summary>
        [Required]
        public string? TenantId { get; set; }
    }
}

