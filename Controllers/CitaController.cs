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
    public class CitaController : ControllerBase
    {
        private readonly SistemaMasajesContext _context;

        public CitaController(SistemaMasajesContext context)
        {
            _context = context;
        }

        // GET: api/cita
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cita>>> GetCitas()
        {
            var citas = await _context.Citas
                .FromSqlRaw("EXEC sp_ObtenerCitas")
                .ToListAsync();

            return Ok(citas);
        }

        // GET: api/cita/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Cita>> GetCita(int id)
        {
            var citas = await _context.Citas
                .FromSqlRaw("EXEC sp_ObtenerCitaPorId @Id = {0}", id)
                .ToListAsync();

            var cita = citas.FirstOrDefault();

            if (cita == null)
                return NotFound($"No se encontró la cita con ID {id}");

            return Ok(cita);
        }

        // POST: api/cita
        [HttpPost]
        public async Task<IActionResult> PostCita([FromBody] Cita cita)
        {
            if (cita == null)
                return BadRequest("Datos de cita inválidos");

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_InsertarCita 
                    @ClienteId = {cita.ClienteId},
                    @ServicioId = {cita.ServicioId},
                    @EmpleadoId = {cita.EmpleadoId},
                    @FechaHoraCita = {cita.FechaHoraCita},
                    @FechaHoraIngresado = {cita.FechaHoraIngresado},
                    @Estado = {cita.Estado},
                    @Observaciones = {cita.Observaciones}");

            return Ok("Cita insertada correctamente");
        }

        // PUT: api/cita/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCita(int id, [FromBody] Cita cita)
        {
            if (cita == null || id != cita.CitaId)
                return BadRequest("ID de cita inválido o no coincide");

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_ActualizarCita 
                    @CitaId = {id},
                    @ClienteId = {cita.ClienteId},
                    @ServicioId = {cita.ServicioId},
                    @EmpleadoId = {cita.EmpleadoId},
                    @FechaHoraCita = {cita.FechaHoraCita},
                    @FechaHoraIngresado = {cita.FechaHoraIngresado},
                    @Estado = {cita.Estado},
                    @Observaciones = {cita.Observaciones}");

            return Ok("Cita actualizada correctamente");
        }

        // DELETE: api/cita/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCita(int id)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_EliminarCita @CitaId = {id}");

            return Ok("Cita eliminada correctamente");
        }
    }
}
