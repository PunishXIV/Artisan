using ECommons;
using ECommons.DalamudServices;
using NAudio.Wave;
using System;
using System.IO;

namespace Artisan.Sounds
{
    public static class SoundPlayer
    {
        private static readonly object _lockObj = new();

        private static WaveOutEvent waveOut = new();

        public static void PlaySound()
        {
            lock (_lockObj)
            {
                try
                {
                    WaveStream? reader = null;

                    bool useFallback = false;

                    if(P.Config.UseCustomNotificationSound)
                    {
                        if (!File.Exists(P.Config.CustomSoundPath)) useFallback = true;

                        if(!useFallback)
                        {
                            string path = P.Config.CustomSoundPath;
                            string ext = Path.GetExtension(P.Config.CustomSoundPath);

                            switch(ext)
                            {
                                case ".wav":
                                    reader = new WaveFileReader(path);
                                    break;
                                case ".mp3":
                                    reader = new Mp3FileReader(path);
                                    break;
                                default:
                                    useFallback = true;
                                    break;
                            }

                        }
                    }
                    
                    if(useFallback)
                    {
                        string sound = "Time Up";
                        string path = Path.Combine(Svc.PluginInterface.AssemblyLocation.Directory.FullName, "Sounds", $"{sound}.mp3");
                        reader = new Mp3FileReader(path);
                    }

                    if (reader != null)
                    {
                        waveOut.Init(reader);
                        var previousVol = waveOut.Volume;
                        waveOut.Volume = P.Config.SoundVolume;
                        waveOut.Play();
                        waveOut.PlaybackStopped += (sender, args) => waveOut.Volume = previousVol;
                    }
                }
                catch(Exception ex)
                {
                    ex.Log();
                }
            }
        }
    }
}
