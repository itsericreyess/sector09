using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Infrastructure.Levels;
using DoomFPS.Infrastructure.Repositories;
using NBomber.CSharp;

var scenario = Scenario.Create("inicio_campana_sector09", async _ =>
{
    try
    {
        var campaign = new CampaignService(new CampaignLevelFactory(), new InMemoryCampaignRepository(), () => new InMemoryGameStateRepository());
        campaign.StartNewCampaign(Difficulty.Normal);
        return campaign.LevelCount == 7 && campaign.Game.Player.IsAlive ? Response.Ok() : Response.Fail();
    }
    catch (Exception) { return Response.Fail(); }
})
.WithoutWarmUp()
.WithLoadSimulations(Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

NBomberRunner.RegisterScenarios(scenario).Run();
