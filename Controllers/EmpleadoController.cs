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
    public class EmpleadoController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public EmpleadoController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        // GET: api/Empleado
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Empleado>>> GetEmpleados()
        {
            try
            {
                var empleados = await _coreService.GetAsync<List<Empleado>>("Empleado");
                return Ok(empleados);
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

        // GET: api/Empleado/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Empleado>> GetEmpleado(int id)
        {
            try
            {
                var empleado = await _coreService.GetAsync<Empleado>($"Empleado/{id}");

                if (empleado == null)
                    return NotFound($"No se encontró el empleado con ID {id}");

                return Ok(empleado);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el empleado con ID {id}");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // GET: api/Empleado/cargo/{cargo}
        [HttpGet("cargo/{cargo}")]
        public async Task<ActionResult<IEnumerable<Empleado>>> GetByCargo(string cargo)
        {
            try
            {
                var empleados = await _coreService.GetAsync<List<Empleado>>($"Empleado/cargo/{cargo}");

                if (empleados == null || empleados.Count == 0)
                    return NotFound($"No se encontraron empleados con cargo '{cargo}'");

                return Ok(empleados);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontraron empleados con cargo '{cargo}'");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // POST: api/Empleado
        [HttpPost]
        public async Task<IActionResult> PostEmpleado([FromBody] Empleado empleado)
        {
            if (empleado == null)
                return BadRequest("Datos de empleado inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Guardar en BD local
                _context.Empleados.Add(empleado);
                await _context.SaveChangesAsync();

                // 2. Enviar al servicio Core
                var resultado = await _coreService.PostAsync<Empleado>("Empleado", empleado);

                // 3. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Empleado creado correctamente", data = resultado });
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

        // PUT: api/Empleado/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEmpleado(int id, [FromBody] Empleado empleado)
        {
            if (empleado == null || id != empleado.EmpleadoId)
                return BadRequest("ID de empleado inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var empleadoExistente = await _context.Empleados.FindAsync(id);
                if (empleadoExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el empleado con ID {id} en BD local");
                }

                // 2. Actualizar en BD local
                _context.Entry(empleadoExistente).CurrentValues.SetValues(empleado);
                await _context.SaveChangesAsync();

                // 3. Enviar al servicio Core
                var resultado = await _coreService.PutAsync<Empleado>($"Empleado/{id}", empleado);

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Empleado actualizado correctamente", data = resultado });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el empleado con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // DELETE: api/Empleado/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmpleado(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var empleadoExistente = await _context.Empleados.FindAsync(id);
                if (empleadoExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el empleado con ID {id} en BD local");
                }

                // 2. Eliminar de BD local
                _context.Empleados.Remove(empleadoExistente);
                await _context.SaveChangesAsync();

                // 3. Eliminar del servicio Core
                await _coreService.DeleteAsync($"Empleado/{id}");

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Empleado eliminado correctamente" });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el empleado con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // Método adicional para obtener empleados desde BD local
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

        // Método adicional para obtener empleados por cargo desde BD local
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

        // Método adicional para obtener empleados activos desde BD local
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