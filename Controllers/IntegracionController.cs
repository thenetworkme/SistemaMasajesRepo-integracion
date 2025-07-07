using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.DTOs;
using SistemaMasajes.Integracion.Services.Interfaces;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IntegracionController : ControllerBase
    {
        private readonly ICoreService _coreService;

        public IntegracionController(ICoreService coreService)
        {
            _coreService = coreService;
        }

        [HttpGet("clientes")]
        public async Task<ActionResult<IEnumerable<ClienteDTO>>> ObtenerClientes()
        {
            try
            {
                var clientes = await _coreService.ObtenerClientesAsync();
                return Ok(clientes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("clientes/{id}")]
        public async Task<ActionResult<ClienteDTO>> ObtenerCliente(int id)
        {
            try
            {
                var cliente = await _coreService.ObtenerClienteAsync(id);
                if (cliente == null)
                    return NotFound();

                return Ok(cliente);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("clientes")]
        public async Task<ActionResult<ClienteDTO>> CrearCliente([FromBody] ClienteDTO cliente)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var nuevoCliente = await _coreService.CrearClienteAsync(cliente);
                return CreatedAtAction(nameof(ObtenerCliente), new { id = nuevoCliente.Id }, nuevoCliente);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}