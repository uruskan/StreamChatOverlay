using System.IO;
using System.Media;

namespace StreamChatOverlay.Services;

public sealed class SoundService
{
    private SoundPlayer? _player;
    private string _currentSound = "None";

    // Available sound names
    public static readonly string[] AvailableSounds =
        ["None", "Pop", "Ding", "Click", "Blip"];

    public void SetSound(string soundName)
    {
        _currentSound = soundName;
        _player?.Dispose();
        _player = null;

        if (soundName == "None") return;

        var wavData = GenerateWav(soundName);
        var ms = new MemoryStream(wavData);
        _player = new SoundPlayer(ms);
        _player.Load();
    }

    public void Play(double volume)
    {
        if (_currentSound == "None" || _player == null) return;
        // SoundPlayer doesn't support volume natively, but we can
        // adjust the WAV data amplitude. For simplicity, just play at full volume
        // when volume > 0.
        if (volume > 0)
            _player.Play();
    }

    private static byte[] GenerateWav(string soundName)
    {
        // Generate simple sine wave beeps as WAV data
        int sampleRate = 44100;
        int durationMs = soundName switch
        {
            "Pop" => 80,
            "Ding" => 200,
            "Click" => 30,
            "Blip" => 60,
            _ => 100
        };
        int frequency = soundName switch
        {
            "Pop" => 800,
            "Ding" => 1200,
            "Click" => 2000,
            "Blip" => 1500,
            _ => 1000
        };

        int numSamples = sampleRate * durationMs / 1000;
        var samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            // Apply fade-out envelope
            double envelope = 1.0 - ((double)i / numSamples);
            double sample = Math.Sin(2 * Math.PI * frequency * t) * envelope * 0.5;
            samples[i] = (short)(sample * short.MaxValue);
        }

        // Build WAV file in memory
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        int dataSize = numSamples * 2; // 16-bit = 2 bytes per sample
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
}
