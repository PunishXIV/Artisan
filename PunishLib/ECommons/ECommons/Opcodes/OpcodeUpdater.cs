using ECommons.Logging;
using ECommons.Schedulers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ECommons.Opcodes
{
    public static class OpcodeUpdater
    {
        static HttpClient client;
        public static void DownloadOpcodes(string url, Action<Dictionary<string, uint>> successCallback, Action<string> failureCallback = null)
        {
            client ??= new();
            new Thread(() =>
            {
                try
                {
                    Dictionary<string, uint> dic = new();
                    PluginLog.Debug($"Opcode list downloading from {url}...");
                    var result = client.GetAsync(url).Result;
                    if (!result.IsSuccessStatusCode)
                    {
                        _ = new TickScheduler(delegate
                        {
                            var error = $"{(int)result.StatusCode} {result.StatusCode}";
                            if (failureCallback == null)
                            {
                                PluginLog.Warning($"Failed to download opcodes: {error}");
                            }
                            else
                            {
                                failureCallback(error);
                            }
                        });
                    }
                    else
                    {
                        PluginLog.Debug("Opcode list download success");
                        var content = result.Content.ReadAsStringAsync().Result;
                        foreach (var s in content.Split("\n"))
                        {
                            var o = s.Split("|");
                            if (o.Length == 2)
                            {
                                PluginLog.Debug($"Received opcode: {o[0]} = 0x{o[1]}");
                                if (uint.TryParse(o[1], NumberStyles.HexNumber, null, out var code))
                                {
                                    PluginLog.Debug($"Opcode {o[0]} = 0x{code:X} ({code})");
                                    dic[o[0]] = code;
                                }
                            }
                        }
                        _ = new TickScheduler(delegate
                        {
                            successCallback(dic);
                        });
                    }
                }
                catch(Exception e)
                {
                    var error = $"{e.Message} \n {e.StackTrace ?? ""}";
                    if(failureCallback == null)
                    {
                        PluginLog.Warning($"Failed to download opcodes: {error}");
                    }
                    else
                    {
                        failureCallback(error);
                    }
                }
            }).Start();
        }
    }
}
