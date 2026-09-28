using DoomFPS.Application.Services;
using DoomFPS.Application.Interfaces;
using DoomFPS.Infrastructure.Levels;
using DoomFPS.Infrastructure.Repositories;

namespace DoomFPS.Presentation;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        // Perfiles locales sin correo ni contraseña, como una cuenta local de
        // consola: se guardan en disco en la carpeta de datos del usuario.
        IProfileRepository profiles = new JsonProfileRepository();
        System.Windows.Forms.Application.Run(new GameForm(CreateCampaign, profiles));
    }

    // Único punto que conoce e inyecta las implementaciones concretas.
    // La campaña recibe la fuente de niveles, el repositorio de progreso y una
    // fábrica de repositorios de partida: uno nuevo por misión.
    private static CampaignService CreateCampaign()
        => new(new CampaignLevelFactory(), new InMemoryCampaignRepository(),
            () => new InMemoryGameStateRepository());
}
