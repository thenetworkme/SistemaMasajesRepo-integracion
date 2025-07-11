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
    public class UsuarioController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public UsuarioController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Usuario>>> Get()
        {
            try
            {
                var usuarios = await _coreService.GetAsync<List<Usuario>>("Usuario");
                return Ok(usuarios);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Usuario>> GetById(int id)
        {
            try
            {
                var usuario = await _coreService.GetAsync<Usuario>($"Usuario/{id}");
                if (usuario == null)
                    return NotFound($"Usuario con ID {id} no encontrado");

                return Ok(usuario);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Usuario usuario)
        {
            if (usuario == null)
                return BadRequest("Datos de usuario inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                var resultado = await _coreService.PostAsync<Usuario>("Usuario", usuario);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Usuario creado correctamente", data = resultado });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Usuario usuario)
        {
            if (usuario == null || id != usuario.UsuarioId)
                return BadRequest("ID de usuario inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existente = await _context.Usuarios.FindAsync(id);
                if (existente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el usuario con ID {id} en BD local");
                }

                _context.Entry(existente).CurrentValues.SetValues(usuario);
                await _context.SaveChangesAsync();

                var resultado = await _coreService.PutAsync<Usuario>($"Usuario/{id}", usuario);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Usuario actualizado correctamente", data = resultado });
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
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el usuario con ID {id} en BD local");
                }

                _context.Usuarios.Remove(usuario);
                await _context.SaveChangesAsync();

                await _coreService.DeleteAsync($"Usuario/{id}");

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Usuario eliminado correctamente" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Usuario>>> GetLocal()
        {
            try
            {
                var usuarios = await _context.Usuarios.ToListAsync();
                return Ok(usuarios);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener usuarios locales: {ex.Message}");
            }
        }
    }
}
