using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection; // Para CreateScope
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Services.BackgroundSync; // Asegúrate de que esta carpeta existe
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace SistemaMasajes.Integracion.Services.BackgroundSync
{
    // ISyncQueue y SyncQueue deben estar definidos como en la respuesta anterior
    // ICoreService debe tener las firmas actualizadas

    public class CoreSyncBackgroundService : BackgroundService
    {
        private readonly ISyncQueue _syncQueue;
        private readonly IServiceProvider _serviceProvider; // Para crear scopes para servicios con scoped lifetime

        public CoreSyncBackgroundService(ISyncQueue syncQueue, IServiceProvider serviceProvider)
        {
            _syncQueue = syncQueue;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Servicio de Sincronización en segundo plano iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Espera por un elemento en la cola
                    var (endpoint, data, httpMethod) = await _syncQueue.DequeueAsync(stoppingToken);

                    if (data == null) continue; // Si se cancela o Dequeue devuelve null

                    // Creamos un scope para obtener servicios con Scoped lifetime (como DbContext o ICoreService si está Scoped)
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var coreService = scope.ServiceProvider.GetRequiredService<ICoreService>();

                        Console.WriteLine($"Intentando sincronizar {httpMethod} a {endpoint} con datos: {JsonSerializer.Serialize(data)}");

                        try
                        {
                            switch (httpMethod.ToUpper())
                            {
                                case "POST":
                                    await coreService.PostAsync(endpoint, data); // No esperamos un resultado aquí
                                    break;
                                case "PUT":
                                    await coreService.PutAsync(endpoint, data); // No esperamos un resultado aquí
                                    break;
                                case "DELETE":
                                    await coreService.DeleteAsync(endpoint); // No esperamos un resultado aquí
                                    break;
                                default:
                                    Console.WriteLine($"Método HTTP {httpMethod} no soportado para sincronización. Descartando operación.");
                                    break;
                            }
                           
                            Console.WriteLine($"✅ Sincronización exitosa para {httpMethod} a {endpoint}. Datos enviados al Core.");
                            // Aquí es donde, en una solución más avanzada, podrías actualizar
                            // el estado de sincronización en tu BD local para el elemento
                            // original (si has implementado un ID de correlación o estado).
                        }
                        catch (HttpRequestException ex)
                        {
                            Console.WriteLine($"Fallo la sincronización con Core para {endpoint}: {ex.Message}. Reencolando para reintentar.");
                            _syncQueue.Enqueue(endpoint, data, httpMethod); // Reencola para reintentar
                            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Espera antes de procesar el siguiente o reintentar
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error inesperado durante la sincronización para {endpoint}: {ex.Message}. Descartando operación.");
                            // Considera si este tipo de error justifica reencolar o si es un error fatal
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Esto ocurre cuando el servicio se detiene
                    Console.WriteLine("Servicio de Sincronización en segundo plano cancelado.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error general en el servicio de sincronización: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Pequeña espera en caso de error para evitar loops rápidos
                }
            }
        }
    }
}