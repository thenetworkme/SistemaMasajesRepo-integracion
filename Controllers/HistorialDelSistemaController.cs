using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HistorialDelSistemaController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public HistorialDelSistemaController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        // GET: api/HistorialDelSistema
        [HttpGet]
        public async Task<ActionResult<IEnumerable<HistorialDelSistema>>> GetHistorial()
        {
            try
            {
                var historial = await _coreService.GetAsync<List<HistorialDelSistema>>("HistorialDelSistema");
                return Ok(historial);
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

        // GET: api/HistorialDelSistema/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<HistorialDelSistema>> GetHistorialPorId(int id)
        {
            try
            {
                var item = await _coreService.GetAsync<HistorialDelSistema>($"HistorialDelSistema/{id}");

                if (item == null)
                    return NotFound($"No se encontró el historial con ID {id}");

                return Ok(item);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el historial con ID {id}");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // POST: api/HistorialDelSistema
        [HttpPost]
        public async Task<IActionResult> PostHistorial([FromBody] HistorialDelSistema historial)
        {
            if (historial == null)
                return BadRequest("Datos del historial inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Guardar en BD local
                _context.HistorialSistema.Add(historial);
                await _context.SaveChangesAsync();

                // Enviar al servicio Core
                var resultado = await _coreService.PostAsync<HistorialDelSistema>("HistorialDelSistema", historial);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Evento registrado correctamente", data = resultado });
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

        // DELETE: api/HistorialDelSistema/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHistorial(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Verificar en BD local
                var historial = await _context.HistorialSistema.FindAsync(id);
                if (historial == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el historial con ID {id} en BD local");
                }

                // Eliminar local
                _context.HistorialSistema.Remove(historial);
                await _context.SaveChangesAsync();

                // Eliminar del Core
                await _coreService.DeleteAsync($"HistorialDelSistema/{id}");

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Historial eliminado correctamente" });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el historial con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // EXTRA: GET local
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
