using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Models.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CuentaPorCobrarController : ControllerBase
    {
        private readonly SistemaMasajesContext _context;

        public CuentaPorCobrarController(SistemaMasajesContext context)
        {
            _context = context;
        }

        // GET: api/CuentaPorCobrar
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetCuentas()
        {
            var cuentas = await _context.CuentasPorCobrar
                .FromSqlRaw("EXEC sp_ObtenerCuentas")
                .ToListAsync();

            return Ok(cuentas);
        }

        // GET: api/CuentaPorCobrar/estado/{estado}
        [HttpGet("estado/{estado}")]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetByEstado(string estado)
        {
            bool pagadoFiltro = false;
            if (estado.ToLower() == "pagado") pagadoFiltro = true;
            else if (estado.ToLower() != "pendiente") return BadRequest("Estado inválido. Use 'pagado' o 'pendiente'.");

            var cuentas = await _context.CuentasPorCobrar
                .FromSqlInterpolated($"EXEC sp_ObtenerCuentasPorEstado @Pagado = {pagadoFiltro}")
                .ToListAsync();

            return Ok(cuentas);
        }

        // POST: api/CuentaPorCobrar
        [HttpPost]
        public async Task<IActionResult> PostCuentaPorCobrar([FromBody] CuentaPorCobrar cuentaPorCobrar)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_InsertarCuentaPorCobrar 
                    @FacturaId = {cuentaPorCobrar.FacturaId},
                    @MontoPendiente = {cuentaPorCobrar.MontoPendiente},
                    @Pagado = {cuentaPorCobrar.Pagado}");

            return Ok("Cuenta por cobrar registrada correctamente");
        }

        // PUT: api/CuentaPorCobrar/{id}/pagar
        [HttpPut("{id}/pagar")]
        public async Task<IActionResult> ActualizarPago(int id, [FromBody] bool pagado)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_ActualizarEstadoPago 
                    @Id = {id}, 
                    @Pagado = {pagado}");

            return NoContent();
        }

        // DELETE: api/CuentaPorCobrar/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCuentaPorCobrar(int id)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_EliminarCuentaPorCobrar @Id = {id}");

            return NoContent();
        }
    }
}
