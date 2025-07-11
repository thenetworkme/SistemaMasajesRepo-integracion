using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.BackgroundSync;
using SistemaMasajes.Integracion.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue;

        public ClientesController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue)
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes()
        {
            try
            {
                var clientes = await _coreService.GetAsync<List<Cliente>>("Clientes");
                Console.WriteLine("Clientes obtenidos del servicio Core.");
                return Ok(clientes);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetClientes. Obteniendo de BD local: {ex.Message}");
                var clientesLocal = await _context.Clientes.ToListAsync();
                return Ok(clientesLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener clientes: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> GetCliente(int id)
        {
            try
            {
                var cliente = await _coreService.GetAsync<Cliente>($"Clientes/{id}");
                if (cliente == null)
                {
                    Console.WriteLine($"Core no devolvió cliente con ID {id}. Verificando en BD local.");
                    var clienteLocalFallback = await _context.Clientes.FindAsync(id);
                    if (clienteLocalFallback == null)
                    {
                        return NotFound($"No se encontró el cliente con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(clienteLocalFallback);
                }
                Console.WriteLine($"Cliente con ID {id} obtenido del servicio Core.");
                return Ok(cliente);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para GetCliente/{id}. Obteniendo de BD local: {ex.Message}");
                var clienteLocal = await _context.Clientes.FindAsync(id);
                if (clienteLocal == null)
                    return NotFound($"No se encontró el cliente con ID {id} en la BD local.");
                return Ok(clienteLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener cliente con ID {id}: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostCliente([FromBody] Cliente cliente)
        {
            if (cliente == null)
                return BadRequest("Datos de cliente inválidos");

            try
            {
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Cliente con ID {cliente.Id} guardado localmente.");

                try
                {
                    var resultadoCore = await _coreService.PostAsync<Cliente>("Clientes", cliente);
                    Console.WriteLine("Cliente enviado y confirmado por el servicio Core.");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar cliente al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Cliente>("Clientes", cliente, "POST");
                    Console.WriteLine($"Cliente con ID {cliente.Id} encolado para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar cliente al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Cliente>("Clientes", cliente, "POST");
                    Console.WriteLine($"Cliente con ID {cliente.Id} encolado para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Cliente procesado. Guardado localmente, sincronización con Core intentada.", data = cliente });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al crear cliente: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutCliente(int id, [FromBody] Cliente cliente)
        {
            if (cliente == null || id != cliente.Id)
                return BadRequest("ID de cliente inválido o no coincide");

            try
            {
                var clienteExistente = await _context.Clientes.FindAsync(id);
                if (clienteExistente == null)
                {
                    return NotFound($"No se encontró el cliente con ID {id} en BD local para actualizar.");
                }

                _context.Entry(clienteExistente).CurrentValues.SetValues(cliente);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Cliente con ID {id} actualizado localmente.");

                try
                {
                    await _coreService.PutAsync<Cliente>($"Clientes/{id}", cliente);
                    Console.WriteLine("Cliente actualizado y confirmado por el servicio Core.");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar cliente en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Cliente>($"Clientes/{id}", cliente, "PUT");
                    Console.WriteLine($"Cliente con ID {id} encolado para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar cliente en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Cliente>($"Clientes/{id}", cliente, "PUT");
                    Console.WriteLine($"Cliente con ID {id} encolado para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Cliente procesado. Actualizado localmente, sincronización con Core intentada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al actualizar cliente: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCliente(int id)
        {
            try
            {
                var clienteExistente = await _context.Clientes.FindAsync(id);
                if (clienteExistente == null)
                {
                    return NotFound($"No se encontró el cliente con ID {id} en BD local para eliminar.");
                }

                _context.Clientes.Remove(clienteExistente);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Cliente con ID {id} eliminado localmente.");

                try
                {
                    await _coreService.DeleteAsync($"Clientes/{id}");
                    Console.WriteLine("Cliente eliminado y confirmado por el servicio Core.");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar cliente del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Cliente>($"Clientes/{id}", null, "DELETE");
                    Console.WriteLine($"Cliente con ID {id} encolado para sincronización.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar cliente del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Cliente>($"Clientes/{id}", null, "DELETE");
                    Console.WriteLine($"Cliente con ID {id} encolado para sincronización (error inesperado).");
                }

                return Ok(new { mensaje = "Cliente procesado. Eliminado localmente, sincronización con Core intentada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al eliminar cliente: {ex.Message}");
            }
        }

        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientesLocal()
        {
            try
            {
                var clientes = await _context.Clientes.ToListAsync();
                Console.WriteLine("Clientes obtenidos directamente de la BD local.");
                return Ok(clientes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener clientes de BD local: {ex.Message}");
            }
        }
    }
}