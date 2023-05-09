namespace NotificationBot.Bot
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using System.Net;
    using Azure.Identity;
    using Microsoft.Bot.Builder;
    using Microsoft.Extensions.Options;
    using Microsoft.Graph;
    using Microsoft.Graph.Communications.Common;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.Graph.Communications.Core.Notifications;
    using Microsoft.Graph.Communications.Core.Serialization;
    using NotificationBot.Extensions;
    using NotificationBot.Utility;
    using Microsoft.Graph.Communications.Client;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;

    public class CallBot : ActivityHandler, ICallBot
    {
        // This is used to store the filenames as Key (unique) and madeCall.Id and a stopwatch (for voicemail timing) to keep track.
        private Dictionary<string, (string, Stopwatch)> _callInstances = new();

        public string? UserName { get; private set; } = null;
        private UserInputData? _userInputData = null;
        private GraphServiceClient? _graphClient = null;
        private readonly Uri? botBaseUri;
        private readonly ILogger<CallBot> _logger;
        private readonly IOptions<BotOptions> _botOptions;
        private readonly IGraphLogger _graphLogger;
        private readonly NotificationProcessor? notificationProcessor;
        private readonly CommsSerializer? serializer;
        private readonly string AppId;
        private readonly string AppSecret;
        private readonly int DurationBeforeVoiceMail;
        //userId = e5edd89a-8717-41b5-97c2-d152fb77bc20
        //TenantId = e62ef4b0-f598-416e-a8a1-d1946d558653
        public CallBot(IOptions<BotOptions> botoptions, ILogger<CallBot> logger, IGraphLogger graphLogger)
        {
            // If you are unfamiliar to ASP.NET go read up Dependency Injection at least so you know why this constructor makes sense.
            var name = this.GetType().Assembly.GetName().Name;
            _botOptions = botoptions;
            _logger = logger;
            _graphLogger = graphLogger;
            AppId = _botOptions.Value.AppId;
            AppSecret = _botOptions.Value.AppSecret;
            botBaseUri = _botOptions.Value.BaseURL;
            DurationBeforeVoiceMail = _botOptions.Value.DurationBeforeVoicemail;
            serializer = new CommsSerializer();
            notificationProcessor = new NotificationProcessor(serializer);
            notificationProcessor.OnNotificationReceived += NotificationProcessor_OnNotificationReceived;
        }

        /// <summary>
        /// Takes the <see cref="UserInputData"/> and returns a <see cref="GraphServiceClient"/>
        /// </summary>
        /// <returns>Returns a <see cref="GraphServiceClient"/> of the specified tenant</returns>
        private GraphServiceClient CreateGraphServiceClient(UserInputData data)
        {
            // Using TenantId ,AppId and AppSecret to instanctiate an Graph Service Client
            var ops = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            };
            var clientSecretCred = new ClientSecretCredential(data.TenantId, clientId: AppId, clientSecret: AppSecret, ops);
            return new GraphServiceClient(clientSecretCred);

        }
        /// <summary>
        /// Calls user via GraphAPI
        /// </summary>
        public async Task CallUserAsync(string userId, string TenantId, string text, Guid filename)
        {
            this._userInputData = new UserInputData
            {
                UserId = userId,
                TenantId = TenantId,
                Text = text
            };
            this._graphClient = CreateGraphServiceClient(this._userInputData);
            _logger.LogInformation("\n\n## CallUserAsync: Creating GraphServiceClient\n");


            try
            {
                // Get DisplayName of userId and also test GraphServiceClient
                var user = await this._graphClient.Users[this._userInputData.UserId].Request().GetAsync();
                UserName = user.DisplayName;
            }
            catch (ServiceException ex)
            {
                _logger.LogError("\n\n## GraphServiceClient creation failed");
                if (ex.InnerException != null)
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.InnerException.Message); }
                else
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.Message); }

                throw ex;
            }
            catch(Exception ex)
            {
                _logger.LogError("\n\n## GraphServiceClient creation failed");
                if (ex.InnerException != null)
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.InnerException.Message); }
                else
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.Message); }

                throw new Exception(ex.Message,ex);
            }


            // Instantiate Call Object
            var call = new Call
            {
                // Your Bot
                Source = new ParticipantInfo
                {
                    Identity = new IdentitySet
                    {
                        Application = new Identity
                        {
                            Id = AppId,
                        }
                    }
                },
                // User being called
                Targets = new List<InvitationParticipantInfo>
                {
                    new InvitationParticipantInfo
                    {
                        Identity = new IdentitySet
                        {
                            User = new Identity
                            {
                                Id = _userInputData.UserId,
                            }
                        }
                    }

                },
                MediaConfig = new ServiceHostedMediaConfig()
                {
                    PreFetchMedia = new List<MediaInfo>()
                    {
                        new MediaInfo()
                        {
                            ResourceId = Guid.NewGuid().ToString(),
                            Uri = new Uri(botBaseUri, $"{filename}.wav").ToString(),
                        }
                    }
                },
                RequestedModalities = new List<Modality> { Modality.Audio },
                Direction = CallDirection.Outgoing,
                CallbackUri = new Uri(botBaseUri, "callback").ToString(),
                TenantId = this._userInputData.TenantId,
            };
            
            try
            {   
                // get Call Id
                var madeCall = await this._graphClient.Communications.Calls.Request().AddAsync(call);

                _logger.LogInformation(
                    "\n\n## Calling user {username}" +
                    "\n\n## Check callId from madecall: {callId}",
                    UserName, madeCall.Id);
                // Saves the instance of out-going call
                (string, Stopwatch) entry = new(filename.ToString(), new Stopwatch());
                _callInstances.Add(madeCall.Id, entry);
            }
            catch (ServiceException ex)
            {
                if (ex.InnerException != null)
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.InnerException.Message); }
                else
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.Message); }
                throw ex;
            }
            catch (Exception ex)
            {
                _logger.LogError("\n\n## GraphServiceClient creation failed");
                if (ex.InnerException != null)
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.InnerException.Message); }
                else
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.Message); }

                throw new Exception(ex.Message, ex);
            }
        }

        /// <summary>
        /// Callback function of the Bot
        /// </summary>
        public async Task BotProcessNotificationAsync(HttpRequest request, HttpResponse response)
        {
            // Callback function
            try
            {
                var httpRequest = request.CreateRequestMessage();
                var httpResponse = await notificationProcessor.ProcessNotificationAsync(httpRequest).ConfigureAwait(false); // This line "triggers" NotificationProcessor
                await httpResponse.CreateHttpResponseAsync(response).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await response.WriteAsync(e.ToString()).ConfigureAwait(false);
            }
        }
        private void NotificationProcessor_OnNotificationReceived(NotificationEventArgs args)
        {
            // NotificationProcessor Triggers and runs this function
#pragma warning disable 4014
            try
            {
                _ = NotificationProcessorOnReceivedAsync(args).ForgetAndLogExceptionAsync(_graphLogger, $"Error processing notification {args.Notification.ResourceUrl} with scenario {args.ScenarioId}");
            }
            catch (Exception ex)
            {
                _logger.LogInformation("\n## Error: {ex}", ex.Message);
            }
#pragma warning restore 4014
        }

        /// <summary>
        /// Function that processes the callback information and performs corresponding actions
        /// </summary>
        public async Task NotificationProcessorOnReceivedAsync(NotificationEventArgs args)
        {
            // From NotificationProcessor_OnNotificationReceived then starts to check what type and state the Notification is
            if (this._graphClient == null && this._userInputData != null)
            {
                try
                {
                    this._graphClient = CreateGraphServiceClient(this._userInputData);
                    _logger.LogInformation("\n\n## NotificationProcessorOnReceivedAsync: Creating GraphServiceClient\n");
                }
                catch
                {
                    throw;
                }
            }

            var headers = new[]
            {
                new KeyValuePair<string, IEnumerable<string>>(HttpConstants.HeaderNames.ScenarioId, new[] { args.ScenarioId.ToString() }),
                new KeyValuePair<string, IEnumerable<string>>(HttpConstants.HeaderNames.ClientRequestId, new[] { args.RequestId.ToString() }),
                new KeyValuePair<string, IEnumerable<string>>(HttpConstants.HeaderNames.Tenant, new[] { args.TenantId }),
            };
            _logger.LogInformation(
                "\n\n## Processing Notification ##\n\n" +
                "\n\n## Displaying Information from args" +
                "\n\n## scenarioId :{scenarioId}," +
                "\n\n## ClientRequestId: {ClientRequestId}," +
                "\n\n## args.ResourceData: {ResourceData}\n",
                headers[0].Value, headers[1].Value, args.ResourceData.ToString());

            // This second of delay is so that the beginning of the Prompt isn't cut short.
            await Task.Delay(1000);

            // Call connection/state checks
            if (args.ResourceData is Call call)
            {
                // Get callId directly from return URL (which starts after the 22th char)
                // call.Id doesn't work (is null) 
                var perCallId = args.Notification.ResourceUrl[22..];

                _logger.LogInformation("\n\n## ResourceURL of args aka Call Id: {ResourceURL}", perCallId);
                _logger.LogInformation("\n\n## Call state: {state}\n", call.State.ToString());

                if (call.State == CallState.Establishing)
                {
                    // Begin counting call ringing duration 
                    if (!_callInstances[perCallId].Item2.IsRunning)
                    {
                        _callInstances[perCallId].Item2.Start();
                    }
                }
                if (call.State == CallState.Established && call.MediaState?.Audio == MediaState.Active)
                {
                    // DurationBeforeVoiceMail is defaultly set to Teams default value (20 seconds) + 5s
                    if (_callInstances[perCallId].Item2.Elapsed > TimeSpan.FromSeconds(DurationBeforeVoiceMail - 8) && _callInstances[perCallId].Item2.Elapsed < TimeSpan.FromSeconds(DurationBeforeVoiceMail))
                    {
                        // This logic check is to help debug voicemail
                        _logger.LogWarning(
                        "\n\n##!! Igore below if not debugging for Voicemail !!##\n\n" +
                        "\n\n## VoiceMail Dubugging Information ##" +
                        "\n\n## If 'Time Elasped' when the call isn't picked up is just a bit short " +
                        "\n\n## Ex: Time Elapsed: {elapsed} but DurationBeforeVoicemail is {DurationBeforeVoicemail}" +
                        "\n\n## Solution: Set DurationBeforeVoicemail to below {elapsed} by 2 or 3 to {newSetTime}\n\n"
                        , _callInstances[perCallId].Item2.Elapsed.TotalSeconds
                        , _botOptions.Value.DurationBeforeVoicemail
                        , _callInstances[perCallId].Item2.Elapsed.TotalSeconds
                        , (int)_callInstances[perCallId].Item2.Elapsed.TotalSeconds - 3);
                    }
                    else if (_callInstances[perCallId].Item2.Elapsed > TimeSpan.FromSeconds(DurationBeforeVoiceMail))
                    {
                        _logger.LogInformation(
                            "\n\n## User didn't pick up phone" +
                            "\n\n## Play Prompt to voicemail.");
                        // 9 seconds (default) seems to be a good duration for Teams voicemail to be prepare for recieving the prompt.
                        await Task.Delay(_botOptions.Value.TuningDurationForCorrectVoicemail*1000); // Milliseconds so duration * 1000 
                    }

                    await BotPlayPromptAsync(perCallId).ConfigureAwait(false);

                }

                _logger.LogInformation(
                    "\n\n## Call instance: {instance}\n" +
                    "\n\n## Time Elapsed: {elapsed}\n",
                    _callInstances[perCallId].Item1,
                    _callInstances[perCallId].Item2.Elapsed.TotalSeconds);
            }

            // Play Prompt checks
            else if (args.ResourceData is PlayPromptOperation playPromptOperation)
            {
                _logger.LogInformation("\n\n## PlayPromptOperation state: {status}\n", playPromptOperation.Status.ToString());

                //  Checking for the call id sent in ClientContext.
                if (string.IsNullOrWhiteSpace(playPromptOperation.ClientContext))
                {
                    throw new ServiceException(new Error()
                    {
                        Message = "No call id provided in PlayPromptOperation.ClientContext.",
                    });
                }
                else if (playPromptOperation.Status == OperationStatus.Completed)
                {

                    // The playPromptOperation has been completed
                    // First delete the generated .wav file
                    DeleteAudioFile(_callInstances[playPromptOperation.ClientContext].Item1);
                    _logger.LogInformation("\n\n## Prompt finished playing\n");

                    // Hang up the call
                    await this._graphClient.Communications.Calls[playPromptOperation.ClientContext].Request().DeleteAsync();

                    // Release memory from _callInstances for finished operation
                    _callInstances.Remove(_callInstances[playPromptOperation.ClientContext].Item1);
                }
            }

        }
        /// <summary>
        /// Deletes audio wav file after it has served it's purpose
        /// </summary>
        private void DeleteAudioFile(string fileId)
        {
            // Windows server path
            var filePath = $@"C:\home\site\wwwroot\wwwroot\{fileId}.wav";
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    _logger.LogInformation("\n\n## Deleting {filename}", filePath);
                    System.IO.File.Delete(filePath);
                }
                else
                {
                    _logger.LogError("\n\n## This File ({filename}) doesn't exist.", filePath);
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError("Error: {ex.message}",ex.Message);
                throw new Exception(ex.Message,ex);
            }
            
        }
        /// <summary>
        /// Plays the corresponding audio wav file to the correct call
        /// </summary>
        private async Task BotPlayPromptAsync(string callId)
        {
            if (this._graphClient == null && this._userInputData != null)
            {
                try
                {
                    this._graphClient = CreateGraphServiceClient(this._userInputData);
                    _logger.LogInformation("\n\n## BotPlayPromptAsync: Creating GraphServiceClient\n");
                }
                catch
                {
                    throw;
                }
            }

            _logger.LogInformation("\n\n## Accessing item in List (_callInstances)");
            _logger.LogInformation("\n\n## Found key: {key}", callId);

            // Get corresponding audio file ID from each call Id
            var filename = _callInstances[callId].Item1;
            var prompts = new Prompt[]
            {
                new MediaPrompt
                {
                    MediaInfo = new MediaInfo
                    {
                        Uri = new Uri(botBaseUri, $"{filename}.wav").ToString(),
                        ResourceId = Guid.NewGuid().ToString(),
                    },
                },
            };

            try
            {
                _logger.LogInformation("\n\n## Graph Client PlayPrompt Posting -->\n");
                await this._graphClient.Communications.Calls[callId]
                .PlayPrompt(
                prompts: prompts,
                clientContext: callId) // callId added to each PlayPrompt so we have infomation to track
                .Request().PostAsync();
            }
            catch (ServiceException ex)
            {
                if (ex.InnerException != null)
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.InnerException.Message); }
                else
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.Message); }

                throw ex;
            }
        }
        private class UserInputData
        {
            public string UserId { get; set; }
            public string TenantId { get; set; }
            public string Text { get; set; }
        }
    }
}
