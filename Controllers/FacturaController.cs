using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Models.Entities;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacturaController : ControllerBase
    {
        private readonly SistemaMasajesContext _context;

        public FacturaController(SistemaMasajesContext context)
        {
            _context = context;
        }

        // GET: api/Factura
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Factura>>> GetFacturas()
        {
            var facturas = await _context.Facturas
                .Include(f => f.Cliente)
                .Include(f => f.Detalles)
                .ToListAsync();

            return Ok(facturas);
        }

        // GET: api/Factura/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Factura>> GetFactura(int id)
        {
            var factura = await _context.Facturas
                .Include(f => f.Cliente)
                .Include(f => f.Detalles)
                .FirstOrDefaultAsync(f => f.FacturaId == id);

            if (factura == null)
                return NotFound($"Factura con ID {id} no encontrada.");

            return Ok(factura);
        }

        // POST: api/Factura
        [HttpPost]
        public async Task<IActionResult> PostFactura([FromBody] Factura factura)
        {
            using var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();

            using var transaction = await conn.BeginTransactionAsync();
            try
            {
                // Insertar factura principal
                using var cmdFactura = conn.CreateCommand();
                cmdFactura.Transaction = transaction;
                cmdFactura.CommandText = "sp_InsertarFactura";
                cmdFactura.CommandType = CommandType.StoredProcedure;

                cmdFactura.Parameters.Add(new SqlParameter("@ClienteId", factura.ClienteId));
                cmdFactura.Parameters.Add(new SqlParameter("@Fecha", factura.Fecha));
                cmdFactura.Parameters.Add(new SqlParameter("@Total", factura.Total));
                cmdFactura.Parameters.Add(new SqlParameter("@TipoPago", factura.TipoPago));

                var outputIdParam = new SqlParameter("@NuevoId", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };
                cmdFactura.Parameters.Add(outputIdParam);

                await cmdFactura.ExecuteNonQueryAsync();
                int nuevoFacturaId = (int)outputIdParam.Value;

                // Insertar detalles
                foreach (var detalle in factura.Detalles)
                {
                    using var cmdDetalle = conn.CreateCommand();
                    cmdDetalle.Transaction = transaction;
                    cmdDetalle.CommandText = "sp_InsertarFacturaDetalle";
                    cmdDetalle.CommandType = CommandType.StoredProcedure;

                    cmdDetalle.Parameters.Add(new SqlParameter("@FacturaId", nuevoFacturaId));
                    cmdDetalle.Parameters.Add(new SqlParameter("@Tipo", detalle.Tipo));
                    cmdDetalle.Parameters.Add(new SqlParameter("@NombreItem", detalle.NombreItem));
                    cmdDetalle.Parameters.Add(new SqlParameter("@PrecioUnitario", detalle.PrecioUnitario));
                    cmdDetalle.Parameters.Add(new SqlParameter("@Cantidad", detalle.Cantidad));
                    cmdDetalle.Parameters.Add(new SqlParameter("@Subtotal", detalle.Subtotal));

                    await cmdDetalle.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return Ok(new { FacturaId = nuevoFacturaId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error al insertar factura: {ex.Message}");
            }
        }

        // PUT: api/Factura/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutFactura(int id, [FromBody] Factura factura)
        {
            if (id != factura.FacturaId)
                return BadRequest("El ID de la factura no coincide.");

            var facturaExistente = await _context.Facturas.FindAsync(id);
            if (facturaExistente == null)
                return NotFound($"Factura con ID {id} no encontrada.");

            // Actualización simple sin SP (puedes adaptarlo a SP si tienes uno)
            facturaExistente.ClienteId = factura.ClienteId;
            facturaExistente.Fecha = factura.Fecha;
            facturaExistente.Total = factura.Total;
            facturaExistente.TipoPago = factura.TipoPago;

            await _context.SaveChangesAsync();
            return Ok("Factura actualizada correctamente.");
        }

        // DELETE: api/Factura/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFactura(int id)
        {
            var factura = await _context.Facturas.FindAsync(id);
            if (factura == null)
                return NotFound($"Factura con ID {id} no encontrada.");

            _context.Facturas.Remove(factura);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
