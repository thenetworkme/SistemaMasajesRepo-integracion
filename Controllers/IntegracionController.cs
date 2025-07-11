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
                var clientes = await _coreService.GetAsync<List<ClienteDTO>>("Cliente");
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

        [HttpGet("clientes/{id}")]
        public async Task<ActionResult<ClienteDTO>> ObtenerCliente(int id)
        {
            try
            {
                var cliente = await _coreService.GetAsync<ClienteDTO>($"Cliente/{id}");

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

        [HttpPost("clientes")]
        public async Task<ActionResult<ClienteDTO>> CrearCliente([FromBody] ClienteDTO cliente)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var nuevoCliente = await _coreService.PostAsync<ClienteDTO>("Cliente", cliente);

                return CreatedAtAction(nameof(ObtenerCliente), new { id = nuevoCliente.Id }, nuevoCliente);
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

        [HttpPut("clientes/{id}")]
        public async Task<ActionResult<ClienteDTO>> ActualizarCliente(int id, [FromBody] ClienteDTO cliente)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (id != cliente.Id)
                    return BadRequest("El ID del cliente no coincide");

                var clienteActualizado = await _coreService.PutAsync<ClienteDTO>($"Cliente/{id}", cliente);

                return Ok(clienteActualizado);
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

        [HttpDelete("clientes/{id}")]
        public async Task<IActionResult> EliminarCliente(int id)
        {
            try
            {
                await _coreService.DeleteAsync($"Cliente/{id}");
                return Ok(new { mensaje = "Cliente eliminado correctamente" });
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
    }
}