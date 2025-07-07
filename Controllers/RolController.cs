using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;

namespace SistemaMasajes.Integracion.Controllers
{
    [Route("api/[controller]")]
    public class RolController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            // GET /api/Rol: Listar roles
            return Ok();
        }

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            // GET /api/Rol/{id}: buscar rol por id
            return Ok();
        }

        [HttpPost]
        public IActionResult Post([FromBody] Rol rol)
        {
            // POST /api/Rol: Crear rol
            return Ok();
        }

        [HttpPut("{id}")]
        public IActionResult Put(int id, [FromBody] Rol rol)
        {
            // PUT /api/Rol/{id}: Actualizar rol
            return Ok();
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            // DELETE /api/Rol/{id}: Eliminar rol
            return Ok();
        }
    }
}
