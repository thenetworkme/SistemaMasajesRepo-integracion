using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http; // Necesario para HttpRequestException
using System;
using SistemaMasajes.Integracion.Services.BackgroundSync; // Asegúrate de tener este using para ISyncQueue

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CuentaPorCobrarController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue; // Inyecta ISyncQueue

        public CuentaPorCobrarController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue) // Agrega ISyncQueue al constructor
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue; // Asigna ISyncQueue
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetCuentas()
        {
            try
            {
                var cuentas = await _coreService.GetAsync<List<CuentaPorCobrar>>("CuentaPorCobrar");
                Console.WriteLine("Cuentas por cobrar obtenidas del servicio Core.");
                return Ok(cuentas);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetCuentas. Obteniendo de BD local: {ex.Message}");
                var cuentasLocal = await _context.CuentasPorCobrar.ToListAsync(); // Fallback a BD local
                return Ok(cuentasLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener cuentas por cobrar: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CuentaPorCobrar>> GetCuentaPorCobrar(int id)
        {
            try
            {
                var cuenta = await _coreService.GetAsync<CuentaPorCobrar>($"CuentaPorCobrar/{id}");

                if (cuenta == null)
                {
                    Console.WriteLine($"Core no devolvió cuenta por cobrar con ID {id}. Verificando en BD local.");
                    var cuentaLocalFallback = await _context.CuentasPorCobrar.FindAsync(id); // Fallback a BD local
                    if (cuentaLocalFallback == null)
                    {
                        return NotFound($"No se encontró la cuenta por cobrar con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(cuentaLocalFallback);
                }
                Console.WriteLine($"Cuenta por cobrar con ID {id} obtenida del servicio Core.");
                return Ok(cuenta);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetCuentaPorCobrar/{id}. Obteniendo de BD local: {ex.Message}");
                var cuentaLocal = await _context.CuentasPorCobrar.FindAsync(id); // Fallback a BD local
                if (cuentaLocal == null)
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en la BD local.");
                return Ok(cuentaLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener cuenta por cobrar con ID {id}: {ex.Message}");
            }
        }

        [HttpGet("estado/{estado}")]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetByEstado(string estado)
        {
            if (estado.ToLower() != "pagado" && estado.ToLower() != "pendiente")
                return BadRequest("Estado inválido. Use 'pagado' o 'pendiente'.");

            try
            {
                var cuentas = await _coreService.GetAsync<List<CuentaPorCobrar>>($"CuentaPorCobrar/estado/{estado}");
                Console.WriteLine($"Cuentas por cobrar con estado '{estado}' obtenidas del servicio Core.");
                return Ok(cuentas);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetByEstado/{estado}. Obteniendo de BD local: {ex.Message}");
                bool pagado = estado.ToLower() == "pagado";
                var cuentasLocal = await _context.CuentasPorCobrar.Where(c => c.Pagado == pagado).ToListAsync(); // Fallback a BD local
                return Ok(cuentasLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener cuentas por cobrar por estado: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostCuentaPorCobrar([FromBody] CuentaPorCobrar cuentaPorCobrar)
        {
            if (cuentaPorCobrar == null)
                return BadRequest("Datos de cuenta por cobrar inválidos");

            try
            {
                // 1. Guardar en BD local primero
                _context.CuentasPorCobrar.Add(cuentaPorCobrar);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Cuenta por cobrar con ID {cuentaPorCobrar.Id} guardada localmente.");

                // 2. Intentar enviar al servicio Core
                try
                {
                    var resultadoCore = await _coreService.PostAsync<CuentaPorCobrar>("CuentaPorCobrar", cuentaPorCobrar);
                    Console.WriteLine("Cuenta por cobrar enviada y confirmada por el servicio Core.");
                    // Opcional: Si el Core asigna un ID diferente o modifica propiedades, actualiza la entidad local aquí.
                    // cuentaPorCobrar.Id = resultadoCore.Id;
                    // await _context.SaveChangesAsync();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar cuenta por cobrar al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<CuentaPorCobrar>("CuentaPorCobrar", cuentaPorCobrar, "POST"); // Encolar para sincronización
                    Console.WriteLine($"Cuenta por cobrar con ID {cuentaPorCobrar.Id} encolada para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar cuenta por cobrar al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<CuentaPorCobrar>("CuentaPorCobrar", cuentaPorCobrar, "POST"); // Encolar para sincronización (error inesperado)
                    Console.WriteLine($"Cuenta por cobrar con ID {cuentaPorCobrar.Id} encolada para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Cuenta por cobrar procesada. Guardada localmente, sincronización con Core intentada.", data = cuentaPorCobrar });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al crear cuenta por cobrar: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutCuentaPorCobrar(int id, [FromBody] CuentaPorCobrar cuentaPorCobrar)
        {
            if (cuentaPorCobrar == null || id != cuentaPorCobrar.Id)
                return BadRequest("ID de cuenta por cobrar inválido o no coincide");

            try
            {
                // 1. Verificar y actualizar en BD local
                var cuentaExistente = await _context.CuentasPorCobrar.FindAsync(id);
                if (cuentaExistente == null)
                {
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en BD local para actualizar.");
                }

                _context.Entry(cuentaExistente).CurrentValues.SetValues(cuentaPorCobrar);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Cuenta por cobrar con ID {id} actualizada localmente.");

                // 2. Intentar enviar al servicio Core
                try
                {
                    await _coreService.PutAsync<CuentaPorCobrar>($"CuentaPorCobrar/{id}", cuentaPorCobrar);
                    Console.WriteLine("Cuenta por cobrar actualizada y confirmada por el servicio Core.");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar cuenta por cobrar en el servicio Core. Actualizada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<CuentaPorCobrar>($"CuentaPorCobrar/{id}", cuentaPorCobrar, "PUT"); // Encolar para sincronización
                    Console.WriteLine($"Cuenta por cobrar con ID {id} encolada para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar cuenta por cobrar en el servicio Core. Actualizada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<CuentaPorCobrar>($"CuentaPorCobrar/{id}", cuentaPorCobrar, "PUT"); // Encolar para sincronización (error inesperado)
                    Console.WriteLine($"Cuenta por cobrar con ID {id} encolada para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Cuenta por cobrar procesada. Actualizada localmente, sincronización con Core intentada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al actualizar cuenta por cobrar: {ex.Message}");
            }
        }

        [HttpPut("{id}/pagar")]
        public async Task<IActionResult> ActualizarPago(int id, [FromBody] bool pagado)
        {
            try
            {
                // 1. Verificar y actualizar en BD local
                var cuentaExistente = await _context.CuentasPorCobrar.FindAsync(id);
                if (cuentaExistente == null)
                {
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en BD local.");
                }

                cuentaExistente.Pagado = pagado;
                await _context.SaveChangesAsync();
                Console.WriteLine($"Estado de pago de la cuenta {id} actualizado localmente a: {pagado}.");

                // 2. Intentar enviar al servicio Core
                try
                {
                    var pagoData = new { Pagado = pagado };
                    await _coreService.PutAsync<object>($"CuentaPorCobrar/{id}/pagar", pagoData);
                    Console.WriteLine("Estado de pago de la cuenta por cobrar actualizado y confirmado por el servicio Core.");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar estado de pago en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"CuentaPorCobrar/{id}/pagar", new { Id = id, Pagado = pagado }, "PUT"); // Encolar para sincronización
                    Console.WriteLine($"Estado de pago de la cuenta {id} encolado para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar estado de pago en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"CuentaPorCobrar/{id}/pagar", new { Id = id, Pagado = pagado }, "PUT"); // Encolar para sincronización (error inesperado)
                    Console.WriteLine($"Estado de pago de la cuenta {id} encolado para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Estado de pago procesado. Actualizado localmente, sincronización con Core intentada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al actualizar estado de pago: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCuentaPorCobrar(int id)
        {
            try
            {
                // 1. Verificar y eliminar de BD local
                var cuentaExistente = await _context.CuentasPorCobrar.FindAsync(id);
                if (cuentaExistente == null)
                {
                    return NotFound($"No se encontró la cuenta por cobrar con ID {id} en BD local para eliminar.");
                }

                _context.CuentasPorCobrar.Remove(cuentaExistente);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Cuenta por cobrar con ID {id} eliminada localmente.");

                // 2. Intentar eliminar del servicio Core
                try
                {
                    await _coreService.DeleteAsync($"CuentaPorCobrar/{id}");
                    Console.WriteLine("Cuenta por cobrar eliminada y confirmada por el servicio Core.");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar cuenta por cobrar del servicio Core. Eliminada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"CuentaPorCobrar/{id}", null, "DELETE"); // Encolar para sincronización (usando object para null)
                    Console.WriteLine($"Cuenta por cobrar con ID {id} encolada para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar cuenta por cobrar del servicio Core. Eliminada solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"CuentaPorCobrar/{id}", null, "DELETE"); // Encolar para sincronización (error inesperado)
                    Console.WriteLine($"Cuenta por cobrar con ID {id} encolada para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Cuenta por cobrar procesada. Eliminada localmente, sincronización con Core intentada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al eliminar cuenta por cobrar: {ex.Message}");
            }
        }

        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetCuentasLocal()
        {
            try
            {
                var cuentas = await _context.CuentasPorCobrar
                    .Include(c => c.ClienteId) // Asumiendo que CuentaPorCobrar tiene una propiedad de navegación Cliente
                    .ToListAsync();
                return Ok(cuentas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener cuentas por cobrar de BD local: {ex.Message}");
            }
        }

        [HttpGet("local/estado/{estado}")]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetCuentasByEstadoLocal(string estado)
        {
            if (estado.ToLower() != "pagado" && estado.ToLower() != "pendiente")
                return BadRequest("Estado inválido. Use 'pagado' o 'pendiente'.");

            try
            {
                bool pagado = estado.ToLower() == "pagado";
                var cuentas = await _context.CuentasPorCobrar
                    .Include(c => c.ClienteId) // Asumiendo que CuentaPorCobrar tiene una propiedad de navegación Cliente
                    .Where(c => c.Pagado == pagado)
                    .ToListAsync();
                return Ok(cuentas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener cuentas por cobrar por estado desde BD local: {ex.Message}");
            }
        }

        [HttpGet("local/cliente/{clienteId}")]
        public async Task<ActionResult<IEnumerable<CuentaPorCobrar>>> GetCuentasByClienteLocal(int clienteId)
        {
            try
            {
                var cuentas = await _context.CuentasPorCobrar
                    .Include(c => c.ClienteId) // Asumiendo que CuentaPorCobrar tiene una propiedad de navegación Cliente
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