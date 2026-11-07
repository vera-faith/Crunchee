#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using Crunchee.Audio;
using Crunchee.Core;

namespace Crunchee.EditorTools
{
    /// <summary>Crunchee/Export Sounds: writes every synthesized sound as a
    /// WAV into Captures/Audio/ so the owner can audition them, and logs
    /// duration/peak/RMS per file.</summary>
    public static class SoundExport
    {
        [MenuItem("Crunchee/Export Sounds")]
        public static void Export()
        {
            string dir = "Captures/Audio";
            Directory.CreateDirectory(dir);
            int n = 0;
            void W(string name, AudioClip clip)
            {
                if (clip == null) { Debug.LogWarning($"[Export] null clip {name}"); return; }
                WriteWav(Path.Combine(dir, name + ".wav"), clip);
                var d = new float[clip.samples];
                clip.GetData(d, 0);
                float peak = 0f, sum = 0f;
                foreach (var s in d) { peak = Mathf.Max(peak, Mathf.Abs(s)); sum += s * s; }
                float rms = Mathf.Sqrt(sum / d.Length);
                Debug.Log($"[Export] {name}.wav  {clip.length:0.00}s  peak={peak:0.00}  rms={rms:0.000}");
                n++;
            }
            for (int l = 1; l <= 10; l++)
            {
                W($"wax_l{l}", AsmrSynth.WaxCrack(l, 1));
                W($"waxstep_l{l}", AsmrSynth.WaxStepCrack(l, 1));
            }
            foreach (BaseMaterial b in System.Enum.GetValues(typeof(BaseMaterial)))
                W($"base_{b}", AsmrSynth.BaseFor(b, 1));
            W("sugar", AsmrSynth.SugarCrunch(1));
            W("honey", AsmrSynth.HoneyPull(1));
            W("nailpolish", AsmrSynth.NailPolishTap(1));
            W("squish", AsmrSynth.SquishStep(1));
            W("squishdeep", AsmrSynth.SquishStep(1, true));
            W("music_ambient", AsmrSynth.DreamyAmbient());
            Debug.Log($"[Export] done: {n} wavs -> {dir}");
        }

        static void WriteWav(string path, AudioClip clip)
        {
            var d = new float[clip.samples * clip.channels];
            clip.GetData(d, 0);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int dataLen = d.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataLen);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)clip.channels);
                w.Write(clip.frequency); w.Write(clip.frequency * clip.channels * 2);
                w.Write((short)(clip.channels * 2)); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(dataLen);
                foreach (var s in d) w.Write((short)Mathf.Clamp(s * 32767f, -32768f, 32767f));
            }
        }
    }
}
#endif
