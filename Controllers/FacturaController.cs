using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacturaController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public FacturaController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        // GET: api/Factura
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Factura>>> GetFacturas()
        {
            try
            {
                var facturas = await _coreService.GetAsync<List<Factura>>("Factura");
                return Ok(facturas);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                    return NotFound($"No se encontró la factura con ID {id}");

                return Ok(factura);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la factura con ID {id}");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // GET: api/Factura/{id}/detalles
        [HttpGet("{id}/detalles")]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetFacturaDetalles(int id)
        {
            try
            {
                var detalles = await _coreService.GetAsync<List<FacturaDetalle>>($"Factura/{id}/detalles");
                return Ok(detalles);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la factura con ID {id}");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                // 1. Guardar en BD local
                _context.Facturas.Add(factura);
                await _context.SaveChangesAsync();

                // 2. Enviar al servicio Core
                var resultado = await _coreService.PostAsync<Factura>("Factura", factura);

                // 3. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Factura creada correctamente", data = resultado });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                // 1. Verificar que existe en BD local
                var facturaExistente = await _context.Facturas.FindAsync(id);
                if (facturaExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró la factura con ID {id} en BD local");
                }

                // 2. Actualizar en BD local
                _context.Entry(facturaExistente).CurrentValues.SetValues(factura);
                await _context.SaveChangesAsync();

                // 3. Enviar al servicio Core
                var resultado = await _coreService.PutAsync<Factura>($"Factura/{id}", factura);

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Factura actualizada correctamente", data = resultado });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la factura con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // DELETE: api/Factura/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFactura(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var facturaExistente = await _context.Facturas.FindAsync(id);
                if (facturaExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró la factura con ID {id} en BD local");
                }

                // 2. Eliminar de BD local
                _context.Facturas.Remove(facturaExistente);
                await _context.SaveChangesAsync();

                // 3. Eliminar del servicio Core
                await _coreService.DeleteAsync($"Factura/{id}");

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Factura eliminada correctamente" });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la factura con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                    .Include(f => f.FacturaId) // Incluir detalles de la factura
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
                    .Include(f => f.FacturaId)
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
                    .Include(f => f.FacturaId)
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
                    .Include(fd => fd.FacturaId) // Incluir datos del servicio si es necesario
                    .Where(fd => fd.FacturaId == id)
                    .ToListAsync();

                if (detalles == null || detalles.Count == 0)
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
                    .Include(f => f.FacturaId)
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