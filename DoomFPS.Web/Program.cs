using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DoomFPS.Web;
using DoomFPS.Web.Platform;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var host = builder.Build();

// Carga el módulo JS que dibuja en el canvas y recoge teclado/ratón.
string moduleUrl = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "js/sector09.js").ToString();
await JSHost.ImportAsync(JsBridge.Module, moduleUrl);

var game = GameBoot.Create(new BrowserPlatform());
Action<double, double, double> onFrame = (now, dx, dy) =>
{
    try { game.Frame(now, dx, dy); }
    catch (Exception ex) { Console.Error.WriteLine(ex); }
};
Action<string, string, double> onEvent = (type, arg, value) =>
{
    try { game.Event(type, arg, value); }
    catch (Exception ex) { Console.Error.WriteLine(ex); }
};
JsBridge.Init(onFrame, onEvent);
JsBridge.Ready();

await host.RunAsync();
