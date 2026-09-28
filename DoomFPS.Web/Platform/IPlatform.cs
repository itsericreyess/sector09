namespace DoomFPS.Web.Platform;

// Todo lo que el juego necesita del navegador pasa por aquí: dibujar el
// fotograma, medir texto, audio, voz, almacenamiento y captura del ratón.
// En el navegador lo implementa BrowserPlatform (JSImport); en las pruebas
// sin ventana se usa una implementación falsa.
public interface IPlatform
{
    void Present(ReadOnlySpan<int> pixels, string commands);
    double MeasureText(string cssFont, string text);
    void LoadSound(string name, byte[] wav);
    void PlaySound(string name);
    void Speak(string text);
    void SetMuted(bool muted);
    string? StorageGet(string key);
    void StorageSet(string key, string value);
    void LockPointer();
    void UnlockPointer();
}
