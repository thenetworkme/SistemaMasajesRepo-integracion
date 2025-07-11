using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using SistemaMasajes.Integracion.Services.BackgroundSync;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicioController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue;

        public ServicioController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue)
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Servicio>>> GetServicios()
        {
            try
            {
                var servicios = await _coreService.GetAsync<List<Servicio>>("Servicio");
                return Ok(servicios);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetServicios. Obteniendo de BD local: {ex.Message}");
                var serviciosLocal = await _context.Servicios.ToListAsync();
                return Ok(serviciosLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener servicios: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Servicio>> GetServicioById(int id)
        {
            try
            {
                var servicio = await _coreService.GetAsync<Servicio>($"Servicio/{id}");
                if (servicio == null)
                {
                    Console.WriteLine($"Core no devolvió servicio con ID {id}. Verificando en BD local.");
                    var servicioLocalFallback = await _context.Servicios.FindAsync(id);
                    if (servicioLocalFallback == null)
                    {
                        return NotFound($"No se encontró el servicio con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(servicioLocalFallback);
                }
                return Ok(servicio);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetServicioById/{id}. Obteniendo de BD local: {ex.Message}");
                var servicioLocal = await _context.Servicios.FindAsync(id);
                if (servicioLocal == null)
                    return NotFound($"No se encontró el servicio con ID {id} en la BD local.");
                return Ok(servicioLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener servicio con ID {id}: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostServicio([FromBody] Servicio servicio)
        {
            if (servicio == null)
                return BadRequest("Datos de servicio inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Servicios.Add(servicio);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Servicio con ID {servicio.ServicioId} guardado localmente.");

                try
                {
                    var resultadoCore = await _coreService.PostAsync<Servicio>("Servicio", servicio);
                    Console.WriteLine("Servicio enviado y confirmado por el servicio Core.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Servicio creado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar servicio al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Servicio>("Servicio", servicio, "POST");
                    Console.WriteLine($"Servicio con ID {servicio.ServicioId} encolado para sincronización.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Servicio procesado. Guardado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar servicio al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Servicio>("Servicio", servicio, "POST");
                    Console.WriteLine($"Servicio con ID {servicio.ServicioId} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Servicio procesado. Guardado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al crear servicio: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutServicio(int id, [FromBody] Servicio servicio)
        {
            if (servicio == null || id != servicio.ServicioId)
                return BadRequest("ID de servicio inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existente = await _context.Servicios.FindAsync(id);
                if (existente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el servicio con ID {id} en BD local para actualizar.");
                }

                _context.Entry(existente).CurrentValues.SetValues(servicio);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Servicio con ID {id} actualizado localmente.");

                try
                {
                    var resultadoCore = await _coreService.PutAsync<Servicio>($"Servicio/{id}", servicio);
                    Console.WriteLine("Servicio actualizado y confirmado por el servicio Core.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Servicio actualizado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar servicio en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Servicio>($"Servicio/{id}", servicio, "PUT");
                    Console.WriteLine($"Servicio con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Servicio procesado. Actualizado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar servicio en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Servicio>($"Servicio/{id}", servicio, "PUT");
                    Console.WriteLine($"Servicio con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Servicio procesado. Actualizado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al actualizar servicio: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteServicio(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var servicio = await _context.Servicios.FindAsync(id);
                if (servicio == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el servicio con ID {id} en BD local para eliminar.");
                }

                _context.Servicios.Remove(servicio);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Servicio con ID {id} eliminado localmente.");

                try
                {
                    await _coreService.DeleteAsync($"Servicio/{id}");
                    Console.WriteLine("Servicio eliminado y confirmado por el servicio Core.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Servicio eliminado correctamente" });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar servicio del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Servicio/{id}", null, "DELETE");
                    Console.WriteLine($"Servicio con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Servicio procesado. Eliminado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar servicio del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Servicio/{id}", null, "DELETE");
                    Console.WriteLine($"Servicio con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Servicio procesado. Eliminado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al eliminar servicio: {ex.Message}");
            }
        }

        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Servicio>>> GetServiciosLocal()
        {
            try
            {
                var servicios = await _context.Servicios.ToListAsync();
                return Ok(servicios);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener servicios locales: {ex.Message}");
            }
        }
    }
}