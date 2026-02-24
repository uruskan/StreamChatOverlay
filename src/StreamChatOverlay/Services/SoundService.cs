using System.IO;
using System.Windows.Media;

namespace StreamChatOverlay.Services;

public sealed class SoundService : IDisposable
{
    private MediaPlayer _mediaPlayer = new();
    private string? _tempWavPath;
    private string _currentSound = "None";

    // Available sound names
    public static readonly string[] AvailableSounds =
        ["None", "Chirp", "Bubble", "Chime", "Drop", "Ping"];

    public void SetSound(string soundName)
    {
        _currentSound = soundName;
        _mediaPlayer.Stop();
        _mediaPlayer.Close();

        // Clean up old temp file
        CleanupTempFile();

        if (soundName == "None") return;

        var wavData = GenerateWav(soundName);
        _tempWavPath = Path.Combine(Path.GetTempPath(), $"sco_notification_{soundName}.wav");
        File.WriteAllBytes(_tempWavPath, wavData);
        _mediaPlayer.Open(new Uri(_tempWavPath));
    }

    public void Play(double volume)
    {
        if (_currentSound == "None" || _tempWavPath == null) return;
        if (volume <= 0) return;

        _mediaPlayer.Stop();
        _mediaPlayer.Position = TimeSpan.Zero;
        _mediaPlayer.Volume = volume;
        _mediaPlayer.Play();
    }

    /// <summary>
    /// Preview the given sound at the given volume (used from settings UI).
    /// </summary>
    public void PlayPreview(string soundName, double volume)
    {
        if (soundName == "None" || volume <= 0) return;

        // Temporarily generate and play the preview sound
        var wavData = GenerateWav(soundName);
        var previewPath = Path.Combine(Path.GetTempPath(), $"sco_preview_{soundName}.wav");
        File.WriteAllBytes(previewPath, wavData);

        var preview = new MediaPlayer();
        preview.Open(new Uri(previewPath));
        preview.Volume = volume;
        // MediaPlayer.Open is async; wait for MediaOpened to play
        preview.MediaOpened += (_, _) => preview.Play();
        preview.MediaEnded += (_, _) =>
        {
            preview.Close();
            try { File.Delete(previewPath); } catch { }
        };
    }

    private static byte[] GenerateWav(string soundName)
    {
        const int sampleRate = 44100;
        var samples = soundName switch
        {
            "Chirp" => GenerateChirp(sampleRate),
            "Bubble" => GenerateBubble(sampleRate),
            "Chime" => GenerateChime(sampleRate),
            "Drop" => GenerateDrop(sampleRate),
            "Ping" => GeneratePing(sampleRate),
            _ => GenerateChirp(sampleRate)
        };

        return BuildWav(samples, sampleRate);
    }

    // Gentle rising chirp — soft bird-like "tweet"
    private static short[] GenerateChirp(int sr)
    {
        int len = sr * 120 / 1000; // 120ms
        var s = new short[len];
        for (int i = 0; i < len; i++)
        {
            double p = (double)i / len;
            double freq = 600 + 800 * p; // sweep 600→1400 Hz
            double t = (double)i / sr;
            double env = Math.Sin(Math.PI * p); // smooth bell envelope
            double val = Math.Sin(2 * Math.PI * freq * t) * env * 0.35;
            s[i] = (short)(val * short.MaxValue);
        }
        return s;
    }

    // Bubbly pop — quick pitch rise then drop, soft and round
    private static short[] GenerateBubble(int sr)
    {
        int len = sr * 150 / 1000; // 150ms
        var s = new short[len];
        for (int i = 0; i < len; i++)
        {
            double p = (double)i / len;
            // Pitch rises quickly then falls
            double freq = 400 + 500 * Math.Sin(Math.PI * p);
            double t = (double)i / sr;
            double env = Math.Pow(1.0 - p, 2); // quadratic fade-out
            // Mix fundamental + soft octave for roundness
            double val = (Math.Sin(2 * Math.PI * freq * t) * 0.7
                        + Math.Sin(2 * Math.PI * freq * 0.5 * t) * 0.3) * env * 0.35;
            s[i] = (short)(val * short.MaxValue);
        }
        return s;
    }

    // Two-tone chime — gentle "ding-dong" feel
    private static short[] GenerateChime(int sr)
    {
        int len = sr * 250 / 1000; // 250ms
        var s = new short[len];
        int half = len / 2;
        for (int i = 0; i < len; i++)
        {
            double t = (double)i / sr;
            double p = (double)i / len;
            // First half: E5 (659Hz), second half: G5 (784Hz)
            double freq = i < half ? 659 : 784;
            // Smooth envelope per note with overall fade
            double noteP = i < half ? (double)i / half : (double)(i - half) / (len - half);
            double env = Math.Sin(Math.PI * noteP) * (1.0 - p * 0.5);
            // Add a soft harmonic for warmth
            double val = (Math.Sin(2 * Math.PI * freq * t) * 0.6
                        + Math.Sin(2 * Math.PI * freq * 2 * t) * 0.15
                        + Math.Sin(2 * Math.PI * freq * 3 * t) * 0.05) * env * 0.35;
            s[i] = (short)(val * short.MaxValue);
        }
        return s;
    }

    // Water drop — descending tone with a soft plop
    private static short[] GenerateDrop(int sr)
    {
        int len = sr * 180 / 1000; // 180ms
        var s = new short[len];
        for (int i = 0; i < len; i++)
        {
            double p = (double)i / len;
            double t = (double)i / sr;
            // Quick exponential pitch drop
            double freq = 1200 * Math.Exp(-4.0 * p) + 300;
            double env = Math.Exp(-5.0 * p); // fast exponential decay
            double val = Math.Sin(2 * Math.PI * freq * t) * env * 0.4;
            s[i] = (short)(val * short.MaxValue);
        }
        return s;
    }

    // Soft ping — round, warm tone with gentle decay
    private static short[] GeneratePing(int sr)
    {
        int len = sr * 200 / 1000; // 200ms
        var s = new short[len];
        for (int i = 0; i < len; i++)
        {
            double p = (double)i / len;
            double t = (double)i / sr;
            double freq = 523; // C5 — pleasant middle pitch
            // Smooth attack + exponential decay
            double attack = Math.Min(1.0, p * 20); // 5ms attack
            double env = attack * Math.Exp(-3.5 * p);
            // Rich tone: fundamental + octave below + soft 5th
            double val = (Math.Sin(2 * Math.PI * freq * t) * 0.5
                        + Math.Sin(2 * Math.PI * freq * 0.5 * t) * 0.3
                        + Math.Sin(2 * Math.PI * freq * 1.5 * t) * 0.1) * env * 0.4;
            s[i] = (short)(val * short.MaxValue);
        }
        return s;
    }

    private static byte[] BuildWav(short[] samples, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        int dataSize = samples.Length * 2;
        int fileSize = 36 + dataSize;

        writer.Write("RIFF"u8);
        writer.Write(fileSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);            // chunk size
        writer.Write((short)1);      // PCM
        writer.Write((short)1);      // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // byte rate
        writer.Write((short)2);      // block align
        writer.Write((short)16);     // bits per sample
        writer.Write("data"u8);
        writer.Write(dataSize);

        foreach (var s in samples)
            writer.Write(s);

        return ms.ToArray();
    }

    private void CleanupTempFile()
    {
        if (_tempWavPath != null && File.Exists(_tempWavPath))
        {
            try { File.Delete(_tempWavPath); } catch { }
            _tempWavPath = null;
        }
    }

    public void Dispose()
    {
        _mediaPlayer.Stop();
        _mediaPlayer.Close();
        CleanupTempFile();
    }
}
