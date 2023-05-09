namespace NotificationBot.SpeechService
{
    public class SpeechServiceOptions
    {
        public const string Speech = "Speech";
        public Uri? Endpoint { get; set; }
        public string Key { get; set; } = string.Empty;
        public Uri? STSUri { get; set; }
        public string SpeechSpeedPercentage { get; set; } = string.Empty;
        public string SpeechGender { get; set; } = string.Empty;
    }
}
