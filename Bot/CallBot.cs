namespace NotificationBot.Bot
{
    using Azure.Identity;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Bot.Builder;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.Graph;
    using Microsoft.Graph.Communications.Client;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.Graph.Communications.Core.Notifications;
    using Microsoft.Graph.Communications.Core.Serialization;
    using NotificationBot.Extensions;
    using NotificationBot.Utility;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    public class CallBot : ActivityHandler, ICallBot
    {
        public const string SubscribeToneAudio = "SubscribeToneAudio";
        // This is used to store the filenames as Key (unique) and madeCall.Id and a stopwatch (for voicemail timing) to keep track.
        private Dictionary<string, (string, Stopwatch)> callInstances = new();
        public string? UserName { get; private set; } = null;
        private GraphServiceClient? graphClient = null;
        private readonly ILogger<CallBot> _logger;
        private readonly IGraphLogger _graphLogger;
        private readonly NotificationProcessor? notificationProcessor;
        private readonly CommsSerializer? serializer;
        private readonly string AppId;
        private readonly string AppSecret;
        private readonly Uri? botBaseUri;
        private readonly string PSTNAppId;
        private readonly int DurationBeforeVoiceMail;
        private readonly int TuningDurationForCorrectVoicemail;
        private readonly string BotTeamsDisplayName;
        private readonly bool UsingSubscribeTone;
        private readonly int SubscribeToneWaitTime;
        public CallBot(IOptions<BotOptions> botoptions, ILogger<CallBot> logger, IGraphLogger graphLogger)
        {
            // If you are unfamiliar to ASP.NET go read up Dependency Injection at least so you know why this constructor makes sense.
            var name = this.GetType().Assembly.GetName().Name;
            _logger = logger;
            _graphLogger = graphLogger;
            AppId = botoptions.Value.AppId;
            AppSecret = botoptions.Value.AppSecret;
            botBaseUri = botoptions.Value.BaseURL;
            PSTNAppId = botoptions.Value.PSTNAppId;
            DurationBeforeVoiceMail = botoptions.Value.DurationBeforeVoicemail;
            TuningDurationForCorrectVoicemail = botoptions.Value.TuningDurationForCorrectVoicemail;
            BotTeamsDisplayName = botoptions.Value.BotTeamsDisplayName;
            UsingSubscribeTone = botoptions.Value.UsingSubscribeTone;
            SubscribeToneWaitTime = botoptions.Value.SubscribeToneWaitTime;
            serializer = new CommsSerializer();
            notificationProcessor = new NotificationProcessor(serializer);
            notificationProcessor.OnNotificationReceived += NotificationProcessor_OnNotificationReceived;
        }

        /// <summary>
        /// Takes the tenantId from the POST method and returns a <see cref="GraphServiceClient"/>
        /// </summary>
        /// <returns>Returns a <see cref="GraphServiceClient"/> of the specified tenant</returns>
        private GraphServiceClient CreateGraphServiceClient(string tenantId)
        {
            // Using TenantId ,AppId and AppSecret to instanctiate an Graph Service Client
            var ops = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            };
            var clientSecretCred = new ClientSecretCredential(tenantId, clientId: AppId, clientSecret: AppSecret, ops);
            return new GraphServiceClient(clientSecretCred);

        }
        /// <summary>
        /// Calls Teams user via GraphAPI
        /// </summary>
        public async Task CallUserAsync(string userId, string TenantId, string text, Guid filename)
        {
            graphClient = CreateGraphServiceClient(TenantId);
            _logger.LogInformation("\n\n## CallUserAsync: Creating GraphServiceClient\n");
            try
            {
                // Get DisplayName of userId and also test GraphServiceClient
                var user = await graphClient.Users[userId].Request().GetAsync();
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
            catch (Exception ex)
            {
                _logger.LogError("\n\n## GraphServiceClient creation failed");
                if (ex.InnerException != null)
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.InnerException.Message); }
                else
                { _logger.LogError("\n\n## Error message: {ex.Message}", ex.Message); }

                throw new Exception(ex.Message, ex);
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
                            DisplayName = BotTeamsDisplayName,
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
                                Id = userId,
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
                TenantId = TenantId,
            };

            try
            {
                _logger.LogInformation("\n\n## Calling user {username}", UserName);
                // get Call Id
                var madeCall = await graphClient.Communications.Calls.Request().AddAsync(call);
                _logger.LogInformation("\n\n## madecall.Id: {callId}", madeCall.Id);
                // Saves the instance of out-going call
                (string, Stopwatch) entry = new(filename.ToString(), new Stopwatch());
                callInstances.Add(madeCall.Id, entry);
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
        /// Calls phone number via GraphAPI (Need to apply for this service; 需要申請才可使用)
        /// </summary>
        public async Task CallPSTNAsync(string phoneNumber, string TenantId, string text, Guid filename)
        {
            graphClient = CreateGraphServiceClient(TenantId);
            _logger.LogInformation("\n\n## CallPSTNAsync: Creating GraphServiceClient\n");

            // Instantiate Call Object
            var call = new Call
            {
                // Your Bot
                Source = new ParticipantInfo
                {
                    Identity = new IdentitySet
                    {
                        AdditionalData = new Dictionary<string, object>
                        {
                            {
                                "applicationInstance" , new
                                {
                                    DisplayName = BotTeamsDisplayName,
                                    Id = PSTNAppId,
                                }
                            },
                        },

                    }
                },
                // User being called
                Targets = new List<InvitationParticipantInfo>
                {
                    new InvitationParticipantInfo
                    {
                        Identity = new IdentitySet
                        {
                            AdditionalData = new Dictionary<string, object>
                            {
                                {
                                    "phone" , new
                                    {
                                        Id = phoneNumber,
                                    }
                                },
                            },

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
                TenantId = TenantId,
            };

            try
            {
                // get Call Id
                var madeCall = await graphClient.Communications.Calls.Request().AddAsync(call);

                _logger.LogInformation(
                    "\n\n## PSTN: Calling Phone number {phone number}" +
                    "\n\n## PSTN: Check callId from madecall: {callId}",
                    phoneNumber, madeCall.Id);
                // Saves the instance of out-going call
                (string, Stopwatch) entry = new(filename.ToString(), new Stopwatch());
                callInstances.Add(madeCall.Id, entry);
            }
            catch (ServiceException ex)
            {
                if (ex.InnerException != null)
                { _logger.LogError("\n\n## PSTN: Error message: {ex.Message}", ex.InnerException.Message); }
                else
                { _logger.LogError("\n\n## PSTN: Error message: {ex.Message}", ex.Message); }
                throw ex;
            }
            catch (Exception ex)
            {
                _logger.LogError("\n\n## PSTN: GraphServiceClient creation failed");
                if (ex.InnerException != null)
                { _logger.LogError("\n\n## PSTN: Error message: {ex.Message}", ex.InnerException.Message); }
                else
                { _logger.LogError("\n\n## PSTN: Error message: {ex.Message}", ex.Message); }

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

        /// <summary>
        /// <see cref="NotificationProcessor"/> Triggers and runs this function<br/>
        /// Which triggers <see cref="NotificationProcessorOnReceivedAsync"/> that checks the type and status of callback
        /// </summary>
        /// <param name="args"></param>
        private void NotificationProcessor_OnNotificationReceived(NotificationEventArgs args)
        {
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

            // Not needed
            //var headers = new[]
            //{
            //    new KeyValuePair<string, IEnumerable<string>>(HttpConstants.HeaderNames.ScenarioId, new[] { args.ScenarioId.ToString() }),
            //    new KeyValuePair<string, IEnumerable<string>>(HttpConstants.HeaderNames.ClientRequestId, new[] { args.RequestId.ToString() }),
            //    new KeyValuePair<string, IEnumerable<string>>(HttpConstants.HeaderNames.Tenant, new[] { args.TenantId }),
            //};

            _logger.LogInformation(
                "\n\n## Processing Notification ##\n\n" +
                "\n\n## Displaying Information from args" +
                "\n\n## args.ResourceData: {ResourceData}\n",
                args.ResourceData.ToString());

            // This second of delay is so that the beginning of the Prompt isn't cut short.
            //await Task.Delay(1000);

            // Call connection/state checks
            if (args.ResourceData is Call call)
            {
                // Get callId directly from return URL (which starts after the 22th char)
                // call.Id doesn't work (is null) 
                var perCallId = args.Notification.ResourceUrl[22..];
                _logger.LogInformation("\n\n## ResourceURL of args aka Call Id: {perCallId}", perCallId);
                _logger.LogInformation("\n\n## Call state: {state}\n", call.State.ToString());

                // Establishing block
                if (call.State == CallState.Establishing)
                {
                    // Begin counting call ringing duration 
                    if (!callInstances[perCallId].Item2.IsRunning)
                    {
                        callInstances[perCallId].Item2.Start();
                        _logger.LogInformation("\n\n## Call({callid}) begin timer", perCallId);
                    }
                }

                // Established block
                if (call.State == CallState.Established)
                {
                    // PlayPrompt block
                    if (call.MediaState?.Audio == MediaState.Active)
                    {
                        // DurationBeforeVoiceMail is defaultly set to Teams default value (20 seconds) + 5s
                        if (callInstances[perCallId].Item2.Elapsed > TimeSpan.FromSeconds(DurationBeforeVoiceMail - 8) && callInstances[perCallId].Item2.Elapsed < TimeSpan.FromSeconds(DurationBeforeVoiceMail))
                        {
                            // This logic check is to help debug voicemail
                            _logger.LogWarning(
                            "\n\n##!! Igore below if not debugging for Voicemail !!##\n\n" +
                            "\n\n## VoiceMail Dubugging Information ##" +
                            "\n\n## If 'Time Elasped' when the call isn't picked up is just a bit short " +
                            "\n\n## Ex: Time Elapsed: {elapsed} but DurationBeforeVoicemail is {DurationBeforeVoicemail}" +
                            "\n\n## Solution: Set DurationBeforeVoicemail to below {elapsed} by 2 or 3 to {newSetTime}\n\n"
                            , callInstances[perCallId].Item2.Elapsed.TotalSeconds
                            , DurationBeforeVoiceMail
                            , callInstances[perCallId].Item2.Elapsed.TotalSeconds
                            , (int)callInstances[perCallId].Item2.Elapsed.TotalSeconds - 3);
                        }
                        else if (callInstances[perCallId].Item2.Elapsed > TimeSpan.FromSeconds(DurationBeforeVoiceMail))
                        {
                            _logger.LogInformation(
                                "\n\n## User didn't pick up phone" +
                                "\n\n## Play Prompt to voicemail.");
                            // 9 seconds (default) seems to be a good duration for Teams voicemail to be prepare for recieving the prompt.
                            // However this depends on user name length as a main factor
                            // But still play around and test
                            await Task.Delay(TuningDurationForCorrectVoicemail * 1000); // Milliseconds so duration * 1000 
                        }
                        await BotPlayPromptAsync(perCallId).ConfigureAwait(false);
                    }
                    // Init call to be Subscribe to tone
                    else if (UsingSubscribeTone && call.ToneInfo is null)
                    {
                        _logger.LogInformation("\n\n## Enter Subscribe to tone block");
                        await graphClient.Communications.Calls[perCallId].SubscribeToTone(clientContext: perCallId).Request().PostAsync();
                        _logger.LogInformation("\n\n## Subscribe to tone to call: {callid}", perCallId);
                        await Task.Delay(3000);
                        await BotPlayPromptAsync(callId: perCallId, filename: SubscribeToneAudio);
                    }
                    // Check Subscribe to tone input
                    else if (call.ToneInfo is not null)
                    {
                        var tone = call.ToneInfo.Tone.Value;
                        _logger.LogInformation(
                            "\n\n## ToneInfo detected {tone}"
                            ,tone);
                        
                        switch (tone)
                        {
                            case Tone.Tone1:
                                // do business logic 1 ex. confirm and ends call
                                _logger.LogInformation("\n\n## Message Confirmed. Hanging up call");
                                await BotUtils.EndCall(perCallId, callInstances[perCallId].Item1, _logger, graphClient);
                                // Release memory from callInstances for finished operation
                                callInstances.Remove(perCallId);
                                break;
                            case Tone.Tone2:
                                // do business logic 2 ex. play prompt again
                                await BotPlayPromptAsync(perCallId);
                                // restart wait timer so call doesn't end mid prompt
                                callInstances[perCallId].Item2.Restart();
                                await Task.Delay(3000);
                                await BotPlayPromptAsync(callId: perCallId, filename: SubscribeToneAudio);
                                break;
                        }
                    }
                }
                _logger.LogInformation(
                    "\n\n## Call instance: {instance}\n" +
                    "\n## Time Elapsed: {elapsed}\n",
                    callInstances[perCallId].Item1,
                    callInstances[perCallId].Item2.Elapsed.TotalSeconds);
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
                    _logger.LogInformation("\n\n## Prompt finished playing\n");
                    var promptCallId = playPromptOperation.ClientContext;
                    if (!UsingSubscribeTone)
                    {
                        // Hang up the call after prompt is played (orginal no subscribe to tone behavior)
                        await BotUtils.EndCall(promptCallId, callInstances[promptCallId].Item1, _logger, graphClient);
                    }
                    else
                    {

                        // Wait for user to press tone and perform corresponding function
                        callInstances[promptCallId].Item2.Restart();

                        // A set timer until call ends
                        while(callInstances[promptCallId].Item2.Elapsed.TotalSeconds <= SubscribeToneWaitTime)
                        {
                            await Task.Delay(1000);
                        }
                        _logger.LogInformation("\n\n ## Tone waiting timed out");
                        await BotUtils.EndCall(promptCallId, callInstances[promptCallId].Item1, _logger, graphClient);
                        // Release memory from callInstances for finished operation
                        callInstances.Remove(promptCallId);
                    }
                }
            }

        }
        /// <summary>
        /// Plays the corresponding audio wav file to the correct call
        /// </summary>
        private async Task BotPlayPromptAsync(string callId, string filename = "")
        {
            if (filename is "")
            {
                //_logger.LogInformation("\n\n## Accessing item in List (_callInstances)");
                //_logger.LogInformation("\n\n## Found key: {key}", callId);

                // Get corresponding audio file ID from each call Id
                filename = callInstances[callId].Item1;
            }

            _logger.LogInformation("\n\n## Playing Prompt with filename: {filename} ##\n\n",filename);

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
                await graphClient.Communications.Calls[callId]
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
    }
}
