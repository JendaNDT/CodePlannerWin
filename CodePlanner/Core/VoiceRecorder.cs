using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CodePlanner.Core
{
    /// <summary>
    /// Zprostředkovává nahrávání zvuku z mikrofonu na Windows pomocí MCI (winmm.dll).
    /// </summary>
    public static class VoiceRecorder
    {
        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", CharSet = CharSet.Ansi)]
        private static extern int mciSendString(string lpstrCommand, StringBuilder? lpstrReturnString, int uReturnLength, IntPtr hwndCallback);

        private static bool _recording = false;
        private static readonly object _lock = new object();

        public static void StartRecording()
        {
            lock (_lock)
            {
                if (_recording) return;

                // Close previous instance just in case
                mciSendString("close recsound", null, 0, IntPtr.Zero);

                try
                {
                    // Open new recording
                    int err = mciSendString("open new type waveaudio alias recsound", null, 0, IntPtr.Zero);
                    if (err != 0) throw new InvalidOperationException("Failed to open recording device (MCI). Please check your microphone.");
                    
                    // Set quality: 16-bit, 16000 Hz, mono (ideal for speech-to-text)
                    mciSendString("set recsound bitspersample 16", null, 0, IntPtr.Zero);
                    mciSendString("set recsound samplespersec 16000", null, 0, IntPtr.Zero);
                    mciSendString("set recsound channels 1", null, 0, IntPtr.Zero);
                    
                    // Start recording
                    err = mciSendString("record recsound", null, 0, IntPtr.Zero);
                    if (err != 0) throw new InvalidOperationException("Failed to start recording (MCI).");
                    _recording = true;
                }
                catch
                {
                    mciSendString("close recsound", null, 0, IntPtr.Zero);
                    throw;
                }
            }
        }

        public static string? StopRecording()
        {
            lock (_lock)
            {
                if (!_recording) return null;

                string cesta = Path.Combine(Path.GetTempPath(), $"voice_input_{Guid.NewGuid():N}.wav");
                
                // If file exists, delete it
                try { if (File.Exists(cesta)) File.Delete(cesta); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error deleting file: {ex.Message}"); }

                // Stop recording
                mciSendString("stop recsound", null, 0, IntPtr.Zero);
                
                // Save to file
                int err = mciSendString($"save recsound \"{cesta}\"", null, 0, IntPtr.Zero);
                mciSendString("close recsound", null, 0, IntPtr.Zero);
                
                _recording = false;

                if (err != 0 || !File.Exists(cesta)) return null;
                return cesta;
            }
        }
    }
}
