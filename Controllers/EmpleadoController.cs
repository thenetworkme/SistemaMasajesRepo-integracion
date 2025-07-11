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
    public class EmpleadoController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue; // Inyecta ISyncQueue

        public EmpleadoController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue) // Agrega ISyncQueue al constructor
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue; // Asigna ISyncQueue
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Empleado>>> GetEmpleados()
        {
            try
            {
                var empleados = await _coreService.GetAsync<List<Empleado>>("Empleado");
                Console.WriteLine("Empleados obtenidos del servicio Core.");
                return Ok(empleados);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetEmpleados. Obteniendo de BD local: {ex.Message}");
                var empleadosLocal = await _context.Empleados.ToListAsync(); // Fallback a BD local
                return Ok(empleadosLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener empleados: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Empleado>> GetEmpleado(int id)
        {
            try
            {
                var empleado = await _coreService.GetAsync<Empleado>($"Empleado/{id}");

                if (empleado == null)
                {
                    Console.WriteLine($"Core no devolvió empleado con ID {id}. Verificando en BD local.");
                    var empleadoLocalFallback = await _context.Empleados.FindAsync(id); // Fallback a BD local
                    if (empleadoLocalFallback == null)
                    {
                        return NotFound($"No se encontró el empleado con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(empleadoLocalFallback);
                }
                Console.WriteLine($"Empleado con ID {id} obtenido del servicio Core.");
                return Ok(empleado);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetEmpleado/{id}. Obteniendo de BD local: {ex.Message}");
                var empleadoLocal = await _context.Empleados.FindAsync(id); // Fallback a BD local
                if (empleadoLocal == null)
                    return NotFound($"No se encontró el empleado con ID {id} en la BD local.");
                return Ok(empleadoLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener empleado con ID {id}: {ex.Message}");
            }
        }

        [HttpGet("cargo/{cargo}")]
        public async Task<ActionResult<IEnumerable<Empleado>>> GetByCargo(string cargo)
        {
            try
            {
                var empleados = await _coreService.GetAsync<List<Empleado>>($"Empleado/cargo/{cargo}");
                Console.WriteLine($"Empleados con cargo '{cargo}' obtenidos del servicio Core.");

                if (empleados == null || empleados.Count == 0)
                {
                    Console.WriteLine($"Core no devolvió empleados con cargo '{cargo}'. Verificando en BD local.");
                    var empleadosLocalFallback = await _context.Empleados.Where(e => e.Cargo.ToLower() == cargo.ToLower()).ToListAsync(); // Fallback a BD local
                    if (empleadosLocalFallback == null || empleadosLocalFallback.Count == 0)
                        return NotFound($"No se encontraron empleados con cargo '{cargo}' ni en Core ni en BD local.");
                    return Ok(empleadosLocalFallback);
                }
                return Ok(empleados);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetByCargo/{cargo}. Obteniendo de BD local: {ex.Message}");
                var empleadosLocal = await _context.Empleados.Where(e => e.Cargo.ToLower() == cargo.ToLower()).ToListAsync(); // Fallback a BD local
                if (empleadosLocal == null || empleadosLocal.Count == 0)
                    return NotFound($"No se encontraron empleados con cargo '{cargo}' en BD local.");
                return Ok(empleadosLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener empleados por cargo: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostEmpleado([FromBody] Empleado empleado)
        {
            if (empleado == null)
                return BadRequest("Datos de empleado inválidos");

            try
            {
                // 1. Guardar en BD local primero
                _context.Empleados.Add(empleado);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Empleado con ID {empleado.EmpleadoId} guardado localmente.");

                // 2. Intentar enviar al servicio Core
                try
                {
                    var resultadoCore = await _coreService.PostAsync<Empleado>("Empleado", empleado);
                    Console.WriteLine("Empleado enviado y confirmado por el servicio Core.");
                    // Opcional: Si el Core asigna un EmpleadoId diferente o modifica propiedades, actualiza la entidad local aquí.
                    // empleado.EmpleadoId = resultadoCore.EmpleadoId;
                    // await _context.SaveChangesAsync();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar empleado al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Empleado>("Empleado", empleado, "POST"); // Encolar para sincronización
                    Console.WriteLine($"Empleado con ID {empleado.EmpleadoId} encolado para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar empleado al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Empleado>("Empleado", empleado, "POST"); // Encolar para sincronización (error inesperado)
                    Console.WriteLine($"Empleado con ID {empleado.EmpleadoId} encolado para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Empleado procesado. Guardado localmente, sincronización con Core intentada.", data = empleado });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al crear empleado: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutEmpleado(int id, [FromBody] Empleado empleado)
        {
            if (empleado == null || id != empleado.EmpleadoId)
                return BadRequest("ID de empleado inválido o no coincide");

            try
            {
                // 1. Verificar y actualizar en BD local
                var empleadoExistente = await _context.Empleados.FindAsync(id);
                if (empleadoExistente == null)
                {
                    return NotFound($"No se encontró el empleado con ID {id} en BD local para actualizar.");
                }

                _context.Entry(empleadoExistente).CurrentValues.SetValues(empleado);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Empleado con ID {id} actualizado localmente.");

                // 2. Intentar enviar al servicio Core
                try
                {
                    await _coreService.PutAsync<Empleado>($"Empleado/{id}", empleado);
                    Console.WriteLine("Empleado actualizado y confirmado por el servicio Core.");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar empleado en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Empleado>($"Empleado/{id}", empleado, "PUT"); // Encolar para sincronización
                    Console.WriteLine($"Empleado con ID {id} encolado para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar empleado en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Empleado>($"Empleado/{id}", empleado, "PUT"); // Encolar para sincronización (error inesperado)
                    Console.WriteLine($"Empleado con ID {id} encolado para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Empleado procesado. Actualizado localmente, sincronización con Core intentada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al actualizar empleado: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmpleado(int id)
        {
            try
            {
                // 1. Verificar y eliminar de BD local
                var empleadoExistente = await _context.Empleados.FindAsync(id);
                if (empleadoExistente == null)
                {
                    return NotFound($"No se encontró el empleado con ID {id} en BD local para eliminar.");
                }

                _context.Empleados.Remove(empleadoExistente);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Empleado con ID {id} eliminado localmente.");

                // 2. Intentar eliminar del servicio Core
                try
                {
                    await _coreService.DeleteAsync($"Empleado/{id}");
                    Console.WriteLine("Empleado eliminado y confirmado por el servicio Core.");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar empleado del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Empleado/{id}", null, "DELETE"); // Encolar para sincronización (usando object para null)
                    Console.WriteLine($"Empleado con ID {id} encolado para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar empleado del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Empleado/{id}", null, "DELETE"); // Encolar para sincronización (error inesperado)
                    Console.WriteLine($"Empleado con ID {id} encolado para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Empleado procesado. Eliminado localmente, sincronización con Core intentada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al eliminar empleado: {ex.Message}");
            }
        }

        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Empleado>>> GetEmpleadosLocal()
        {
            try
            {
                var empleados = await _context.Empleados.ToListAsync();
                return Ok(empleados);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener empleados de BD local: {ex.Message}");
            }
        }

        [HttpGet("local/cargo/{cargo}")]
        public async Task<ActionResult<IEnumerable<Empleado>>> GetEmpleadosByCargoLocal(string cargo)
        {
            try
            {
                var empleados = await _context.Empleados
                    .Where(e => e.Cargo.ToLower() == cargo.ToLower())
                    .ToListAsync();

                if (empleados == null || empleados.Count == 0)
                    return NotFound($"No se encontraron empleados con cargo '{cargo}' en BD local");

                return Ok(empleados);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener empleados por cargo desde BD local: {ex.Message}");
            }
        }

        [HttpGet("local/activos")]
        public async Task<ActionResult<IEnumerable<Empleado>>> GetEmpleadosActivosLocal()
        {
            try
            {
                var empleados = await _context.Empleados
                    .Where(e => e.Activo == true) // Asumiendo que existe una propiedad Activo
                    .ToListAsync();
                return Ok(empleados);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener empleados activos desde BD local: {ex.Message}");
            }
        }
    }
}