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
    public class FacturaDetalleController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue; // Inject ISyncQueue

        public FacturaDetalleController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue) // Add ISyncQueue to constructor
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue; // Assign ISyncQueue
        }

        // GET: api/FacturaDetalle
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetDetalles()
        {
            try
            {
                var detalles = await _coreService.GetAsync<List<FacturaDetalle>>("FacturaDetalle");
                Console.WriteLine("Detalles de Factura obtenidos del servicio Core.");
                return Ok(detalles);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Detalles de Factura. Obteniendo de BD local: {ex.Message}");
                var detallesLocal = await _context.FacturaDetalles.ToListAsync(); // Fallback to local DB
                return Ok(detallesLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener detalles de factura: {ex.Message}");
            }
        }

        // GET: api/FacturaDetalle/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<FacturaDetalle>> GetDetalle(int id)
        {
            try
            {
                var detalle = await _coreService.GetAsync<FacturaDetalle>($"FacturaDetalle/{id}");
                if (detalle == null)
                {
                    Console.WriteLine($"Core no devolvió detalle de factura con ID {id}. Verificando en BD local.");
                    var detalleLocalFallback = await _context.FacturaDetalles.FindAsync(id); // Fallback to local DB
                    if (detalleLocalFallback == null)
                    {
                        return NotFound($"No se encontró el detalle de factura con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(detalleLocalFallback);
                }
                Console.WriteLine($"Detalle de Factura con ID {id} obtenido del servicio Core.");
                return Ok(detalle);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Detalle de Factura/{id}. Obteniendo de BD local: {ex.Message}");
                var detalleLocal = await _context.FacturaDetalles.FindAsync(id); // Fallback to local DB
                if (detalleLocal == null)
                    return NotFound($"No se encontró el detalle de factura con ID {id} en la BD local.");
                return Ok(detalleLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener detalle de factura con ID {id}: {ex.Message}");
            }
        }

        // GET: api/FacturaDetalle/factura/{facturaId}
        [HttpGet("factura/{facturaId}")]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetDetallesByFactura(int facturaId)
        {
            try
            {
                var detalles = await _coreService.GetAsync<List<FacturaDetalle>>($"FacturaDetalle/factura/{facturaId}");
                Console.WriteLine($"Detalles de Factura para FacturaID {facturaId} obtenidos del servicio Core.");
                return Ok(detalles);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Detalles por FacturaID. Obteniendo de BD local: {ex.Message}");
                var detallesLocal = await _context.FacturaDetalles.Where(fd => fd.FacturaId == facturaId).ToListAsync(); // Fallback to local DB
                if (detallesLocal == null || detallesLocal.Count == 0)
                {
                    return NotFound($"No se encontraron detalles para la factura con ID {facturaId} ni en Core ni en BD local.");
                }
                return Ok(detallesLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener detalles de factura por ID de factura: {ex.Message}");
            }
        }

        // POST: api/FacturaDetalle
        [HttpPost]
        public async Task<IActionResult> PostDetalle([FromBody] FacturaDetalle detalle)
        {
            if (detalle == null)
                return BadRequest("Datos de detalle inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Save to local DB first
                _context.FacturaDetalles.Add(detalle);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Detalle de Factura con ID {detalle.Id} guardado localmente."); // Assuming 'Id' is the primary key

                // 2. Attempt to send to Core service
                try
                {
                    var resultadoCore = await _coreService.PostAsync<FacturaDetalle>("FacturaDetalle", detalle);
                    Console.WriteLine("Detalle de Factura enviado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Detalle de factura creado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar detalle de factura al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<FacturaDetalle>("FacturaDetalle", detalle, "POST"); // Enqueue for synchronization
                    Console.WriteLine($"Detalle de Factura con ID {detalle.Id} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Detalle de factura procesado. Guardado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar detalle de factura al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<FacturaDetalle>("FacturaDetalle", detalle, "POST"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Detalle de Factura con ID {detalle.Id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Detalle de factura procesado. Guardado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al crear detalle de factura: {ex.Message}");
            }
        }

        // PUT: api/FacturaDetalle/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDetalle(int id, [FromBody] FacturaDetalle detalle)
        {
            // IMPORTANT: Assuming 'Id' is the primary key for FacturaDetalle.
            if (detalle == null || id != detalle.Id)
                return BadRequest("ID de detalle inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verify and update in local DB
                var detalleExistente = await _context.FacturaDetalles.FindAsync(id);
                if (detalleExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el detalle con ID {id} en BD local para actualizar.");
                }

                _context.Entry(detalleExistente).CurrentValues.SetValues(detalle);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Detalle de Factura con ID {id} actualizado localmente.");

                // 2. Attempt to send to Core service
                try
                {
                    var resultadoCore = await _coreService.PutAsync<FacturaDetalle>($"FacturaDetalle/{id}", detalle);
                    Console.WriteLine("Detalle de Factura actualizado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Detalle de factura actualizado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar detalle de factura en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<FacturaDetalle>($"FacturaDetalle/{id}", detalle, "PUT"); // Enqueue for synchronization
                    Console.WriteLine($"Detalle de Factura con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Detalle de factura procesado. Actualizado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar detalle de factura en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<FacturaDetalle>($"FacturaDetalle/{id}", detalle, "PUT"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Detalle de Factura con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Detalle de factura procesado. Actualizado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al actualizar detalle de factura: {ex.Message}");
            }
        }

        // DELETE: api/FacturaDetalle/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDetalle(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verify and delete from local DB
                var detalleExistente = await _context.FacturaDetalles.FindAsync(id);
                if (detalleExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el detalle con ID {id} en BD local para eliminar.");
                }

                _context.FacturaDetalles.Remove(detalleExistente);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Detalle de Factura con ID {id} eliminado localmente.");

                // 2. Attempt to delete from Core service
                try
                {
                    await _coreService.DeleteAsync($"FacturaDetalle/{id}");
                    Console.WriteLine("Detalle de Factura eliminado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Detalle de factura eliminado correctamente" });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar detalle de factura del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"FacturaDetalle/{id}", null, "DELETE"); // Enqueue for synchronization (using object for null body)
                    Console.WriteLine($"Detalle de Factura con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Detalle de factura procesado. Eliminado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar detalle de factura del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"FacturaDetalle/{id}", null, "DELETE"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Detalle de Factura con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Detalle de factura procesado. Eliminado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al eliminar detalle de factura: {ex.Message}");
            }
        }

        // EXTRA: Obtener detalles locales
        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetDetallesLocales()
        {
            try
            {
                // Consider eager loading related entities if needed, e.g., Include(fd => fd.SomeRelatedProperty)
                var detalles = await _context.FacturaDetalles.ToListAsync();
                return Ok(detalles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener datos de BD local: {ex.Message}");
            }
        }
    }
}