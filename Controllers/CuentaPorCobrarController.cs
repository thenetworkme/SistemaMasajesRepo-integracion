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
    public class CuentaPorCobrarController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public CuentaPorCobrarController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        // GET: api/CuentaPorCobrar
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetCuentas()
        {
            try
            {
                var cuentas = await _coreService.GetAsync<List<CuentaPorCobrar>>("CuentaPorCobrar");
                return Ok(cuentas);
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

        // GET: api/CuentaPorCobrar/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<CuentaPorCobrar>> GetCuentaPorCobrar(int id)
        {
            try
            {
                var cuenta = await _coreService.GetAsync<CuentaPorCobrar>($"CuentaPorCobrar/{id}");

                if (cuenta == null)
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id}");

                return Ok(cuenta);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id}");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // GET: api/CuentaPorCobrar/estado/{estado}
        [HttpGet("estado/{estado}")]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetByEstado(string estado)
        {
            if (estado.ToLower() != "pagado" && estado.ToLower() != "pendiente")
                return BadRequest("Estado inválido. Use 'pagado' o 'pendiente'.");

            try
            {
                var cuentas = await _coreService.GetAsync<List<CuentaPorCobrar>>($"CuentaPorCobrar/estado/{estado}");
                return Ok(cuentas);
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

        // POST: api/CuentaPorCobrar
        [HttpPost]
        public async Task<IActionResult> PostCuentaPorCobrar([FromBody] CuentaPorCobrar cuentaPorCobrar)
        {
            if (cuentaPorCobrar == null)
                return BadRequest("Datos de cuenta por cobrar inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Guardar en BD local
                _context.CuentasPorCobrar.Add(cuentaPorCobrar);
                await _context.SaveChangesAsync();

                // 2. Enviar al servicio Core
                var resultado = await _coreService.PostAsync<CuentaPorCobrar>("CuentaPorCobrar", cuentaPorCobrar);

                // 3. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Cuenta por cobrar creada correctamente", data = resultado });
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

        // PUT: api/CuentaPorCobrar/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCuentaPorCobrar(int id, [FromBody] CuentaPorCobrar cuentaPorCobrar)
        {
            if (cuentaPorCobrar == null || id != cuentaPorCobrar.Id)
                return BadRequest("ID de cuenta por cobrar inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var cuentaExistente = await _context.CuentasPorCobrar.FindAsync(id);
                if (cuentaExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en BD local");
                }

                // 2. Actualizar en BD local
                _context.Entry(cuentaExistente).CurrentValues.SetValues(cuentaPorCobrar);
                await _context.SaveChangesAsync();

                // 3. Enviar al servicio Core
                var resultado = await _coreService.PutAsync<CuentaPorCobrar>($"CuentaPorCobrar/{id}", cuentaPorCobrar);

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Cuenta por cobrar actualizada correctamente", data = resultado });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // PUT: api/CuentaPorCobrar/{id}/pagar
        [HttpPut("{id}/pagar")]
        public async Task<IActionResult> ActualizarPago(int id, [FromBody] bool pagado)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var cuentaExistente = await _context.CuentasPorCobrar.FindAsync(id);
                if (cuentaExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en BD local");
                }

                // 2. Actualizar estado de pago en BD local
                cuentaExistente.Pagado = pagado;
                await _context.SaveChangesAsync();

                // 3. Enviar al servicio Core
                var pagoData = new { Pagado = pagado };
                await _coreService.PutAsync<object>($"CuentaPorCobrar/{id}/pagar", pagoData);

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Estado de pago actualizado correctamente" });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // DELETE: api/CuentaPorCobrar/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCuentaPorCobrar(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var cuentaExistente = await _context.CuentasPorCobrar.FindAsync(id);
                if (cuentaExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en BD local");
                }

                // 2. Eliminar de BD local
                _context.CuentasPorCobrar.Remove(cuentaExistente);
                await _context.SaveChangesAsync();

                // 3. Eliminar del servicio Core
                await _coreService.DeleteAsync($"CuentaPorCobrar/{id}");

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Cuenta por cobrar eliminada correctamente" });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // Método adicional para obtener cuentas por cobrar desde BD local
        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetCuentasLocal()
        {
            try
            {
                var cuentas = await _context.CuentasPorCobrar
                    .Include(c => c.ClienteId) // Incluir datos del cliente si es necesario
                    .ToListAsync();
                return Ok(cuentas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener cuentas por cobrar de BD local: {ex.Message}");
            }
        }

        // Método adicional para obtener cuentas por cobrar por estado desde BD local
        [HttpGet("local/estado/{estado}")]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetCuentasByEstadoLocal(string estado)
        {
            if (estado.ToLower() != "pagado" && estado.ToLower() != "pendiente")
                return BadRequest("Estado inválido. Use 'pagado' o 'pendiente'.");

            try
            {
                bool pagado = estado.ToLower() == "pagado";
                var cuentas = await _context.CuentasPorCobrar
                    .Include(c => c.ClienteId)
                    .Where(c => c.Pagado == pagado)
                    .ToListAsync();
                return Ok(cuentas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener cuentas por cobrar por estado desde BD local: {ex.Message}");
            }
        }

        // Método adicional para obtener cuentas por cobrar por cliente desde BD local
        [HttpGet("local/cliente/{clienteId}")]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetCuentasByClienteLocal(int clienteId)
        {
            try
            {
                var cuentas = await _context.CuentasPorCobrar
                    .Include(c => c.ClienteId)
                    .Where(c => c.ClienteId == clienteId)
                    .ToListAsync();
                return Ok(cuentas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener cuentas por cobrar del cliente desde BD local: {ex.Message}");
            }
        }
    }
}