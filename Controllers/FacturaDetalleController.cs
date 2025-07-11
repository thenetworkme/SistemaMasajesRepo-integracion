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
    public class FacturaDetalleController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public FacturaDetalleController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        // GET: api/FacturaDetalle
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetDetalles()
        {
            try
            {
                var detalles = await _coreService.GetAsync<List<FacturaDetalle>>("FacturaDetalle");
                return Ok(detalles);
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

        // GET: api/FacturaDetalle/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<FacturaDetalle>> GetDetalle(int id)
        {
            try
            {
                var detalle = await _coreService.GetAsync<FacturaDetalle>($"FacturaDetalle/{id}");

                if (detalle == null)
                    return NotFound($"No se encontró el detalle con ID {id}");

                return Ok(detalle);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el detalle con ID {id}");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // GET: api/FacturaDetalle/factura/{facturaId}
        [HttpGet("factura/{facturaId}")]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetDetallesByFactura(int facturaId)
        {
            try
            {
                var detalles = await _coreService.GetAsync<List<FacturaDetalle>>($"FacturaDetalle/factura/{facturaId}");
                return Ok(detalles);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontraron detalles para la factura con ID {facturaId}");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                // Guardar en BD local
                _context.FacturaDetalles.Add(detalle);
                await _context.SaveChangesAsync();

                // Enviar al Core
                var resultado = await _coreService.PostAsync<FacturaDetalle>("FacturaDetalle", detalle);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Detalle de factura creado correctamente", data = resultado });
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

        // PUT: api/FacturaDetalle/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDetalle(int id, [FromBody] FacturaDetalle detalle)
        {
            if (detalle == null || id != detalle.FacturaId)
                return BadRequest("ID de detalle inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var detalleExistente = await _context.FacturaDetalles.FindAsync(id);
                if (detalleExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el detalle con ID {id} en BD local");
                }

                _context.Entry(detalleExistente).CurrentValues.SetValues(detalle);
                await _context.SaveChangesAsync();

                var resultado = await _coreService.PutAsync<FacturaDetalle>($"FacturaDetalle/{id}", detalle);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Detalle actualizado correctamente", data = resultado });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el detalle con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // DELETE: api/FacturaDetalle/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDetalle(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var detalleExistente = await _context.FacturaDetalles.FindAsync(id);
                if (detalleExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el detalle con ID {id} en BD local");
                }

                _context.FacturaDetalles.Remove(detalleExistente);
                await _context.SaveChangesAsync();

                await _coreService.DeleteAsync($"FacturaDetalle/{id}");

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Detalle eliminado correctamente" });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el detalle con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // EXTRA: Obtener detalles locales
        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetDetallesLocales()
        {
            try
            {
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
