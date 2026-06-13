using Microsoft.AspNetCore.Components.Authorization;
using SportVitaal.Web.Components;
using SportVitaal.Shared.Services;
using SportVitaal.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add device-specific services used by the SportVitaal.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// WebApi client: typed HttpClient + per-circuit JWT holder.
var apiBaseUrl = builder.Configuration["WebApi:BaseUrl"] ?? "http://localhost:5272";
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddHttpClient<SportVitaalApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));

// Employee authentication (JWT-backed) for the /medewerker admin area.
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(SportVitaal.Shared._Imports).Assembly);

app.Run();