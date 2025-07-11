using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using SistemaMasajes.Integracion.Services.BackgroundSync; // Add this using directive

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HistorialDelSistemaController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue; // Inject ISyncQueue

        public HistorialDelSistemaController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue) // Add ISyncQueue to constructor
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue; // Assign ISyncQueue
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<HistorialDelSistema>>> GetHistorial()
        {
            try
            {
                var historial = await _coreService.GetAsync<List<HistorialDelSistema>>("HistorialDelSistema");
                Console.WriteLine("Historial del Sistema obtenido del servicio Core.");
                return Ok(historial);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Historial. Obteniendo de BD local: {ex.Message}");
                var historialLocal = await _context.HistorialSistema.ToListAsync(); // Fallback to local DB
                return Ok(historialLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener historial: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<HistorialDelSistema>> GetHistorialPorId(int id)
        {
            try
            {
                var item = await _coreService.GetAsync<HistorialDelSistema>($"HistorialDelSistema/{id}");
                if (item == null)
                {
                    Console.WriteLine($"Core no devolvió historial con ID {id}. Verificando en BD local.");
                    var itemLocalFallback = await _context.HistorialSistema.FindAsync(id); // Fallback to local DB
                    if (itemLocalFallback == null)
                    {
                        return NotFound($"No se encontró el historial con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(itemLocalFallback);
                }
                Console.WriteLine($"Historial con ID {id} obtenido del servicio Core.");
                return Ok(item);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Historial/{id}. Obteniendo de BD local: {ex.Message}");
                var itemLocal = await _context.HistorialSistema.FindAsync(id); // Fallback to local DB
                if (itemLocal == null)
                    return NotFound($"No se encontró el historial con ID {id} en la BD local.");
                return Ok(itemLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener historial con ID {id}: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostHistorial([FromBody] HistorialDelSistema historial)
        {
            if (historial == null)
                return BadRequest("Datos del historial inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Save to local DB first
                _context.HistorialSistema.Add(historial);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Historial del Sistema con ID {historial.Id} guardado localmente.");

                // 2. Attempt to send to Core service
                try
                {
                    var resultadoCore = await _coreService.PostAsync<HistorialDelSistema>("HistorialDelSistema", historial);
                    Console.WriteLine("Historial del Sistema enviado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Evento de historial registrado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar historial al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<HistorialDelSistema>("HistorialDelSistema", historial, "POST"); // Enqueue for synchronization
                    Console.WriteLine($"Historial del Sistema con ID {historial.Id} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Evento de historial procesado. Guardado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar historial al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<HistorialDelSistema>("HistorialDelSistema", historial, "POST"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Historial del Sistema con ID {historial.Id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Evento de historial procesado. Guardado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al crear historial: {ex.Message}");
            }
        }

        // DELETE: HistorialDelSistema is typically append-only, but including for completeness based on original code.
        // Adapt logic if HistorialDelSistemaId is auto-generated and not passed in the request for deletion from Core.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHistorial(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verify and delete from local DB
                var historial = await _context.HistorialSistema.FindAsync(id);
                if (historial == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el historial con ID {id} en BD local para eliminar.");
                }

                _context.HistorialSistema.Remove(historial);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Historial del Sistema con ID {id} eliminado localmente.");

                // 2. Attempt to delete from Core service
                try
                {
                    await _coreService.DeleteAsync($"HistorialDelSistema/{id}");
                    Console.WriteLine("Historial del Sistema eliminado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Historial del Sistema eliminado correctamente" });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar historial del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"HistorialDelSistema/{id}", null, "DELETE"); // Enqueue for synchronization (using object for null body)
                    Console.WriteLine($"Historial del Sistema con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Historial del Sistema procesado. Eliminado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar historial del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"HistorialDelSistema/{id}", null, "DELETE"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Historial del Sistema con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Historial del Sistema procesado. Eliminado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al eliminar historial: {ex.Message}");
            }
        }

        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<HistorialDelSistema>>> GetHistorialLocal()
        {
            try
            {
                var historial = await _context.HistorialSistema.ToListAsync();
                return Ok(historial);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener historial local: {ex.Message}");
            }
        }
    }
}