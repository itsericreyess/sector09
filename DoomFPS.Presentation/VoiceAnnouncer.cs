using System.IO;
using System.Media;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Threading;

namespace DoomFPS.Presentation;

// Voz de "inteligencia artificial de nave", no un locutor de Windows.
// Se sintetiza con una voz femenina del sistema a un flujo de audio en
// memoria (PCM sin cabecera), se le aplica un pequeño proceso de señal
// (modulación en anillo suave + eco corto metálico + tono más agudo) y
// luego se empaqueta como WAV para reproducirla, igual que el resto de
// sonidos del juego (que también se generan por código, nunca con
// archivos externos).
//
// Los avisos se reproducen uno detrás de otro en un único hilo dedicado
// (nunca en paralelo ni interrumpiéndose entre sí): así ninguna frase se
// corta a mitad porque llegó otra encima. Si se acumulan varias mientras
// suena una, solo se conservan las más recientes para no quedar leyendo
// avisos viejos.
internal sealed class VoiceAnnouncer : IDisposable
{
    private readonly SpeechSynthesizer? synth;
    private readonly Queue<string> pending = new();
    private readonly object queueLock = new();
    private readonly SemaphoreSlim signal = new(0);
    private volatile bool disposed;
    private const int SampleRate = 22050;
    public bool Muted { get; set; }

    public VoiceAnnouncer()
    {
        try
        {
            synth = new SpeechSynthesizer();
            var voices = synth.GetInstalledVoices().Select(v => v.VoiceInfo).ToList();
            var chosen = ReadVoiceOverride(voices) ?? voices.FirstOrDefault(v => v.Gender == VoiceGender.Female);
            if (chosen is not null) synth.SelectVoice(chosen.Name);
        }
        catch
        {
            // Sin voces instaladas o sin dispositivo de audio: la voz queda
            // desactivada, pero el resto del juego no se ve afectado.
            synth = null;
        }
        System.Threading.Tasks.Task.Run(WorkerLoop);
    }

    // Deja (o crea si no existe) Sonidos/voz.txt: un archivo de texto donde el
    // jugador puede escribir el nombre exacto de una voz de Windows instalada
    // para forzarla, igual que puede reemplazar los efectos .wav. La voz en sí
    // no es un archivo de audio fijo (se genera al vuelo para cada frase), así
    // que esto es lo más parecido a "cambiar el archivo de la voz" que existe.
    private static VoiceInfo? ReadVoiceOverride(List<VoiceInfo> voices)
    {
        try
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "Sonidos");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "voz.txt");
            if (!File.Exists(path))
            {
                File.WriteAllText(path,
                    "# Nombre exacto de la voz de Windows para los avisos de la IA de la nave.\r\n" +
                    "# Deja este archivo así para usar la voz femenina automática.\r\n" +
                    "# Para ver los nombres instalados en tu Windows, en PowerShell:\r\n" +
                    "#   Add-Type -AssemblyName System.Speech\r\n" +
                    "#   (New-Object System.Speech.Synthesis.SpeechSynthesizer).GetInstalledVoices() | % { $_.VoiceInfo.Name }\r\n" +
                    "# y copia aquí abajo, en una línea sin #, el nombre exacto que quieras usar.\r\n");
                return null;
            }
            string? name = File.ReadAllLines(path)
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#'));
            return name is null ? null : voices.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        catch { return null; }
    }

    public void Say(string phrase)
    {
        if (Muted || synth is null || string.IsNullOrWhiteSpace(phrase)) return;
        lock (queueLock)
        {
            // Como mucho dos avisos en espera: si llegan más, se descartan los
            // más viejos en vez de acumular una cola de frases atrasadas.
            while (pending.Count >= 2) pending.Dequeue();
            pending.Enqueue(phrase);
        }
        signal.Release();
    }

    // Un único hilo dedicado reproduce las frases de una en una, de principio
    // a fin (PlaySync), así que nunca hay dos sonando a la vez ni una frase
    // corta a otra a mitad de camino.
    private void WorkerLoop()
    {
        var s = synth;
        if (s is null) return;
        while (!disposed)
        {
            signal.Wait();
            if (disposed) return;
            string? phrase;
            lock (queueLock) { phrase = pending.Count > 0 ? pending.Dequeue() : null; }
            if (phrase is null || Muted) continue;
            RenderAndPlay(s, phrase);
        }
    }

    private void RenderAndPlay(SpeechSynthesizer s, string phrase)
    {
        try
        {
            using var raw = new MemoryStream();
            var format = new SpeechAudioFormatInfo(SampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
            s.SetOutputToAudioStream(raw, format);
            string voiceName = s.Voice.Name;
            string culture = s.Voice.Culture.Name;
            string escaped = phrase.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            string ssml =
                $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{culture}\">" +
                $"<voice name=\"{voiceName}\"><prosody pitch=\"+18%\" rate=\"+4%\">{escaped}</prosody></voice></speak>";
            s.SpeakSsml(ssml);
            s.SetOutputToNull();

            byte[] pcm = ApplySciFiVoiceEffect(raw.ToArray());
            using var outStream = new MemoryStream(WrapAsWav(pcm));
            using var player = new SoundPlayer(outStream);
            player.Load();
            player.PlaySync();
        }
        catch { /* La narración por voz es un extra, nunca bloquea el juego. */ }
    }

    // Modulación en anillo suave (el "zumbido" clásico de voz sintética),
    // más un eco metálico corto: la mezcla que distingue una IA de nave de
    // una simple lectura de texto. Trabaja directo sobre PCM de 16 bits.
    private static byte[] ApplySciFiVoiceEffect(byte[] pcm)
    {
        int sampleCount = pcm.Length / 2;
        if (sampleCount < 4) return pcm;
        var samples = new short[sampleCount];
        Buffer.BlockCopy(pcm, 0, samples, 0, sampleCount * 2);

        const double carrierHz = 34.0;
        const double ringMix = 0.28;
        int echoSamples = (int)(SampleRate * 0.017);
        const double echoMix = 0.22;

        var processed = new double[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            double dry = samples[i];
            double carrier = Math.Sin(2 * Math.PI * carrierHz * i / SampleRate);
            double ringed = dry * (1 - ringMix) + dry * carrier * ringMix;
            double echoed = i >= echoSamples ? processed[i - echoSamples] * echoMix : 0;
            processed[i] = ringed + echoed;
        }

        var output = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            // Ligera saturación suave: le da un borde "digital" sin destruir la voz.
            double v = processed[i] / 32768.0;
            v = Math.Tanh(v * 1.15);
            output[i] = (short)Math.Clamp(v * 32767.0, short.MinValue, short.MaxValue);
        }

        var result = new byte[sampleCount * 2];
        Buffer.BlockCopy(output, 0, result, 0, sampleCount * 2);
        return result;
    }

    // PCM crudo de 16 bits mono a WAV reproducible: misma receta que usa
    // SoundBank para sus propios efectos generados por código.
    private static byte[] WrapAsWav(byte[] pcm)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(SampleRate); writer.Write(SampleRate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length);
            writer.Write(pcm);
        }
        return stream.ToArray();
    }

    public void Dispose()
    {
        disposed = true;
        signal.Release();
        synth?.Dispose();
    }
}
