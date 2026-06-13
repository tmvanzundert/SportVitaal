using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SportVitaal.Infrastructure.Data;
using SportVitaal.Infrastructure.Extensions;
using SportVitaal.Application.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured. Set it in appsettings.Development.json (dev) or user-secrets/environment variables (production).");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SportVitaal";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SportVitaalClients";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "JwtBearer";
    options.DefaultChallengeScheme = "JwtBearer";
}).AddJwtBearer("JwtBearer", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// Register AppDbContext using MariaDB/Pomelo provider. Connection string from configuration.
var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Port=3306;Database=SportVitaal;User=root;Password=";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(cs, ServerVersion.AutoDetect(cs)));

// Register infrastructure services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Register application services (domain service implementations, event dispatcher, etc.)
builder.Services.AddApplicationServices();
var app = builder.Build();

// Apply pending migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    // Seed essential data (workouts, locations)
    SeedData.EnsureSeedDataAsync(db).GetAwaiter().GetResult();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Serve uploaded files.
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Basic health endpoint
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .WithName("Health");

app.MapControllers();

app.Run();
