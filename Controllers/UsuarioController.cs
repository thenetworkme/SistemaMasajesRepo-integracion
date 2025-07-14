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
using System.Linq;
using System.Security.Cryptography; // Nuevo using para hashing
using System.Text; // Nuevo using para Encoding

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue;

        public UsuariosController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue)
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
                var usuarios = await _coreService.GetAsync<List<Usuario>>("Usuarios");
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
                var usuario = await _coreService.GetAsync<Usuario>($"Usuarios/{id}");
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
                Console.WriteLine($"Error al conectar con el servicio Core para Get Usuarios/{id}. Obteniendo de BD local: {ex.Message}");
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
        [Route("crear")]
        public async Task<IActionResult> Crear([FromBody] Usuario usuario)
        {
            if (usuario == null)
                return BadRequest("Datos de usuario inválidos.");

            if (string.IsNullOrWhiteSpace(usuario.UsuarioNombre) || string.IsNullOrWhiteSpace(usuario.ClaveUsuario))
            {
                return BadRequest("El nombre de usuario y la contraseña son obligatorios para el registro.");
            }

            usuario.Activo = true;
            usuario.RolId = usuario.RolId == 0 ? 1 : usuario.RolId;
            usuario.EmpleadoId = usuario.EmpleadoId == 0 ? 1 : usuario.EmpleadoId;

            string originalPassword = usuario.ClaveUsuario; // Guarda la contraseña original antes del hash para el Core

            try
            {
                // Primero intenta crear el usuario en el Core
                // El Core es quien debe hacer el hash para su propia BD.
                var resultadoCore = await _coreService.PostAsync<Usuario>("Usuarios/crear", usuario);
                Console.WriteLine("Usuario enviado y confirmado por el servicio Core.");

                // Si el Core responde exitosamente, entonces guárdalo en la BD local
                // Aplica hash para guardar en BD local si el Core fue exitoso.
                usuario.ClaveUsuario = SeguridadHelper.HashSHA256(originalPassword);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Usuarios.Add(usuario);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"Usuario con ID {usuario.UsuarioId} guardado localmente tras confirmación del Core.");
                    await transaction.CommitAsync();
                }
                catch (Exception localEx)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Error al guardar usuario localmente después de la creación en Core: {localEx.Message}");
                    return StatusCode(500, $"Usuario creado en Core pero falló el guardado local: {localEx.Message}");
                }

                return Ok(new { mensaje = "Usuario creado correctamente", data = resultadoCore });
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Advertencia: Error al enviar usuario al servicio Core. Intentando guardar solo localmente. {ex.Message}");

                // Si falla la conexión con el Core, guarda localmente y encola para sincronización
                // Aquí se aplica el hash para el guardado local
                usuario.ClaveUsuario = SeguridadHelper.HashSHA256(originalPassword);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var usuarioExistenteLocal = await _context.Usuarios.FirstOrDefaultAsync(u => u.UsuarioNombre == usuario.UsuarioNombre);
                    if (usuarioExistenteLocal != null)
                    {
                        await transaction.RollbackAsync();
                        return Conflict("El nombre de usuario ya existe localmente al intentar guardar solo aquí.");
                    }

                    _context.Usuarios.Add(usuario);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"Usuario con ID {usuario.UsuarioId} guardado localmente.");
                    _syncQueue.Enqueue<Usuario>("Usuarios/crear", usuario, "POST"); // Encola el usuario con la contraseña hasheada
                    Console.WriteLine($"Usuario con ID {usuario.UsuarioId} encolado para sincronización.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario procesado. Guardado localmente, sincronización con Core intentada." });
                }
                catch (Exception localEx)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, $"Error interno al guardar usuario localmente y encolar: {localEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inesperado al crear usuario: {ex.Message}");
                return StatusCode(500, $"Error interno al crear usuario: {ex.Message}");
            }
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Usuario usuario)
        {
            if (usuario == null || id != usuario.UsuarioId)
                return BadRequest("ID de usuario inválido o no coincide");

            string originalPassword = usuario.ClaveUsuario; // Guarda la contraseña original

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existente = await _context.Usuarios.FindAsync(id);
                if (existente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el usuario con ID {id} en BD local para actualizar.");
                }

                // Aplica hash a la contraseña antes de actualizar localmente
                if (!string.IsNullOrWhiteSpace(usuario.ClaveUsuario))
                {
                    usuario.ClaveUsuario = SeguridadHelper.HashSHA256(usuario.ClaveUsuario);
                }

                _context.Entry(existente).CurrentValues.SetValues(usuario);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Usuario con ID {id} actualizado localmente.");

                try
                {
                    // Para el Core, se envía la contraseña original (sin hash) para que el Core la hashee
                    Usuario usuarioParaCore = usuario;
                    usuarioParaCore.ClaveUsuario = originalPassword; // Revierto el hash para el Core

                    var resultadoCore = await _coreService.PutAsync<Usuario>($"Usuarios/{id}", usuarioParaCore);
                    Console.WriteLine("Usuario actualizado y confirmado por el servicio Core.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario actualizado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar usuario en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Usuario>($"Usuarios/{id}", usuario, "PUT"); // Encola el usuario con la contraseña hasheada
                    Console.WriteLine($"Usuario con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario procesado. Actualizado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar usuario en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Usuario>($"Usuarios/{id}", usuario, "PUT"); // Encola el usuario con la contraseña hasheada
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
                    await _coreService.DeleteAsync($"Usuarios/{id}");
                    Console.WriteLine("Usuario eliminado y confirmado por el servicio Core.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario eliminado correctamente" });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar usuario del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Usuarios/{id}", null, "DELETE");
                    Console.WriteLine($"Usuario con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync();
                    return Ok(new { mensaje = "Usuario procesado. Eliminado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar usuario del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Usuarios/{id}", null, "DELETE");
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

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UsuarioNombre) || string.IsNullOrWhiteSpace(request.ClaveUsuario))
            {
                return BadRequest("Se requiere nombre de usuario y contraseña.");
            }

            string hashedLocalPassword = SeguridadHelper.HashSHA256(request.ClaveUsuario); // Hash para comparar con la BD local

            try
            {
                // Intenta autenticar con el Core (envía la contraseña sin hash al Core para que el Core la hashee)
                var usuarioCore = await _coreService.PostAsync<Usuario>("Usuarios/login", request);
                if (usuarioCore != null)
                {
                    Console.WriteLine($"Usuario '{request.UsuarioNombre}' autenticado exitosamente a través del servicio Core.");
                    return Ok(new { mensaje = "Inicio de sesión exitoso con Core.", data = usuarioCore });
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Advertencia: Error al conectar con el servicio Core para login. Intentando con BD local: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Advertencia: Error inesperado del servicio Core durante el login. Intentando con BD local: {ex.Message}");
            }

            // Si falla la conexión con el Core o el Core no autentica, intenta con la BD local
            // Compara la contraseña hasheada del request con la hasheada en la BD local
            var usuarioLocal = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.UsuarioNombre == request.UsuarioNombre && u.ClaveUsuario == hashedLocalPassword);

            if (usuarioLocal != null)
            {
                Console.WriteLine($"Usuario '{request.UsuarioNombre}' autenticado exitosamente a través de la BD local.");
                return Ok(new { mensaje = "Inicio de sesión exitoso con BD local.", data = usuarioLocal });
            }
            else
            {
                Console.WriteLine($"Fallo de autenticación para el usuario '{request.UsuarioNombre}'. Credenciales inválidas.");
                return Unauthorized("Usuario o contraseña incorrectos.");
            }
        }

        public class LoginRequest
        {
            public string UsuarioNombre { get; set; }
            public string ClaveUsuario { get; set; }
        }

        // Clase estática para hashing de contraseñas (puede ir en un archivo separado SeguridadHelper.cs)
        public static class SeguridadHelper
        {
            public static string HashSHA256(string input)
            {
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        builder.Append(bytes[i].ToString("x2"));
                    }
                    return builder.ToString();
                }
            }
        }
    }
}