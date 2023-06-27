using System.Text;

namespace NotificationBot.SpeechService
{
    public static class SpeechServices
    {
        /// <summary>
        /// Fetch Token from STSURi using subscription key obtained from Azure Speech Service.
        /// </summary>
        /// <param name="STSUri">STS Endpoint Uri</param>
        /// <param name="subscriptionKey">Key of registered Speech Service.</param>
        /// <param name="logger">Logger to monitor status of function.</param>
        /// <returns>Token used to generate text-to-speech file</returns>
        public static async Task<string> FetchTokenAsync(string STSUri, string subscriptionKey, ILogger logger)
        {
            using var client = new HttpClient();
            try
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                UriBuilder uriBuilder = new(STSUri);

                var result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null);
                logger.LogInformation("\n\n## Token Uri: {0}", uriBuilder.Uri.AbsoluteUri);
                logger.LogInformation("\n\n## Fetching Token");
                return await result.Content.ReadAsStringAsync();
            }
            catch (Exception ex) { throw new Exception(ex.Message, ex); }
            finally { client.Dispose(); }
        }

        /// <summary>
        /// Generate audio wav file from text, with registered domain specific Azure speech service
        /// </summary>
        /// <param name="text">text to be made into audio</param>
        /// <param name="token">Token to allow calling speech service</param>
        /// <param name="endPointUri">Domain specific end point URL</param>
        /// <param name="logger">Logger to monitor status of function.</param>
        /// <returns>A <see cref="Guid"/> that represents the name of the generated audio file (<see cref="Guid"/>.wav)</returns>
        public static async Task<Guid> GenerateTextToSpeechAudioFile(string text, string token,Uri endPointUri, ILogger logger)
        {
            // This GUID will become the name of each .wav file
            var objectId = Guid.NewGuid();
            string newbody = "<speak version=\"1.0\" " +
                "xmlns=\"http://www.w3.org/2001/10/synthesis\" " +
                "xmlns:mstts=\"http://www.w3.org/2001/mstts\" " +
                "xml:lang=\"en-US\">" +
                "<voice xml:lang=\"zh-TW\" " +
                "xml:gender=\"Female\" " +
                "name=\"zh-TW-HsiaoChenNeural\">" +
                "<mstts:silence type=\"Sentenceboundary\" " +
                $"value=\"200ms\"/>" +
                $"<prosody rate=\"-20%\" pitch=\"0%\">" + text + "</prosody></voice></speak>";
            //SSML 產生器 文字轉換語音 – 真實 AI 語音產生器 |Microsoft Azure 
            //SCHEMA 參考：Speech Synthesis Markup Language (SSML) overview - Speech service - Azure Cognitive Services | Microsoft Learn


            using HttpClient client = new();
            HttpRequestMessage httpRequest = new()
            {
                Method = HttpMethod.Post,
                RequestUri = endPointUri
            };
            httpRequest.Headers.Add("Authorization", "Baerer " + token);
            httpRequest.Headers.Add("User-Agent", "NTTVoiceToText01");
            httpRequest.Headers.Add("X-Microsoft-OutputFormat", "riff-16khz-16bit-mono-pcm");
            //Text-to-speech API reference (REST) - Speech service - Azure Cognitive Services | Microsoft Learn 有夠扯，範例不完整，Teams 只能撥我上面那個格式，我試半天才研究出來

            httpRequest.Content = new StringContent(newbody, Encoding.UTF8, "application/ssml+xml");
            using HttpResponseMessage response = await client.SendAsync(httpRequest).ConfigureAwait(false);
            using Stream dataStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            // Production Line
            using FileStream fileStream = new($@"C:\home\site\wwwroot\wwwroot\{objectId}.wav", FileMode.Create, FileAccess.Write, FileShare.Write);

            // Local Debug Line
            // Replace the file path with your local systems
            //using FileStream fileStream = new($@"C:\Users\a-liuwilliam\OneDrive - Microsoft\Desktop\ASP.NET webapp\Notification-Bot\wwwroot\{objectId}.wav", FileMode.Create, FileAccess.Write, FileShare.Write);

            try
            {
                await dataStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError("Something went wrong generating .wav file");
                throw new Exception(ex.Message, ex);
            }
            finally
            {
                // Release Memory after using
                fileStream.Close();
                dataStream.Close();
                response.Dispose();
                httpRequest.Dispose();
                client.Dispose();
            }
            return objectId;
        }
    }
}

