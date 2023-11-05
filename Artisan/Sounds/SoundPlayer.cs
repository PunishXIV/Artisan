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
                    string sound = "Time Up";
                    string path = Path.Combine(Svc.PluginInterface.AssemblyLocation.Directory.FullName, "Sounds", $"{sound}.mp3");
                    if (!File.Exists(path)) return;
                    var reader = new Mp3FileReader(path);

                    waveOut.Init(reader);
                    var previousVol = waveOut.Volume;
                    waveOut.Volume = P.Config.SoundVolume;
                    waveOut.Play();
                    waveOut.PlaybackStopped += (sender, args) => waveOut.Volume = previousVol;
                }
                catch(Exception ex)
                {
                    ex.Log();
                }
            }
        }
    }
}
