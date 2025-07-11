using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using SistemaMasajes.Integracion.Services.BackgroundSync; // Add this using directive

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue; // Inject ISyncQueue

        public RolController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue) // Add ISyncQueue to constructor
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue; // Assign ISyncQueue
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Rol>>> Get()
        {
            try
            {
                var roles = await _coreService.GetAsync<List<Rol>>("Rol");
                Console.WriteLine("Roles obtenidos del servicio Core.");
                return Ok(roles);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Roles. Obteniendo de BD local: {ex.Message}");
                var rolesLocal = await _context.Roles.ToListAsync(); // Fallback to local DB
                return Ok(rolesLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener roles: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Rol>> GetById(int id)
        {
            try
            {
                var rol = await _coreService.GetAsync<Rol>($"Rol/{id}");
                if (rol == null)
                {
                    Console.WriteLine($"Core no devolvió rol con ID {id}. Verificando en BD local.");
                    var rolLocalFallback = await _context.Roles.FindAsync(id); // Fallback to local DB
                    if (rolLocalFallback == null)
                    {
                        return NotFound($"No se encontró el rol con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(rolLocalFallback);
                }
                Console.WriteLine($"Rol con ID {id} obtenido del servicio Core.");
                return Ok(rol);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Rol/{id}. Obteniendo de BD local: {ex.Message}");
                var rolLocal = await _context.Roles.FindAsync(id); // Fallback to local DB
                if (rolLocal == null)
                    return NotFound($"No se encontró el rol con ID {id} en la BD local.");
                return Ok(rolLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener rol con ID {id}: {ex.Message}");
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
                // 1. Save to local DB first
                _context.Roles.Add(rol);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Rol con ID {rol.RolId} guardado localmente.");

                // 2. Attempt to send to Core service
                try
                {
                    var resultadoCore = await _coreService.PostAsync<Rol>("Rol", rol);
                    Console.WriteLine("Rol enviado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Rol creado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar rol al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Rol>("Rol", rol, "POST"); // Enqueue for synchronization
                    Console.WriteLine($"Rol con ID {rol.RolId} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Rol procesado. Guardado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar rol al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Rol>("Rol", rol, "POST"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Rol con ID {rol.RolId} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Rol procesado. Guardado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al crear rol: {ex.Message}");
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
                // 1. Verify and update in local DB
                var existente = await _context.Roles.FindAsync(id);
                if (existente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el rol con ID {id} en BD local para actualizar.");
                }

                _context.Entry(existente).CurrentValues.SetValues(rol);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Rol con ID {id} actualizado localmente.");

                // 2. Attempt to send to Core service
                try
                {
                    var resultadoCore = await _coreService.PutAsync<Rol>($"Rol/{id}", rol);
                    Console.WriteLine("Rol actualizado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Rol actualizado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar rol en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Rol>($"Rol/{id}", rol, "PUT"); // Enqueue for synchronization
                    Console.WriteLine($"Rol con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Rol procesado. Actualizado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar rol en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Rol>($"Rol/{id}", rol, "PUT"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Rol con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Rol procesado. Actualizado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al actualizar rol: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verify and delete from local DB
                var rol = await _context.Roles.FindAsync(id);
                if (rol == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el rol con ID {id} en BD local para eliminar.");
                }

                _context.Roles.Remove(rol);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Rol con ID {id} eliminado localmente.");

                // 2. Attempt to delete from Core service
                try
                {
                    await _coreService.DeleteAsync($"Rol/{id}");
                    Console.WriteLine("Rol eliminado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Rol eliminado correctamente" });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar rol del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Rol/{id}", null, "DELETE"); // Enqueue for synchronization (using object for null body)
                    Console.WriteLine($"Rol con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Rol procesado. Eliminado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar rol del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Rol/{id}", null, "DELETE"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Rol con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Rol procesado. Eliminado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al eliminar rol: {ex.Message}");
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