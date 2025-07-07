using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientesController : ControllerBase
    {
        private readonly SistemaMasajesContext _context;

        public ClientesController(SistemaMasajesContext context)
        {
            _context = context;
        }

       
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> Get()
        {
            var clientes = await _context.Clientes
                .FromSqlRaw("EXEC sp_ObtenerClientes")
                .ToListAsync();

            return Ok(clientes);
        }

     
        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> GetCliente(int id)
        {
            var clientes = await _context.Clientes
                .FromSqlRaw("EXEC sp_ObtenerClientePorId @Id = {0}", id)
                .ToListAsync();

            var cliente = clientes.FirstOrDefault();

            if (cliente == null)
                return NotFound($"Cliente con ID {id} no encontrado");

            return Ok(cliente);
        }

      
        [HttpPost]
        public async Task<ActionResult<Cliente>> PostCliente([FromBody] Cliente cliente)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            cliente.FechaRegistro = DateTime.Now;

            var parameters = new[]
            {
                new SqlParameter("@NombreCliente", cliente.NombreCliente),
                new SqlParameter("@ApellidoCliente", cliente.ApellidoCliente),
                new SqlParameter("@TelefonoCliente", cliente.TelefonoCliente),
                new SqlParameter("@CorreoCliente", (object?)cliente.CorreoCliente ?? DBNull.Value),
                new SqlParameter("@FechaRegistro", cliente.FechaRegistro)
            };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC sp_InsertarCliente @NombreCliente, @ApellidoCliente, @TelefonoCliente, @CorreoCliente, @FechaRegistro",
                parameters);

           
            return CreatedAtAction(nameof(GetCliente), new { id = cliente.Id }, cliente);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutCliente(int id, [FromBody] Cliente cliente)
        {
            if (id != cliente.Id)
                return BadRequest("El ID no coincide");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var parameters = new[]
            {
                new SqlParameter("@Id", id),
                new SqlParameter("@NombreCliente", cliente.NombreCliente),
                new SqlParameter("@ApellidoCliente", cliente.ApellidoCliente),
                new SqlParameter("@TelefonoCliente", cliente.TelefonoCliente),
                new SqlParameter("@CorreoCliente", (object?)cliente.CorreoCliente ?? DBNull.Value)
            };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC sp_ActualizarCliente @Id, @NombreCliente, @ApellidoCliente, @TelefonoCliente, @CorreoCliente",
                parameters);

            return NoContent();
        }

    
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCliente(int id)
        {
            var param = new SqlParameter("@Id", id);
            await _context.Database.ExecuteSqlRawAsync("EXEC sp_EliminarCliente @Id", param);
            return NoContent();
        }
    }
}
