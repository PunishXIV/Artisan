using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.Discord;
internal static class Discord
{
    private static readonly HttpClient _httpClient = new();

    private static readonly string ListFinishedMessage = @"{
                                                      ""content"": null,
                                                      ""embeds"": [
                                                        {
                                                          ""title"": ""Artisan"",
                                                          ""description"": ""Crafting list has finished processing."",
                                                          ""url"": ""https://discord.com/channels/1001823907193552978/1003395035762528387"",
                                                          ""color"": 16683843
                                                        }
                                                      ],
                                                      ""username"": ""Artisan"",
                                                      ""avatar_url"": ""https://puni.sh/_next/image?url=https%3A%2F%2Fs3.puni.sh%2Fmedia%2Fplugin%2F6%2Ficon-3h8wd5b9qr.png&w=256&q=100"",
                                                      ""attachments"": []
                                                    }";

    private static readonly string EnduranceFinishedMessage = @"{
                                                      ""content"": null,
                                                      ""embeds"": [
                                                        {
                                                          ""title"": ""Artisan"",
                                                          ""description"": ""Endurance mode has finished processing."",
                                                          ""url"": ""https://discord.com/channels/1001823907193552978/1003395035762528387"",
                                                          ""color"": 16683843
                                                        }
                                                      ],
                                                      ""username"": ""Artisan"",
                                                      ""avatar_url"": ""https://puni.sh/_next/image?url=https%3A%2F%2Fs3.puni.sh%2Fmedia%2Fplugin%2F6%2Ficon-3h8wd5b9qr.png&w=256&q=100"",
                                                      ""attachments"": []
                                                    }";

    public static void SendCraftingListFinished()
    {
        if(string.IsNullOrEmpty(P.Config.DiscordWebhook)) return;

        SendMessage(ListFinishedMessage);
    }

    public static void SendEnduranceFinished()
    {
        if (string.IsNullOrEmpty(P.Config.DiscordWebhook)) return;

        SendMessage(EnduranceFinishedMessage);
    }

    private static void SendMessage(string message)
    {
        Task.Run(async () =>
        {
            var response = await _httpClient.PostAsync(P.Config.DiscordWebhook, new StringContent(message, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        });
    }   
}
