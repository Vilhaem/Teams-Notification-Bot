// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Graph;
using Microsoft.Graph.Communications.Common.Telemetry;
using System.Runtime.CompilerServices;

namespace NotificationBot.Utility
{
    public static class BotUtils
    {
        public static async Task ForgetAndLogExceptionAsync(
            this Task task,
            IGraphLogger logger,
            string? description = null,
            [CallerMemberName] string? memberName = null,
            [CallerFilePath] string? filePath = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                await task.ConfigureAwait(false);
                logger?.Verbose(
                    $"Completed running task successfully: {description ?? string.Empty}",
                    memberName: memberName,
                    filePath: filePath,
                    lineNumber: lineNumber);
            }
            catch (Exception e)
            {
                // Log and absorb all exceptions here.
                logger?.Error(
                    e,
                    $"Caught an Exception running the task: {description ?? string.Empty}",
                    memberName: memberName,
                    filePath: filePath,
                    lineNumber: lineNumber);
            }
        }
        /// <summary>
        /// Deletes audio wav file after it has served it's purpose
        /// </summary>
        private static void DeleteAudioFile(string fileId, ILogger logger)
        {
            // Windows server path
            var filePath = $@"C:\home\site\wwwroot\wwwroot\{fileId}.wav";
            // Linuxor ot would need to find out

            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    logger.LogInformation("\n\n## Deleting {filename}", filePath);
                    System.IO.File.Delete(filePath);
                }
                else
                {
                    logger.LogError("\n\n## This File ({filename}) doesn't exist.", filePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error: {ex.message}", ex.Message);
                throw new Exception(ex.Message, ex);
            }

        }

        public static async Task EndCall(string callId, string fileId, ILogger logger, GraphServiceClient graphClient)
        {
            DeleteAudioFile(fileId, logger);
            await graphClient.Communications.Calls[callId].Request().DeleteAsync();
            
        }
    }
}
