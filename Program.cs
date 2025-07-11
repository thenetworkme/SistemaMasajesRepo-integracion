using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Services.Implementations;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Services.BackgroundSync;
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // Add this using directive for ILogger configuration

var builder = WebApplication.CreateBuilder(args);

// --- Configuración de Logging ---
builder.Logging.ClearProviders(); // Clear any default logging providers (optional, but good for control)
builder.Logging.AddConsole();     // Add console logger (logs to console/debug output)
builder.Logging.AddDebug();       // Add debug output logger
// Configure log levels for specific categories
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information); // Logs SQL commands
builder.Logging.AddFilter("SistemaMasajes.Integracion", LogLevel.Information); // Log information and above for your application's namespace
builder.Logging.AddFilter("Microsoft", LogLevel.Warning); // General filter for Microsoft namespaces to reduce verbosity, log Warnings and above

// --- Servicios de la Aplicación ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Si estás usando AWS Lambda, mantén esta línea
//builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// --- Configuración de Base de Datos Local ---
builder.Services.AddDbContext<SistemaMasajesContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Configuración de HttpClient para el Core API ---
builder.Services.AddHttpClient<ICoreService, CoreService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiUrls:Core"]);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Esto es para permitir certificados SSL no válidos (por ejemplo, en desarrollo con HTTPS local)
    // ¡Ten cuidado con esto en producción! Considera configurar certificados válidos.
    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
});

// --- Servicios de la Lógica de Negocio Local ---
// Asumo que IClienteDbService y ClienteDbService manejan operaciones directas con la BD local.
builder.Services.AddScoped<IClienteDbService, ClienteDbService>();

// --- Servicios de Sincronización en Segundo Plano (Independencia) ---
// Registra la cola de sincronización como un Singleton
builder.Services.AddSingleton<ISyncQueue, SyncQueue>();

// Registra el BackgroundService que procesará la cola
builder.Services.AddHostedService<CoreSyncBackgroundService>();

// --- Configuración de CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// --- Pipeline de Solicitudes HTTP ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll"); // Aplica la política CORS
// app.UseHttpsRedirection(); // Habilita si usas HTTPS y quieres forzar redirección
app.UseAuthorization();

// Endpoint de health check simple
app.MapGet("/health", () => "API is running");

// Mapea los controladores
app.MapControllers();

app.Run();