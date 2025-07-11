//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using SistemaMasajes.Integracion.Data;
//using SistemaMasajes.Integracion.Models.Entities;
//using SistemaMasajes.Integracion.Services.Interfaces;
//using System;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//namespace SistemaMasajes.Integracion.Services
//{
//    public class SyncBackgroundService : BackgroundService
//    {
//        private readonly IServiceProvider _serviceProvider;
//        private readonly ILogger<SyncBackgroundService> _logger;
//        private readonly int _intervaloSincronizacion = 30000; // 30 segundos

//        public SyncBackgroundService(
//            IServiceProvider serviceProvider,
//            ILogger<SyncBackgroundService> logger)
//        {
//            _serviceProvider = serviceProvider;
//            _logger = logger;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    using var scope = _serviceProvider.CreateScope();
//                    var context = scope.ServiceProvider.GetRequiredService<SistemaMasajesContext>();
//                    var coreService = scope.ServiceProvider.GetRequiredService<ICoreService>();

//                    await SincronizarDatosPendientes(context, coreService);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Error durante la sincronización automática");
//                }

//                await Task.Delay(_intervaloSincronizacion, stoppingToken);
//            }
//        }

//        private async Task SincronizarDatosPendientes(SistemaMasajesContext context, ICoreService coreService)
//        {
//            try
//            {
//                var clientesPendientes = await context.Clientes
//                    .Where(c => c.PendienteDeSincronizacion)
//                    .ToListAsync();

//                if (!clientesPendientes.Any())
//                {
//                    _logger.LogInformation("No hay clientes pendientes de sincronización");
//                    return;
//                }

//                _logger.LogInformation($"Iniciando sincronización de {clientesPendientes.Count} clientes");

//                foreach (var cliente in clientesPendientes)
//                {
//                    try
//                    {
//                        if (cliente.EstaEliminado)
//                        {
//                            await coreService.DeleteAsync($"Clientes/{cliente.Id}");
//                            context.Clientes.Remove(cliente);
//                            _logger.LogInformation($"Cliente {cliente.Id} eliminado del Core y BD local");
//                        }
//                        else
//                        {
//                            if (cliente.FechaRegistro == cliente.FechaModificacion)
//                            {
//                                // Es creación
//                                await coreService.PostAsync<Cliente>("Clientes", cliente);
//                                _logger.LogInformation($"Cliente {cliente.Id} creado en Core");
//                            }
//                            else
//                            {
//                                // Es actualización
//                                await coreService.PutAsync<Cliente>($"Clientes/{cliente.Id}", cliente);
//                                _logger.LogInformation($"Cliente {cliente.Id} actualizado en Core");
//                            }

//                            cliente.EsSincronizado = true;
//                            cliente.PendienteDeSincronizacion = false;
//                            cliente.FechaUltimaSync = DateTime.UtcNow;
//                        }
//                    }
//                    catch (HttpRequestException ex)
//                    {
//                        _logger.LogWarning($"Core no disponible para cliente {cliente.Id}: {ex.Message}");
//                        // Mantener como pendiente para próximo intento
//                        break; // Salir del bucle si Core no está disponible
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogError(ex, $"Error sincronizando cliente {cliente.Id}");
//                    }
//                }

//                await context.SaveChangesAsync();
//                _logger.LogInformation("Sincronización completada");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error general en sincronización");
//            }
//        }
//    }

//    // Extensión para registrar el servicio
//    public static class SyncBackgroundServiceExtensions
//    {
//        public static IServiceCollection AddSyncBackgroundService(this IServiceCollection services)
//        {
//            services.AddHostedService<SyncBackgroundService>();
//            return services;
//        }
//    }
//}