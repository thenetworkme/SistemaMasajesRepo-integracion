using Microsoft.AspNetCore.Mvc;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;
using SistemaMasajes.Integracion.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace SistemaMasajes.Integracion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductoController : ControllerBase
    {
        private readonly ICoreService _coreService;
        private readonly SistemaMasajesContext _context;

        public ProductoController(ICoreService coreService, SistemaMasajesContext context)
        {
            _coreService = coreService;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
        {
            try
            {
                var productos = await _coreService.GetAsync<List<Producto>>("Producto");
                return Ok(productos);
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

        [HttpGet("{id}")]
        public async Task<ActionResult<Producto>> GetProducto(int id)
        {
            try
            {
                var producto = await _coreService.GetAsync<Producto>($"Producto/{id}");
                if (producto == null)
                    return NotFound($"Producto con ID {id} no encontrado");

                return Ok(producto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();

                var resultado = await _coreService.PostAsync<Producto>("Producto", producto);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Producto creado correctamente", data = resultado });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                var existente = await _context.Productos.FindAsync(id);
                if (existente == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el producto con ID {id} en BD local");
                }

                _context.Entry(existente).CurrentValues.SetValues(producto);
                await _context.SaveChangesAsync();

                var resultado = await _coreService.PutAsync<Producto>($"Producto/{id}", producto);

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Producto actualizado correctamente", data = resultado });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var producto = await _context.Productos.FindAsync(id);
                if (producto == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound($"No se encontró el producto con ID {id} en BD local");
                }

                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();

                await _coreService.DeleteAsync($"Producto/{id}");

                await transaction.CommitAsync();
                return Ok(new { mensaje = "Producto eliminado correctamente" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
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
