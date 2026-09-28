using DoomFPS.Domain;

namespace DoomFPS.Application.Interfaces
{
    // La campaña pide niveles a través de esta interfaz. Quien los construye
    // (mapas escritos a mano hoy, un lector de archivos mañana) vive en Infrastructure.
    public interface ILevelSource
    {
        int LevelCount { get; }
        Level Create(int index, DifficultySettings settings);
    }
}
