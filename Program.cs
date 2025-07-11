using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Services;
using SistemaMasajes.Integracion.Services.Implementations;
using SistemaMasajes.Integracion.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Servicios MVC / Swagger / Lambda
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// Base de datos
builder.Services.AddDbContext<SistemaMasajesContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// HttpClient para Core
builder.Services.AddHttpClient<ICoreService, CoreService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiUrls:Core"]);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
});

// Servicio de acceso local
builder.Services.AddScoped<IClienteDbService, ClienteDbService>();

// <<< Aquí registras tu BackgroundService de sincronización >>>
//builder.Services.AddSyncBackgroundService();

// CORS
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

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapGet("/health", () => "API is running");
app.MapControllers();

app.Run();
