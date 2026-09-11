using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BridgeGameCalculator.Client;
using BridgeGameCalculator.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = builder.Configuration["ApiBaseAddress"];
var httpBaseAddress = string.IsNullOrWhiteSpace(apiBaseAddress)
    ? builder.HostEnvironment.BaseAddress
    : apiBaseAddress;

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(httpBaseAddress, UriKind.Absolute) });

builder.Services.AddSingleton<SessionState>();
builder.Services.AddSingleton<ISessionStateService>(sp => sp.GetRequiredService<SessionState>());

await builder.Build().RunAsync();
