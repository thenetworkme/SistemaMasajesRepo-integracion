using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Services.Implementations;
using SistemaMasajes.Integracion.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Base de datos
builder.Services.AddDbContext<SistemaMasajesContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// HttpClient con configuración para desarrollo
builder.Services.AddHttpClient<ICoreService, CoreService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiUrls:Core"]);
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    // Solo para desarrollo - ignora certificados SSL inválidos
    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
});

// Registrar servicios
builder.Services.AddScoped<ICoreService, CoreService>();
builder.Services.AddScoped<IClienteDbService, ClienteDbService>();

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

//app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();