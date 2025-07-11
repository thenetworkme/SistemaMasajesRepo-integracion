using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using SistemaMasajes.Integracion.Services.BackgroundSync; // Add this using directive

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductoController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;
        private readonly ISyncQueue _syncQueue; // Inject ISyncQueue

        public ProductoController(ICoreService coreService, SistemaMasajesContext context, ISyncQueue syncQueue) // Add ISyncQueue to constructor
        {
            _coreService = coreService;
            _context = context;
            _syncQueue = syncQueue; // Assign ISyncQueue
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
        {
            try
            {
                var productos = await _coreService.GetAsync<List<Producto>>("Producto");
                Console.WriteLine("Productos obtenidos del servicio Core.");
                return Ok(productos);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Productos. Obteniendo de BD local: {ex.Message}");
                var productosLocal = await _context.Productos.ToListAsync(); // Fallback to local DB
                return Ok(productosLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener productos: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Producto>> GetProducto(int id)
        {
            try
            {
                var producto = await _coreService.GetAsync<Producto>($"Producto/{id}");
                if (producto == null)
                {
                    Console.WriteLine($"Core no devolvió producto con ID {id}. Verificando en BD local.");
                    var productoLocalFallback = await _context.Productos.FindAsync(id); // Fallback to local DB
                    if (productoLocalFallback == null)
                    {
                        return NotFound($"No se encontró el producto con ID {id} ni en Core ni en BD local.");
                    }
                    return Ok(productoLocalFallback);
                }
                Console.WriteLine($"Producto con ID {id} obtenido del servicio Core.");
                return Ok(producto);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error al conectar con el servicio Core para Get Producto/{id}. Obteniendo de BD local: {ex.Message}");
                var productoLocal = await _context.Productos.FindAsync(id); // Fallback to local DB
                if (productoLocal == null)
                    return NotFound($"No se encontró el producto con ID {id} en la BD local.");
                return Ok(productoLocal);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno al obtener producto con ID {id}: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostProducto([FromBody] Producto producto)
        {
            if (producto == null)
                return BadRequest("Datos del producto inválidos");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Save to local DB first
                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Producto con ID {producto.ProductoId} guardado localmente.");

                // 2. Attempt to send to Core service
                try
                {
                    var resultadoCore = await _coreService.PostAsync<Producto>("Producto", producto);
                    Console.WriteLine("Producto enviado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Producto creado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al enviar producto al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Producto>("Producto", producto, "POST"); // Enqueue for synchronization
                    Console.WriteLine($"Producto con ID {producto.ProductoId} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Producto procesado. Guardado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al enviar producto al servicio Core. Guardado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Producto>("Producto", producto, "POST"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Producto con ID {producto.ProductoId} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Producto procesado. Guardado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al crear producto: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutProducto(int id, [FromBody] Producto producto)
        {
            if (producto == null || id != producto.ProductoId)
                return BadRequest("ID de producto inválido o no coincide");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verify and update in local DB
                var existente = await _context.Productos.FindAsync(id);
                if (existente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el producto con ID {id} en BD local para actualizar.");
                }

                _context.Entry(existente).CurrentValues.SetValues(producto);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Producto con ID {id} actualizado localmente.");

                // 2. Attempt to send to Core service
                try
                {
                    var resultadoCore = await _coreService.PutAsync<Producto>($"Producto/{id}", producto);
                    Console.WriteLine("Producto actualizado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Producto actualizado correctamente", data = resultadoCore });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al actualizar producto en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Producto>($"Producto/{id}", producto, "PUT"); // Enqueue for synchronization
                    Console.WriteLine($"Producto con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Producto procesado. Actualizado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al actualizar producto en el servicio Core. Actualizado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<Producto>($"Producto/{id}", producto, "PUT"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Producto con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Producto procesado. Actualizado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al actualizar producto: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Verify and delete from local DB
                var producto = await _context.Productos.FindAsync(id);
                if (producto == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el producto con ID {id} en BD local para eliminar.");
                }

                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Producto con ID {id} eliminado localmente.");

                // 2. Attempt to delete from Core service
                try
                {
                    await _coreService.DeleteAsync($"Producto/{id}");
                    Console.WriteLine("Producto eliminado y confirmado por el servicio Core.");
                    await transaction.CommitAsync(); // Commit local transaction if Core successful
                    return Ok(new { mensaje = "Producto eliminado correctamente" });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Advertencia: Error al eliminar producto del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Producto/{id}", null, "DELETE"); // Enqueue for synchronization (using object for null body)
                    Console.WriteLine($"Producto con ID {id} encolado para sincronización.");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Producto procesado. Eliminado localmente, sincronización con Core intentada." });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Advertencia: Error inesperado al eliminar producto del servicio Core. Eliminado solo localmente. {ex.Message}");
                    _syncQueue.Enqueue<object>($"Producto/{id}", null, "DELETE"); // Enqueue for synchronization (unexpected error)
                    Console.WriteLine($"Producto con ID {id} encolado para sincronización (error inesperado).");
                    await transaction.CommitAsync(); // Commit local transaction even if Core fails
                    return Ok(new { mensaje = "Producto procesado. Eliminado localmente, sincronización con Core intentada (error inesperado)." });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al eliminar producto: {ex.Message}");
            }
        }

        [HttpGet("local")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductosLocal()
        {
            try
            {
                var productos = await _context.Productos.ToListAsync();
                return Ok(productos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener productos locales: {ex.Message}");
            }
        }
    }
}