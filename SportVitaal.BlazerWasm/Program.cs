using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SportVitaal.BlazerWasm;
using SportVitaal.BlazerWasm.Services;
using SportVitaal.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Device-specific service used by the SportVitaal.Shared project.
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// WebApi client: HttpClient pointed at the (cross-origin) WebApi + in-memory JWT holder.
var apiBaseUrl = builder.Configuration["WebApi:BaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddScoped(sp => new SportVitaalApiClient(
    new HttpClient { BaseAddress = new Uri(apiBaseUrl) },
    sp.GetRequiredService<TokenProvider>()));

// Employee authentication (JWT-backed) for the /medewerker admin area.
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

await builder.Build().RunAsync();
