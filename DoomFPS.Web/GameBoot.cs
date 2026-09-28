using DoomFPS.Application.Services;
using DoomFPS.Infrastructure.Levels;
using DoomFPS.Infrastructure.Repositories;
using DoomFPS.Web.Platform;

namespace DoomFPS.Web;

// Composición de la versión web: mismo cableado que Program.cs de escritorio,
// cambiando solo el repositorio de perfiles (localStorage) y la plataforma.
public static class GameBoot
{
    public static WebGame Create(IPlatform platform)
    {
        var profiles = new BrowserProfileRepository(platform);
        return new WebGame(CreateCampaign, profiles, platform);
    }

    private static CampaignService CreateCampaign()
        => new(new CampaignLevelFactory(), new InMemoryCampaignRepository(),
            () => new InMemoryGameStateRepository());
}
