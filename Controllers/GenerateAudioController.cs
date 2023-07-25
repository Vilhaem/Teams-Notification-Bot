using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NotificationBot.ApiKey;
using NotificationBot.Bot;
using NotificationBot.SpeechService;
using NotificationBot.Swagger;
using System.ComponentModel.DataAnnotations;

namespace NotificationBot.Controllers
{
    [ApiController]
    [Route("generate")]
    public class GenerateAudioController : ControllerBase
    {
        private readonly IOptions<SpeechServiceOptions> _speechOptions;
        private readonly ILogger<GenerateAudioController> _logger;
        public GenerateAudioController(
            IOptions<SpeechServiceOptions> speechOptions,
            ILogger<GenerateAudioController> logger
            )
        {
            _speechOptions = speechOptions;
            _logger = logger;
        }
        /// <summary>
        /// Used for Generating utility audio files
        /// </summary>
        /// <param name="request">
        /// # Used for Generating Tone audio files
        /// ## toneSymbol please enter 0 ~ 9 or "#" or "*", others will return error.
        /// ## text is just the content of the audio clip
        /// </param>
        /// <returns></returns>
        [HttpPost("Tone")]
        public async Task<IActionResult> GenerateToneClips([FromBody] ToneAudioRequest request)
        {
            try
            {
                var token = await SpeechServices.FetchTokenAsync(STSUri: _speechOptions.Value.STSUri.ToString(), subscriptionKey: _speechOptions.Value.Key, logger: _logger);
                await SpeechServices.GenerateToneAudio(text: request.Text, token: token, toneSymbol: request.ToneSymbol, endPointUri: _speechOptions.Value.Endpoint, logger: _logger);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return BadRequest(ex.Message); }
            return Ok($"Generate Tone audio clips for tone: \"{request.ToneSymbol}\" \nSuccess");
        }

        /// <summary>
        /// Used for Generating utility audio files
        /// </summary>
        /// <param name="request">
        /// # Used for Generating utility audio files
        /// </param>
        /// <returns></returns>
        [HttpPost("Utility")]
        public async Task<IActionResult> GenerateUtilityAudio([FromHeader] UtilityClips utility, [FromBody] UtilityAudioRequest request)
        {
            try
            {
                var token = await SpeechServices.FetchTokenAsync(STSUri: _speechOptions.Value.STSUri.ToString(), subscriptionKey: _speechOptions.Value.Key, logger: _logger);
                await SpeechServices.GenerateToneUtliltyAudio(text: request.Text, token: token, filename: request.FileName, endPointUri: _speechOptions.Value.Endpoint, logger: _logger);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return BadRequest(ex.Message); }
            return Ok($"Generate Utility audio clips \"{request.FileName}\" \nSuccess");
        }
        [HttpGet("{test}")]
        public async Task<IActionResult> test([UtilityAudioClipsParameter("bruh", "10")] int test, [FromBody] UtilityAudioRequest request)
        {
            try
            {
                var token = await SpeechServices.FetchTokenAsync(STSUri: _speechOptions.Value.STSUri.ToString(), subscriptionKey: _speechOptions.Value.Key, logger: _logger);
                await SpeechServices.GenerateToneUtliltyAudio(text: request.Text, token: token, filename: request.FileName, endPointUri: _speechOptions.Value.Endpoint, logger: _logger);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex) { return BadRequest(ex.Message); }
            return Ok($"Generate Utility audio clips \"{request.FileName}\" \nSuccess");
        }
        public class UtilityAudioRequest
        {
            [Required]
            public string? Text { get; set; }
            [Required]
            public string FileName { get; set; }
        }
        public class ToneAudioRequest
        {
            [Required]
            public string? Text { get; set; }
            [Required]
            public string ToneSymbol { get; set; }
        }
        public enum UtilityClips
        {
            ToneList,
            NoFunction

        }
    }
}
