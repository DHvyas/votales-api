using Microsoft.EntityFrameworkCore;
using Neo4jClient;
using VoTales.API.Data;
using VoTales.API.Services;
using VoTales.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Serilog;

// Configure Serilog early for bootstrap logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting VoTales API");

    var builder = WebApplication.CreateBuilder(args);

    // Replace default logging with Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console());

    // 1. Add services to the container.
    builder.Services.AddControllers();

// CHANGE 1: Use Swashbuckle for the Visual UI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. PostgreSQL (Supabase) configuration
var postgresConnectionString = builder.Configuration.GetConnectionString("Supabase")
    ?? throw new InvalidOperationException("Supabase connection string not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
options.UseNpgsql(postgresConnectionString, npgsqlOptions =>
    npgsqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(30),
        errorCodesToAdd: null)));

// 3. Neo4j configuration
var neo4jUri = builder.Configuration["Neo4j:Uri"]
    ?? throw new InvalidOperationException("Neo4j URI not found.");
var neo4jUser = builder.Configuration["Neo4j:User"]
    ?? throw new InvalidOperationException("Neo4j User not found.");
var neo4jPassword = builder.Configuration["Neo4j:Password"]
    ?? throw new InvalidOperationException("Neo4j Password not found.");

var graphClient = new BoltGraphClient(new Uri(neo4jUri), neo4jUser, neo4jPassword);

// CHANGE 2: Async Connection (Prevents Deadlocks)
// We register the client as a Singleton, but we connect immediately below
builder.Services.AddSingleton<IGraphClient>(graphClient);

// 4. Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",                  // Localhost (Development)
            "https://votales-web.vercel.app",         // Vercel Deployment
            "https://votales.app",                    // Your Custom Domain
            "https://www.votales.app"                 // Custom Domain (www)
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials(); // Important for Auth headers
    });
});

// 5. Register application services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITaleService, TaleService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ExceptionHandlingMiddleware>();

// 6. Configure JWT Authentication
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer not found.");
var jwtX = builder.Configuration["Jwt:x"]
    ?? throw new InvalidOperationException("Jwt:x not found.");
var jwtY = builder.Configuration["Jwt:y"]
    ?? throw new InvalidOperationException("Jwt:y not found.");

// Create ECDsa key from Supabase JWK coordinates
var ecdsa = ECDsa.Create(new ECParameters
{
    Curve = ECCurve.NamedCurves.nistP256,
    Q = new ECPoint
    {
        X = Base64UrlDecode(jwtX),
        Y = Base64UrlDecode(jwtY)
    }
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new ECDsaSecurityKey(ecdsa),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Helper function to decode Base64Url
static byte[] Base64UrlDecode(string input)
{
    var output = input.Replace('-', '+').Replace('_', '/');
    switch (output.Length % 4)
    {
        case 2: output += "=="; break;
        case 3: output += "="; break;
    }
    return Convert.FromBase64String(output);
}

// CHANGE 3: Initialize Neo4j Connection safely on startup
// This ensures the connection is ready before the app starts taking requests
using (var scope = app.Services.CreateScope())
{
    var client = scope.ServiceProvider.GetRequiredService<IGraphClient>();
    try
    {
        await client.ConnectAsync();
        Console.WriteLine("✅ Successfully connected to Neo4j!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Could not connect to Neo4j: {ex.Message}");
    }
}

// CHANGE 4: Force Swagger to load in Docker (removed the 'if Development' check)
app.UseSwagger();
app.UseSwaggerUI();

// Add exception handling middleware early in the pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
    app.UseCors("AllowFrontend");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Optional: Add a Root endpoint so you don't see a 404 at the homepage
    app.MapGet("/", () => Results.Redirect("/swagger"));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
