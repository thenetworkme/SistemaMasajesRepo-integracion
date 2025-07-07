using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HistorialDelSistemaController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            // GET /api/HistorialDelSistema: Listar eventos del sistema
            return Ok();
        }

        [HttpPost]
        public IActionResult Post([FromBody] HistorialDelSistema historial)
        {
            // POST /api/HistorialDelSistema: Registrar nuevo evento (interno)
            return Ok();
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            // DELETE /api/HistorialDelSistema/{id}: Eliminar registros (opcional)
            return Ok();
        }
    }
}
