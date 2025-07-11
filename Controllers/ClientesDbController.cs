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
    public class ClientesController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public ClientesController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        // GET: api/clientes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes()
        {
            try
            {
                var clientes = await _coreService.GetAsync<List<Cliente>>("Clientes");
                return Ok(clientes);
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

        // GET: api/clientes/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> GetCliente(int id)
        {
            try
            {
                var cliente = await _coreService.GetAsync<Cliente>($"Clientes/{id}");

                if (cliente == null)
                    return NotFound($"No se encontró el cliente con ID {id}");

                return Ok(cliente);
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el cliente con ID {id}");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // POST: api/clientes
        [HttpPost]
        public async Task<IActionResult> PostCliente([FromBody] Cliente cliente)
        {
            if (cliente == null)
                return BadRequest("Datos de cliente inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Guardar en BD local
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                // 2. Enviar al servicio Core
                var resultado = await _coreService.PostAsync<Cliente>("Clientes", cliente);

                // 3. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Cliente creado correctamente", data = resultado });
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

        // PUT: api/clientes/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCliente(int id, [FromBody] Cliente cliente)
        {
            if (cliente == null || id != cliente.Id)
                return BadRequest("ID de cliente inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var clienteExistente = await _context.Clientes.FindAsync(id);
                if (clienteExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el cliente con ID {id} en BD local");
                }

                // 2. Actualizar en BD local
                _context.Entry(clienteExistente).CurrentValues.SetValues(cliente);
                await _context.SaveChangesAsync();

                // 3. Enviar al servicio Core
                var resultado = await _coreService.PutAsync<Cliente>($"Clientes/{id}", cliente);

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Cliente actualizado correctamente", data = resultado });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el cliente con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // DELETE: api/clientes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCliente(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verificar que existe en BD local
                var clienteExistente = await _context.Clientes.FindAsync(id);
                if (clienteExistente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el cliente con ID {id} en BD local");
                }

                // 2. Eliminar de BD local
                _context.Clientes.Remove(clienteExistente);
                await _context.SaveChangesAsync();

                // 3. Eliminar del servicio Core
                await _coreService.DeleteAsync($"Clientes/{id}");

                // 4. Confirmar transacción
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Cliente eliminado correctamente" });
            }
            catch (HttpRequestException ex)
            {
                await transaction.RollbackAsync();
                if (ex.Message.Contains("404"))
                    return NotFound($"No se encontró el cliente con ID {id} en el servicio Core");

                return StatusCode(500, $"Error al conectar con el servicio Core: {ex.Message}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // Método adicional para obtener clientes desde BD local
        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientesLocal()
        {
            try
            {
                var clientes = await _context.Clientes.ToListAsync();
                return Ok(clientes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener clientes de BD local: {ex.Message}");
            }
        }
    }
}