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
    public class RolController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public RolController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Rol>>> Get()
        {
            try
            {
                var roles = await _coreService.GetAsync<List<Rol>>("Rol");
                return Ok(roles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Rol>> GetById(int id)
        {
            try
            {
                var rol = await _coreService.GetAsync<Rol>($"Rol/{id}");
                if (rol == null)
                    return NotFound($"Rol con ID {id} no encontrado");

                return Ok(rol);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Rol rol)
        {
            if (rol == null)
                return BadRequest("Datos de rol inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Roles.Add(rol);
                await _context.SaveChangesAsync();

                var resultado = await _coreService.PostAsync<Rol>("Rol", rol);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Rol creado correctamente", data = resultado });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Rol rol)
        {
            if (rol == null || id != rol.RolId)
                return BadRequest("ID de rol inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existente = await _context.Roles.FindAsync(id);
                if (existente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el rol con ID {id} en BD local");
                }

                _context.Entry(existente).CurrentValues.SetValues(rol);
                await _context.SaveChangesAsync();

                var resultado = await _coreService.PutAsync<Rol>($"Rol/{id}", rol);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Rol actualizado correctamente", data = resultado });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var rol = await _context.Roles.FindAsync(id);
                if (rol == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el rol con ID {id} en BD local");
                }

                _context.Roles.Remove(rol);
                await _context.SaveChangesAsync();

                await _coreService.DeleteAsync($"Rol/{id}");

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Rol eliminado correctamente" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Rol>>> GetLocal()
        {
            try
            {
                var roles = await _context.Roles.ToListAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener roles locales: {ex.Message}");
            }
        }
    }
}
