using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Models.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmpleadoController : ControllerBase
    {
        private readonly SistemaMasajesContext _context;

        public EmpleadoController(SistemaMasajesContext context)
        {
            _context = context;
        }

        // GET: api/Empleado
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Empleado>>> Get()
        {
            var empleados = await _context.Empleados
                .FromSqlRaw("EXEC sp_ObtenerEmpleados")
                .ToListAsync();

            return Ok(empleados);
        }

        // GET: api/Empleado/id/{id}
        [HttpGet("id/{id:int}")]
        public async Task<ActionResult<Empleado>> GetById(int id)
        {
            var empleados = await _context.Empleados
                .FromSqlRaw("EXEC sp_ObtenerEmpleadoPorId @Id = {0}", id)
                .ToListAsync();

            var empleado = empleados.FirstOrDefault();

            if (empleado == null)
                return NotFound($"Empleado con ID {id} no encontrado.");

            return Ok(empleado);
        }

        // GET: api/Empleado/cargo/{cargo}
        [HttpGet("cargo/{cargo}")]
        public async Task<ActionResult<IEnumerable<Empleado>>> GetByCargo(string cargo)
        {
            var empleados = await _context.Empleados
                .FromSqlInterpolated($"EXEC sp_ObtenerEmpleadoPorCargo @Cargo = {cargo}")
                .ToListAsync();

            if (empleados.Count == 0)
                return NotFound($"No se encontraron empleados con cargo '{cargo}'.");

            return Ok(empleados);
        }

        // POST: api/Empleado
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Empleado emp)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_InsertarEmpleado 
                    @NombreEmpleado = {emp.NombreEmpleado},
                    @ApellidoEmpleado = {emp.ApellidoEmpleado},
                    @TelefonoEmpleado = {emp.TelefonoEmpleado},
                    @Cargo = {emp.Cargo}");

            return Ok("Empleado insertado correctamente.");
        }

        // PUT: api/Empleado/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Empleado emp)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_ActualizarEmpleado 
                    @EmpleadoId = {id},
                    @NombreEmpleado = {emp.NombreEmpleado},
                    @ApellidoEmpleado = {emp.ApellidoEmpleado},
                    @TelefonoEmpleado = {emp.TelefonoEmpleado},
                    @Cargo = {emp.Cargo}");

            return Ok("Empleado actualizado correctamente.");
        }

        // DELETE: api/Empleado/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_EliminarEmpleado 
                    @EmpleadoId = {id}");

            return Ok("Empleado desactivado correctamente.");
        }
    }
}
