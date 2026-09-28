using DoomFPS.Application.Interfaces;
using DoomFPS.Domain;
using DoomFPS.Infrastructure.Repositories;

namespace DoomFPS.Web.Platform;

// Mismos efectos sintéticos que SoundBank (escritorio): se generan como WAV en
// memoria y se entregan una vez a Web Audio. En el navegador no hay carpeta
// "Sonidos" para reemplazarlos, así que se usan siempre los de fábrica.
public sealed class WebSoundBank
{
    private readonly IPlatform platform;
    private readonly HashSet<string> known = new();
    private readonly HashSet<string> playedThisFrame = new();
    private bool muted;

    public WebSoundBank(IPlatform platform)
    {
        this.platform = platform;
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
        Add("growl_zombi", 0.30, 100, 0.70, 1.6);
        Add("growl_imp", 0.24, 340, 0.45, 1.9);
        Add("growl_demonio", 0.30, 150, 0.60, 1.7);
        Add("growl_espectro", 0.34, 620, 0.25, 1.3);
        Add("growl_almaperdida", 0.20, 780, 0.35, 1.5);
        Add("growl_cacodemon", 0.36, 70, 0.35, 1.2);
        Add("growl_caballeroinfernal", 0.32, 82, 0.50, 1.4);
        Add("growl_mancubus", 0.34, 55, 0.55, 1.5);
        Add("growl_ciberdemonio", 0.50, 42, 0.55, 1.0);
    }

    public bool Muted
    {
        get => muted;
        set { muted = value; platform.SetMuted(value); }
    }

    private void Add(string name, double seconds, double frequency, double noise, double decay)
    {
        known.Add(name);
        platform.LoadSound(name, Synthesize(seconds, frequency, noise, decay));
    }

    // Copia exacta de SoundBank.Synthesize (escritorio).
    public static byte[] Synthesize(double seconds, double frequency, double noise, double decay)
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
                double sweep = frequency * (1 - t * 0.45);
                double wave = Math.Sin(2 * Math.PI * sweep * i / rate);
                wave = wave * (1 - noise) + (random.NextDouble() * 2 - 1) * noise;
                writer.Write((short)(wave * envelope * 8000));
            }
        }
        return stream.ToArray();
    }

    public void BeginFrame() => playedThisFrame.Clear();

    public void Play(string kind)
    {
        if (muted || !known.Contains(kind) || !playedThisFrame.Add(kind)) return;
        platform.PlaySound(kind);
    }
}

// Voz de la IA de la nave con la síntesis de voz del navegador (Web Speech).
public sealed class WebVoice
{
    private readonly IPlatform platform;
    public WebVoice(IPlatform platform) => this.platform = platform;
    public bool Muted { get; set; }
    public void Say(string text)
    {
        if (Muted || string.IsNullOrWhiteSpace(text)) return;
        platform.Speak(text);
    }
}

// Reutiliza JsonProfileRepository tal cual sobre el sistema de archivos en
// memoria de WebAssembly y copia el JSON a localStorage tras cada cambio,
// para que los perfiles sobrevivan al cerrar la pestaña.
public sealed class BrowserProfileRepository : IProfileRepository
{
    public const string StorageKey = "sector09.perfiles";
    private const string FilePath = "/tmp/sector09/perfiles.json";
    private readonly IPlatform platform;
    private readonly JsonProfileRepository inner;

    public BrowserProfileRepository(IPlatform platform)
    {
        this.platform = platform;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = platform.StorageGet(StorageKey);
            if (!string.IsNullOrWhiteSpace(json)) File.WriteAllText(FilePath, json);
            else if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch { /* Sin almacenamiento: se juega sin perfiles persistentes. */ }
        inner = new JsonProfileRepository(FilePath);
    }

    public IReadOnlyList<PlayerProfile> All => inner.All;
    public string? LastUsedName => inner.LastUsedName;
    public PlayerProfile? Find(string name) => inner.Find(name);
    public PlayerProfile CreateOrGet(string name) { var p = inner.CreateOrGet(name); Sync(); return p; }
    public void Save(PlayerProfile profile) { inner.Save(profile); Sync(); }
    public void Delete(string name) { inner.Delete(name); Sync(); }
    public void SetLastUsed(string? name) { inner.SetLastUsed(name); Sync(); }

    private void Sync()
    {
        try { if (File.Exists(FilePath)) platform.StorageSet(StorageKey, File.ReadAllText(FilePath)); }
        catch { /* Guardar es en el mejor esfuerzo. */ }
    }
}
