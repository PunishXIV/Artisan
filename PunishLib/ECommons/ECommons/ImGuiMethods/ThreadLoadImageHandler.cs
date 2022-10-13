using ECommons.Logging;
using ECommons.DalamudServices;
using ImGuiScene;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;

namespace ECommons.ImGuiMethods
{
    public class ThreadLoadImageHandler
    {
        internal static ConcurrentDictionary<string, ImageLoadingResult> CachedTextures = new();
        static volatile bool ThreadRunning = false;
        static HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        public static bool TryGetTextureWrap(string url, out TextureWrap textureWrap)
        {
            ImageLoadingResult result;
            if(!CachedTextures.TryGetValue(url, out result))
            {
                result = new();
                CachedTextures[url] = result;
                BeginThreadIfNotRunning();
            }
            textureWrap = result.texture;
            return result.texture != null;
        }

        internal static void BeginThreadIfNotRunning()
        {
            if (ThreadRunning) return;
            PluginLog.Information("Starting ThreadLoadImageHandler");
            ThreadRunning = true;
            new Thread(() =>
            {
                int idleTicks = 0;
                Safe(delegate
                {
                    while(idleTicks < 100)
                    {
                        Safe(delegate
                        {
                            if (CachedTextures.TryGetFirst(x => x.Value.isCompleted == false, out var keyValuePair))
                            {
                                idleTicks = 0;
                                keyValuePair.Value.isCompleted = true;
                                PluginLog.Information("Loading image " + keyValuePair.Key);
                                if (keyValuePair.Key.StartsWith("http:", StringComparison.OrdinalIgnoreCase) || keyValuePair.Key.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
                                {
                                    var result = httpClient.GetAsync(keyValuePair.Key).Result;
                                    result.EnsureSuccessStatusCode();
                                    var content = result.Content.ReadAsByteArrayAsync().Result;
                                    keyValuePair.Value.texture = Svc.PluginInterface.UiBuilder.LoadImage(content);
                                }
                                else
                                {
                                    keyValuePair.Value.texture = Svc.PluginInterface.UiBuilder.LoadImage(keyValuePair.Key);
                                }
                            }
                        });
                        idleTicks++;
                        Thread.Sleep(100);
                    }
                });
                PluginLog.Information($"Stopping ThreadLoadImageHandler, ticks={idleTicks}");
                ThreadRunning = false;
            }).Start();
        }
    }
}
