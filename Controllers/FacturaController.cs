using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http; // Added for HttpRequestException
using System; // Added for DateTime
using System.Linq; // Added for .Where() and .Any()
using SistemaMasajes.Integracion.Services.BackgroundSync; // Add this using directive

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacturaController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue; // Inject ISyncQueue

        public FacturaController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue) // Add ISyncQueue to constructor
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue; // Assign ISyncQueue
        }

        // GET: api/Factura
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Factura>>> GetFacturas()
        {
            try
            {
                var facturas = await _coreService.GetAsync<List<Factura>>("Factura");
                Console.WriteLine("Facturas obtenidas del servicio Core.");
                return Ok(facturas);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Facturas. Obteniendo de BD local: {ex.Message}");
                var facturasLocal = await _context.Facturas
                                                .Include(f => f.Cliente) // Include related Cliente if it's a navigation property
                                                .ToListAsync();
                return Ok(facturasLocal); // Fallback to local DB
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener facturas: {ex.Message}");
            }
        }

        // GET: api/Factura/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Factura>> GetFactura(int id)
        {
            try
            {
                var factura = await _coreService.GetAsync<Factura>($"Factura/{id}");

                if (factura == null)
                {
                    Console.WriteLine($"Core no devolvió factura con ID {id}. Verificando en BD local.");
                    var facturaLocalFallback = await _context.Facturas
                                                            .Include(f => f.Cliente)
                                                            .FirstOrDefaultAsync(f => f.FacturaId == id); // Fallback to local DB
                    if (facturaLocalFallback == null)
                    {
                        return NotFound($"No se encontró la factura con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(facturaLocalFallback);
                }
                Console.WriteLine($"Factura con ID {id} obtenida del servicio Core.");
                return Ok(factura);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Factura/{id}. Obteniendo de BD local: {ex.Message}");
                var facturaLocal = await _context.Facturas
                                                .Include(f => f.Cliente)
                                                .FirstOrDefaultAsync(f => f.FacturaId == id); // Fallback to local DB
                if (facturaLocal == null)
                    return NotFound($"No se encontró la factura con ID {id} en la BD local.");
                return Ok(facturaLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener factura con ID {id}: {ex.Message}");
            }
        }

        // GET: api/Factura/{id}/detalles
        [HttpGet("{id}/detalles")]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetFacturaDetalles(int id)
        {
            try
            {
                var detalles = await _coreService.GetAsync<List<FacturaDetalle>>($"Factura/{id}/detalles");
                Console.WriteLine($"Detalles de factura para FacturaID {id} obtenidos del servicio Core.");
                return Ok(detalles);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Detalles de Factura/{id}. Obteniendo de BD local: {ex.Message}");
                var detallesLocal = await _context.FacturaDetalles
                                                .Include(fd => fd.Factura) // Assuming FacturaDetalle has a navigation property to Factura
                                                .Where(fd => fd.FacturaId == id)
                                                .ToListAsync(); // Fallback to local DB
                if (detallesLocal == null || !detallesLocal.Any())
                {
                    return NotFound($"No se encontraron detalles para la factura con ID {id} ni en Core ni en BD local.");
                }
                return Ok(detallesLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener detalles de factura para ID {id}: {ex.Message}");
            }
        }

        // POST: api/Factura
        [HttpPost]
        public async Task<IActionResult> PostFactura([FromBody] Factura factura)
        {
            if (factura == null)
                return BadRequest("Datos de factura inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Save to local DB first
                _context.Facturas.Add(factura);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Factura con ID {factura.FacturaId} guardada localmente.");

                // 2. Attempt to send to Core service
                try
                {
                    var resultadoCore = await _coreService.PostAsync<Factura>("Factura", factura);
                    Console.WriteLine("Factura enviada y confirmada por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Factura creada correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar factura al servicio Core. Guardada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Factura>("Factura", factura, "POST"); // Enqueue for synchronization
                    Console.WriteLine($"Factura con ID {factura.FacturaId} encolada para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Factura procesada. Guardada localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar factura al servicio Core. Guardada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Factura>("Factura", factura, "POST"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Factura con ID {factura.FacturaId} encolada para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Factura procesada. Guardada localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al crear factura: {ex.Message}");
            }
        }

        // PUT: api/Factura/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutFactura(int id, [FromBody] Factura factura)
        {
            if (factura == null || id != factura.FacturaId)
                return BadRequest("ID de factura inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verify and update in local DB
                var facturaExistente = await _context.Facturas.FindAsync(id);
                if (facturaExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró la factura con ID {id} en BD local para actualizar.");
                }

                _context.Entry(facturaExistente).CurrentValues.SetValues(factura);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Factura con ID {id} actualizada localmente.");

                // 2. Attempt to send to Core service
                try
                {
                    var resultadoCore = await _coreService.PutAsync<Factura>($"Factura/{id}", factura);
                    Console.WriteLine("Factura actualizada y confirmada por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Factura actualizada correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar factura en el servicio Core. Actualizada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Factura>($"Factura/{id}", factura, "PUT"); // Enqueue for synchronization
                    Console.WriteLine($"Factura con ID {id} encolada para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Factura procesada. Actualizada localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar factura en el servicio Core. Actualizada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Factura>($"Factura/{id}", factura, "PUT"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Factura con ID {id} encolada para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Factura procesada. Actualizada localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al actualizar factura: {ex.Message}");
            }
        }

        // DELETE: api/Factura/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFactura(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verify and delete from local DB
                var facturaExistente = await _context.Facturas.FindAsync(id);
                if (facturaExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró la factura con ID {id} en BD local para eliminar.");
                }

                _context.Facturas.Remove(facturaExistente);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Factura con ID {id} eliminada localmente.");

                // 2. Attempt to delete from Core service
                try
                {
                    await _coreService.DeleteAsync($"Factura/{id}");
                    Console.WriteLine("Factura eliminada y confirmada por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Factura eliminada correctamente" });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar factura del servicio Core. Eliminada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Factura/{id}", null, "DELETE"); // Enqueue for synchronization (using object for null body)
                    Console.WriteLine($"Factura con ID {id} encolada para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Factura procesada. Eliminada localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar factura del servicio Core. Eliminada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Factura/{id}", null, "DELETE"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Factura con ID {id} encolada para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Factura procesada. Eliminada localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al eliminar factura: {ex.Message}");
            }
        }

        // Método adicional para obtener facturas desde BD local
        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Factura>>> GetFacturasLocal()
        {
            try
            {
                var facturas = await _context.Facturas
                    .Include(f => f.Cliente) // Incluir datos del cliente si es necesario
                                             // Removed: .Include(f => f.FacturaDetalles)
                    .ToListAsync();
                return Ok(facturas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener facturas de BD local: {ex.Message}");
            }
        }

        // Método adicional para obtener facturas por cliente desde BD local
        [HttpGet("local/cliente/{clienteId}")]
        public async Task<ActionResult<IEnumerable<Factura>>> GetFacturasByClienteLocal(int clienteId)
        {
            try
            {
                var facturas = await _context.Facturas
                    .Include(f => f.Cliente)
                    // Removed: .Include(f => f.FacturaDetalles)
                    .Where(f => f.ClienteId == clienteId)
                    .ToListAsync();
                return Ok(facturas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener facturas del cliente desde BD local: {ex.Message}");
            }
        }

        // Método adicional para obtener facturas por fecha desde BD local
        [HttpGet("local/fecha/{fecha}")]
        public async Task<ActionResult<IEnumerable<Factura>>> GetFacturasByFechaLocal(DateTime fecha)
        {
            try
            {
                var facturas = await _context.Facturas
                    .Include(f => f.Cliente)
                    // Removed: .Include(f => f.FacturaDetalles)
                    .Where(f => f.Fecha.Date == fecha.Date)
                    .ToListAsync();
                return Ok(facturas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener facturas por fecha desde BD local: {ex.Message}");
            }
        }

        // Método adicional para obtener detalles de factura desde BD local
        [HttpGet("local/{id}/detalles")]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetFacturaDetallesLocal(int id)
        {
            try
            {
                var detalles = await _context.FacturaDetalles
                    .Include(fd => fd.Factura) // Assuming FacturaDetalle has a navigation property to Factura
                    .Where(fd => fd.FacturaId == id)
                    .ToListAsync();

                if (detalles == null || !detalles.Any())
                    return NotFound($"No se encontraron detalles para la factura con ID {id} en BD local");

                return Ok(detalles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener detalles de factura desde BD local: {ex.Message}");
            }
        }

        // Método adicional para obtener facturas por rango de fechas desde BD local
        [HttpGet("local/rango")]
        public async Task<ActionResult<IEnumerable<Factura>>> GetFacturasByRangoFechasLocal(
            [FromQuery] DateTime fechaInicio,
            [FromQuery] DateTime fechaFin)
        {
            try
            {
                var facturas = await _context.Facturas
                    .Include(f => f.Cliente)
                    // Removed: .Include(f => f.FacturaDetalles)
                    .Where(f => f.Fecha.Date >= fechaInicio.Date && f.Fecha.Date <= fechaFin.Date)
                    .ToListAsync();
                return Ok(facturas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener facturas por rango de fechas desde BD local: {ex.Message}");
            }
        }
    }
}