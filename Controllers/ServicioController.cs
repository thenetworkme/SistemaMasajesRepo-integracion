using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicioController : ControllerBase
    {
        private readonly SistemaMasajesContext _context;

        public ServicioController(SistemaMasajesContext context)
        {
            _context = context;
        }

        // GET: api/Servicio
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Servicio>>> GetServicios()
        {
            return await _context.Servicios
                .FromSqlRaw("EXEC sp_ObtenerServicios")
                .ToListAsync();
        }

        // GET: api/Servicio/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Servicio>> GetServicioById(int id)
        {
            var servicios = await _context.Servicios
                .FromSqlRaw("EXEC sp_ObtenerServicioPorId @ServicioId = {0}", id)
                .ToListAsync();

            var servicio = servicios.FirstOrDefault();

            if (servicio == null)
                return NotFound($"Servicio con ID {id} no encontrado");

            return servicio;
        }

        // GET: api/Servicio/nombre/{nombreServicio}
        [HttpGet("nombre/{nombreServicio}")]
        public async Task<ActionResult<IEnumerable<Servicio>>> GetServiciosByNombre(string nombreServicio)
        {
            var servicios = await _context.Servicios
                .FromSqlInterpolated($"EXEC sp_BuscarServicioPorNombre @NombreServicio = {nombreServicio}")
                .ToListAsync();

            if (!servicios.Any())
                return NotFound($"No se encontraron servicios que coincidan con '{nombreServicio}'");

            return servicios;
        }

        // POST: api/Servicio
        [HttpPost]
        public async Task<IActionResult> PostServicio(Servicio servicio)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_InsertarServicio 
                    @NombreServicio = {servicio.NombreServicio},
                    @PrecioServicio = {servicio.PrecioServicio},
                    @DuracionPromedioMinutos = {servicio.DuracionPromedioMinutos}");

            return Ok("Servicio insertado correctamente");
        }

        // PUT: api/Servicio/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutServicio(int id, Servicio servicio)
        {
            if (id != servicio.ServicioId)
                return BadRequest("El ID no coincide");

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_ActualizarServicio 
                    @ServicioId = {id},
                    @NombreServicio = {servicio.NombreServicio},
                    @PrecioServicio = {servicio.PrecioServicio},
                    @DuracionPromedioMinutos = {servicio.DuracionPromedioMinutos}");

            return Ok("Servicio actualizado");
        }

        // DELETE: api/Servicio/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteServicio(int id)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_EliminarServicio @ServicioId = {id}");

            return Ok("Servicio eliminado lógicamente");
        }
    }
}
