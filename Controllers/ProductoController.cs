using Microsoft.AspNetCore.Mvc;
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
    public class ProductoController : ControllerBase
    {
        private readonly SistemaMasajesContext _context;

        public ProductoController(SistemaMasajesContext context)
        {
            _context = context;
        }

        // GET: api/Producto
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
        {
            return await _context.Productos
                .FromSqlRaw("EXEC sp_ObtenerProductos")
                .ToListAsync();
        }

        // GET: api/Producto/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Producto>> GetProductoById(int id)
        {
            var productos = await _context.Productos
                .FromSqlRaw("EXEC sp_ObtenerProductoPorId @ProductoId = {0}", id)
                .ToListAsync();

            var producto = productos.FirstOrDefault();

            if (producto == null)
                return NotFound($"Producto con ID {id} no encontrado");

            return producto;
        }

        // GET: api/Producto/nombre/{nombreProducto}
        [HttpGet("nombre/{nombreProducto}")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductosByNombre(string nombreProducto)
        {
            var productos = await _context.Productos
                .FromSqlInterpolated($"EXEC sp_BuscarProductoPorNombre @NombreProducto = {nombreProducto}")
                .ToListAsync();

            if (!productos.Any())
                return NotFound($"No se encontraron productos con el nombre '{nombreProducto}'");

            return productos;
        }

        // GET: api/Producto/disponible
        [HttpGet("disponible")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductosDisponibles()
        {
            var productos = await _context.Productos
                .Where(p => p.Disponible)
                .ToListAsync();

            return productos;
        }

        // POST: api/Producto
        [HttpPost]
        public async Task<IActionResult> PostProducto(Producto producto)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_InsertarProducto 
                    @NombreProducto={producto.NombreProducto}, 
                    @DescripcionProducto={producto.DescripcionProducto}, 
                    @PrecioProducto={producto.PrecioProducto}, 
                    @Stock={producto.Stock}, 
                    @Disponible={producto.Disponible}");

            return Ok("Producto insertado correctamente");
        }

        // PUT: api/Producto/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProducto(int id, Producto producto)
        {
            if (id != producto.ProductoId)
                return BadRequest("El ID no coincide");

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC sp_ActualizarProducto 
                    @ProductoId={producto.ProductoId}, 
                    @NombreProducto={producto.NombreProducto}, 
                    @DescripcionProducto={producto.DescripcionProducto}, 
                    @PrecioProducto={producto.PrecioProducto}, 
                    @Stock={producto.Stock}, 
                    @Disponible={producto.Disponible}");

            return Ok("Producto actualizado");
        }

        // DELETE: api/Producto/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC sp_EliminarProducto @ProductoId={id}");

            return NoContent();
        }
    }
}
