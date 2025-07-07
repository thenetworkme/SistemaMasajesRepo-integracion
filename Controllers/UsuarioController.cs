using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuarioController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            // GET /api/Usuario: Listar usuarios
            return Ok();
        }

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            // GET /api/Usuario{id}: Buscar usuario por id
            return Ok();
        }

        [HttpPost]
        public IActionResult Post([FromBody] Usuario usuario)
        {
            // POST /api/Usuario: Crear usuario
            return Ok();
        }

        [HttpPut("{id}")]
        public IActionResult Put(int id, [FromBody] Usuario usuario)
        {
            // PUT /api/Usuario/{id}: Actualizar usuario
            return Ok();
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            // DELETE /api/Usuario/{id}: Eliminar usuario
            return Ok();
        }
    }
}
