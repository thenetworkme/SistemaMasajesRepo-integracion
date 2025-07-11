using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using SistemaMasajes.Integracion.Services.BackgroundSync;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuarioController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue;

        public UsuarioController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue)
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Usuario>>> Get()
        {
            try
            {
                var usuarios = await _coreService.GetAsync<List<Usuario>>("Usuario");
                return Ok(usuarios);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Usuarios. Obteniendo de BD local: {ex.Message}");
                var usuariosLocal = await _context.Usuarios.ToListAsync();
                return Ok(usuariosLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener usuarios: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Usuario>> GetById(int id)
        {
            try
            {
                var usuario = await _coreService.GetAsync<Usuario>($"Usuario/{id}");
                if (usuario == null)
                {
                    Console.WriteLine($"Core no devolvió usuario con ID {id}. Verificando en BD local.");
                    var usuarioLocalFallback = await _context.Usuarios.FindAsync(id);
                    if (usuarioLocalFallback == null)
                    {
                        return NotFound($"No se encontró el usuario con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(usuarioLocalFallback);
                }
                return Ok(usuario);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Usuario/{id}. Obteniendo de BD local: {ex.Message}");
                var usuarioLocal = await _context.Usuarios.FindAsync(id);
                if (usuarioLocal == null)
                    return NotFound($"No se encontró el usuario con ID {id} en la BD local.");
                return Ok(usuarioLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener usuario con ID {id}: {ex.Message}");
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
                Console.WriteLine($"Usuario con ID {usuario.UsuarioId} guardado localmente.");

                try
                {
                    var resultadoCore = await _coreService.PostAsync<Usuario>("Usuario", usuario);
                    Console.WriteLine("Usuario enviado y confirmado por el servicio Core.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario creado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar usuario al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Usuario>("Usuario", usuario, "POST");
                    Console.WriteLine($"Usuario con ID {usuario.UsuarioId} encolado para sincronización.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario procesado. Guardado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar usuario al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Usuario>("Usuario", usuario, "POST");
                    Console.WriteLine($"Usuario con ID {usuario.UsuarioId} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario procesado. Guardado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al crear usuario: {ex.Message}");
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
                    return NotFound($"No se encontró el usuario con ID {id} en BD local para actualizar.");
                }

                _context.Entry(existente).CurrentValues.SetValues(usuario);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Usuario con ID {id} actualizado localmente.");

                try
                {
                    var resultadoCore = await _coreService.PutAsync<Usuario>($"Usuario/{id}", usuario);
                    Console.WriteLine("Usuario actualizado y confirmado por el servicio Core.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario actualizado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar usuario en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Usuario>($"Usuario/{id}", usuario, "PUT");
                    Console.WriteLine($"Usuario con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario procesado. Actualizado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar usuario en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Usuario>($"Usuario/{id}", usuario, "PUT");
                    Console.WriteLine($"Usuario con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario procesado. Actualizado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al actualizar usuario: {ex.Message}");
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
                    return NotFound($"No se encontró el usuario con ID {id} en BD local para eliminar.");
                }

                _context.Usuarios.Remove(usuario);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Usuario con ID {id} eliminado localmente.");

                try
                {
                    await _coreService.DeleteAsync($"Usuario/{id}");
                    Console.WriteLine("Usuario eliminado y confirmado por el servicio Core.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario eliminado correctamente" });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar usuario del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Usuario/{id}", null, "DELETE");
                    Console.WriteLine($"Usuario con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario procesado. Eliminado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar usuario del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Usuario/{id}", null, "DELETE");
                    Console.WriteLine($"Usuario con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario procesado. Eliminado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al eliminar usuario: {ex.Message}");
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