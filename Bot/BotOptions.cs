﻿namespace NotificationBot.Bot
{
    public class BotOptions
    {
        public const string Bot = "Bot";
        public string AppId { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public Uri? BaseURL { get; set; }
        public int DurationBeforeVoicemail { get; set; } = 0;
        public int TuningDurationForCorrectVoicemail { get; set; } = 0;
        public string BotTeamsDisplayName { get; set; } = string.Empty;
        public string BotTeamsId { get; set; } = string.Empty;
        public bool UsingHeaderAuth { get; set; } = false;
    }
}
