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
    public class CitaController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public CitaController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        // GET: api/cita
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cita>>> GetCitas()
        {
            try
            {
                var citas = await _coreService.GetAsync<List<Cita>>("Cita");
                return Ok(citas);
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

        // GET: api/cita/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Cita>> GetCita(int id)
        {
            try
            {
                var cita = await _coreService.GetAsync<Cita>($"Cita/{id}");

                if (cita == null)
                    return NotFound($"No se encontró la cita con ID {id}");

                return Ok(cita);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la cita con ID {id}");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // POST: api/cita
        [HttpPost]
        public async Task<IActionResult> PostCita([FromBody] Cita cita)
        {
            if (cita == null)
                return BadRequest("Datos de cita inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Guardar en BD local
                _context.Citas.Add(cita);
                await _context.SaveChangesAsync();

                // 2. Enviar al servicio Core
                var resultado = await _coreService.PostAsync<Cita>("Cita", cita);

                // 3. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Cita creada correctamente", data = resultado });
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

        // PUT: api/cita/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCita(int id, [FromBody] Cita cita)
        {
            if (cita == null || id != cita.CitaId)
                return BadRequest("ID de cita inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var citaExistente = await _context.Citas.FindAsync(id);
                if (citaExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró la cita con ID {id} en BD local");
                }

                // 2. Actualizar en BD local
                _context.Entry(citaExistente).CurrentValues.SetValues(cita);
                await _context.SaveChangesAsync();

                // 3. Enviar al servicio Core
                var resultado = await _coreService.PutAsync<Cita>($"Cita/{id}", cita);

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Cita actualizada correctamente", data = resultado });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la cita con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // DELETE: api/cita/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCita(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var citaExistente = await _context.Citas.FindAsync(id);
                if (citaExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró la cita con ID {id} en BD local");
                }

                // 2. Eliminar de BD local
                _context.Citas.Remove(citaExistente);
                await _context.SaveChangesAsync();

                // 3. Eliminar del servicio Core
                await _coreService.DeleteAsync($"Cita/{id}");

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Cita eliminada correctamente" });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró la cita con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // Método adicional para obtener citas desde BD local
        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Cita>>> GetCitasLocal()
        {
            try
            {
                var citas = await _context.Citas
                    .Include(c => c.Cliente) // Incluir datos del cliente si es necesario
                    .ToListAsync();
                return Ok(citas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener citas de BD local: {ex.Message}");
            }
        }

        // Método adicional para obtener citas por cliente desde BD local
        [HttpGet("local/cliente/{clienteId}")]
        public async Task<ActionResult<IEnumerable<Cita>>> GetCitasByClienteLocal(int clienteId)
        {
            try
            {
                var citas = await _context.Citas
                    .Include(c => c.Cliente)
                    .Where(c => c.ClienteId == clienteId)
                    .ToListAsync();
                return Ok(citas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener citas del cliente desde BD local: {ex.Message}");
            }
        }
    }
}