using ECommons.DalamudServices;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.Sounds
{
    public static class SoundPlayer
    {
        private static readonly object _lockObj = new();

        public static void PlaySound()
        {
            lock (_lockObj)
            {
                string sound = "Time Up";
                string path = Path.Combine(Svc.PluginInterface.AssemblyLocation.Directory.FullName, "Sounds", $"{sound}.mp3");
                if (!File.Exists(path)) return;
                var reader = new Mp3FileReader(path);
                var waveOut = new WaveOutEvent();

                waveOut.Init(reader);
                waveOut.Volume = P.Config.SoundVolume;
                waveOut.Play();
            }
        }
    }
}
