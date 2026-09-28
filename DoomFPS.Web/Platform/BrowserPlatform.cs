using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace DoomFPS.Web.Platform;

// Puente con wwwroot/js/sector09.js mediante [JSImport]. El fotograma se pasa
// como MemoryView (sin copias) y el HUD como una lista compacta de órdenes.
[SupportedOSPlatform("browser")]
internal static partial class JsBridge
{
    public const string Module = "sector09";

    [JSImport("init", Module)]
    public static partial void Init(
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<double, double, double> onFrame,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.String, JSType.Number>>] Action<string, string, double> onEvent);

    [JSImport("present", Module)]
    public static partial void Present([JSMarshalAs<JSType.MemoryView>] Span<byte> pixels, string commands);

    [JSImport("measure", Module)]
    public static partial double Measure(string font, string text);

    [JSImport("loadSound", Module)]
    public static partial void LoadSound(string name, [JSMarshalAs<JSType.MemoryView>] Span<byte> wav);

    [JSImport("playSound", Module)]
    public static partial void PlaySound(string name);

    [JSImport("speak", Module)]
    public static partial void Speak(string text);

    [JSImport("setMuted", Module)]
    public static partial void SetMuted(bool muted);

    [JSImport("storageGet", Module)]
    public static partial string? StorageGet(string key);

    [JSImport("storageSet", Module)]
    public static partial void StorageSet(string key, string value);

    [JSImport("lockPointer", Module)]
    public static partial void LockPointer();

    [JSImport("unlockPointer", Module)]
    public static partial void UnlockPointer();

    [JSImport("ready", Module)]
    public static partial void Ready();
}

[SupportedOSPlatform("browser")]
public sealed class BrowserPlatform : IPlatform
{
    public void Present(ReadOnlySpan<int> pixels, string commands)
    {
        // MemoryView necesita un Span mutable; el búfer no se modifica en JS.
        var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(pixels), pixels.Length));
        JsBridge.Present(bytes, commands);
    }

    public double MeasureText(string cssFont, string text) => JsBridge.Measure(cssFont, text);
    public void LoadSound(string name, byte[] wav) => JsBridge.LoadSound(name, wav);
    public void PlaySound(string name) => JsBridge.PlaySound(name);
    public void Speak(string text) => JsBridge.Speak(text);
    public void SetMuted(bool muted) => JsBridge.SetMuted(muted);
    public string? StorageGet(string key) => JsBridge.StorageGet(key);
    public void StorageSet(string key, string value) => JsBridge.StorageSet(key, value);
    public void LockPointer() => JsBridge.LockPointer();
    public void UnlockPointer() => JsBridge.UnlockPointer();
}
