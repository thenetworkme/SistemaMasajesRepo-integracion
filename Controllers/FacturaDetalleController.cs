using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Models.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FacturaDetalleController : ControllerBase
    {
        private readonly SistemaMasajesContext _context;

        public FacturaDetalleController(SistemaMasajesContext context)
        {
            _context = context;
        }

        // GET: api/FacturaDetalle
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FacturaDetalle>>> GetDetalles()
        {
            var detalles = await _context.FacturaDetalles.ToListAsync();
            return Ok(detalles);
        }

        // Opcional: implementar DELETE si es necesario
        // DELETE: api/FacturaDetalle/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDetalle(int id)
        {
            var detalle = await _context.FacturaDetalles.FindAsync(id);
            if (detalle == null)
                return NotFound();

            _context.FacturaDetalles.Remove(detalle);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
