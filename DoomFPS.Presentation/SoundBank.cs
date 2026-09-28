using System.IO;
using System.Media;
using System.Threading;

namespace DoomFPS.Presentation;

// Efectos generados por código (no hacen falta archivos de Doom ni descargas),
// pero cada uno se guarda también como .wav en una carpeta "Sonidos" junto al
// ejecutable la primera vez que se usa. Si el jugador entra a esa carpeta y
// reemplaza un archivo por el suyo (mismo nombre, formato .wav), el juego lo
// detecta solo y lo usa desde ese momento, sin necesidad de recompilar.
internal sealed class SoundBank : IDisposable
{
    // Varias voces por efecto: reproducir el mismo sonido dos veces seguidas
    // (disparos rápidos, pasos) ya no corta la reproducción anterior, porque
    // cada repetición usa un SoundPlayer distinto en turno rotativo.
    private const int Voices = 4;
    private readonly Dictionary<string, SoundPlayer[]> sounds = new();
    private readonly Dictionary<string, int> nextVoice = new();
    private readonly Dictionary<string, byte[]> builtIn = new();
    private readonly HashSet<string> playedThisFrame = new();
    private readonly string folder = Path.Combine(AppContext.BaseDirectory, "Sonidos");
    private FileSystemWatcher? watcher;
    public bool Muted { get; set; }

    public SoundBank()
    {
        //    nombre      dur.   frecuencia  ruido  caída
        Add("shot", 0.16, 95, 0.70, 2.0);
        Add("pistol", 0.09, 210, 0.55, 2.4);
        Add("boom", 0.30, 70, 0.80, 1.6);
        Add("plasma", 0.10, 620, 0.20, 2.2);
        Add("rocket", 0.24, 130, 0.60, 1.5);
        Add("explode", 0.36, 55, 0.90, 1.3);
        Add("fireball", 0.16, 300, 0.45, 2.0);
        Add("rifle", 0.11, 160, 0.65, 2.4);
        Add("hurt", 0.14, 120, 0.70, 2.0);
        Add("pickup", 0.13, 720, 0.05, 2.0);
        Add("weapon", 0.20, 460, 0.10, 1.8);
        Add("door", 0.26, 170, 0.30, 1.4);
        Add("kill", 0.20, 150, 0.55, 1.6);
        Add("empty", 0.07, 240, 0.35, 3.0);
        Add("growl", 0.28, 88, 0.55, 1.4);
        Add("charge", 0.22, 105, 0.60, 1.5);
        Add("soul", 0.20, 520, 0.40, 1.8);
        Add("thud", 0.14, 78, 0.75, 2.2);
        Add("stomp", 0.34, 48, 0.85, 1.4);
        Add("summon", 0.40, 240, 0.35, 1.2);
        Add("bosswake", 0.45, 62, 0.55, 1.1);
        Add("jump", 0.09, 340, 0.20, 2.0);
        Add("land", 0.10, 85, 0.45, 2.2);
        Add("won", 0.60, 620, 0.05, 1.0);
        Add("lost", 0.50, 90, 0.35, 1.0);
        Add("level", 0.45, 540, 0.05, 1.1);
        Add("reload", 0.16, 130, 0.55, 2.6);
        Add("reloaded", 0.09, 480, 0.15, 3.0);
        Add("step", 0.07, 60, 0.90, 3.4);

        // Un rugido/grito distinto por cada tipo de monstruo, para que se
        // reconozca quién ronda cerca solo por el oído antes de verlo.
        Add("growl_zombi", 0.30, 100, 0.70, 1.6);
        Add("growl_imp", 0.24, 340, 0.45, 1.9);
        Add("growl_demonio", 0.30, 150, 0.60, 1.7);
        Add("growl_espectro", 0.34, 620, 0.25, 1.3);
        Add("growl_almaperdida", 0.20, 780, 0.35, 1.5);
        Add("growl_cacodemon", 0.36, 70, 0.35, 1.2);
        Add("growl_caballeroinfernal", 0.32, 82, 0.50, 1.4);
        Add("growl_mancubus", 0.34, 55, 0.55, 1.5);
        Add("growl_ciberdemonio", 0.50, 42, 0.55, 1.0);

        try
        {
            Directory.CreateDirectory(folder);
            watcher = new FileSystemWatcher(folder, "*.wav")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            watcher.Changed += OnSoundFileTouched;
            watcher.Created += OnSoundFileTouched;
            watcher.Renamed += OnSoundFileTouched;
        }
        catch { /* Sin permisos de escritura o carpeta no disponible: se sigue con los sonidos de fábrica. */ }
    }

    private void Add(string name, double seconds, double frequency, double noise, double decay)
    {
        byte[] data = Synthesize(seconds, frequency, noise, decay);
        builtIn[name] = data;
        sounds[name] = BuildPool(ResolveInitialBytes(name, data));
        nextVoice[name] = 0;
    }

    // Genera el mismo tipo de onda sintética que antes, pero como bytes WAV
    // en memoria en vez de crear el SoundPlayer directamente: así se puede
    // volcar a disco (para que el jugador la encuentre y la reemplace) o
    // descartar si ya existe un archivo propio del jugador con ese nombre.
    private static byte[] Synthesize(double seconds, double frequency, double noise, double decay)
    {
        const int rate = 22050;
        int count = (int)(seconds * rate);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            var random = new Random(9);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)count;
                double envelope = Math.Pow(1 - t, decay);
                // La frecuencia baja durante el sonido: da cuerpo a golpes y explosiones.
                double sweep = frequency * (1 - t * 0.45);
                double wave = Math.Sin(2 * Math.PI * sweep * i / rate);
                wave = wave * (1 - noise) + (random.NextDouble() * 2 - 1) * noise;
                writer.Write((short)(wave * envelope * 8000));
            }
        }
        return stream.ToArray();
    }

    // Si ya existe Sonidos/<nombre>.wav (de una partida anterior o porque el
    // jugador puso el suyo), se usa ese archivo tal cual. Si no existe, se
    // vuelca el sonido de fábrica ahí mismo para que el jugador sepa dónde
    // reemplazarlo. Cualquier fallo (permisos, disco lleno) cae de vuelta al
    // sonido generado en memoria: el juego nunca se queda sin audio por esto.
    private byte[] ResolveInitialBytes(string name, byte[] generated)
    {
        try
        {
            string path = Path.Combine(folder, name + ".wav");
            if (!File.Exists(path)) File.WriteAllBytes(path, generated);
            return File.ReadAllBytes(path);
        }
        catch { return generated; }
    }

    private static SoundPlayer[] BuildPool(byte[] data)
    {
        var players = new SoundPlayer[Voices];
        for (int i = 0; i < Voices; i++)
        {
            // Cada voz necesita su propia copia del flujo: no se puede compartir
            // un MemoryStream entre varios SoundPlayer reproduciendo a la vez.
            var player = new SoundPlayer(new MemoryStream(data, false));
            try { player.Load(); } catch { /* El juego sigue funcionando sin dispositivo de audio. */ }
            players[i] = player;
        }
        return players;
    }

    // El editor de audio del jugador suele guardar el archivo varias veces
    // seguidas (o dejarlo bloqueado un instante); se reintenta unas cuantas
    // veces con una pequeña espera antes de rendirse y mantener el sonido
    // anterior.
    private void OnSoundFileTouched(object? sender, FileSystemEventArgs e)
    {
        string name = Path.GetFileNameWithoutExtension(e.Name ?? string.Empty);
        if (string.IsNullOrEmpty(name) || !builtIn.ContainsKey(name)) return;
        string path = Path.Combine(folder, name + ".wav");
        System.Threading.Tasks.Task.Run(() => ReloadFromDisk(name, path));
    }

    private void ReloadFromDisk(string name, string path)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return;
                byte[] data = File.ReadAllBytes(path);
                if (data.Length <= 44) { Thread.Sleep(150); continue; }
                var fresh = BuildPool(data);
                var previous = sounds[name];
                sounds[name] = fresh;
                foreach (var old in previous) old.Dispose();
                return;
            }
            catch { Thread.Sleep(150); }
        }
    }

    // Un mismo efecto no suena dos veces en el mismo paso de simulación.
    public void BeginFrame() => playedThisFrame.Clear();

    public void Play(string kind)
    {
        if (Muted || !playedThisFrame.Add(kind) || !sounds.TryGetValue(kind, out var players)) return;
        int index = nextVoice[kind];
        nextVoice[kind] = (index + 1) % players.Length;
        try { players[index].Play(); } catch { /* El audio es opcional. */ }
    }

    public void Dispose()
    {
        watcher?.Dispose();
        foreach (var players in sounds.Values)
            foreach (var player in players) player.Dispose();
    }
}
