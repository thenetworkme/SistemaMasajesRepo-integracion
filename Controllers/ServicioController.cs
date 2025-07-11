using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicioController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public ServicioController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Servicio>>> GetServicios()
        {
            try
            {
                var servicios = await _coreService.GetAsync<List<Servicio>>("Servicio");
                return Ok(servicios);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Servicio>> GetServicioById(int id)
        {
            try
            {
                var servicio = await _coreService.GetAsync<Servicio>($"Servicio/{id}");
                if (servicio == null)
                    return NotFound($"Servicio con ID {id} no encontrado");

                return Ok(servicio);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostServicio([FromBody] Servicio servicio)
        {
            if (servicio == null)
                return BadRequest("Datos de servicio inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Servicios.Add(servicio);
                await _context.SaveChangesAsync();

                var resultado = await _coreService.PostAsync<Servicio>("Servicio", servicio);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Servicio creado correctamente", data = resultado });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutServicio(int id, [FromBody] Servicio servicio)
        {
            if (servicio == null || id != servicio.ServicioId)
                return BadRequest("ID de servicio inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existente = await _context.Servicios.FindAsync(id);
                if (existente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el servicio con ID {id} en BD local");
                }

                _context.Entry(existente).CurrentValues.SetValues(servicio);
                await _context.SaveChangesAsync();

                var resultado = await _coreService.PutAsync<Servicio>($"Servicio/{id}", servicio);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Servicio actualizado correctamente", data = resultado });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteServicio(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var servicio = await _context.Servicios.FindAsync(id);
                if (servicio == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el servicio con ID {id} en BD local");
                }

                _context.Servicios.Remove(servicio);
                await _context.SaveChangesAsync();

                await _coreService.DeleteAsync($"Servicio/{id}");

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Servicio eliminado correctamente" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Servicio>>> GetServiciosLocal()
        {
            try
            {
                var servicios = await _context.Servicios.ToListAsync();
                return Ok(servicios);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener servicios locales: {ex.Message}");
            }
        }
    }
}
