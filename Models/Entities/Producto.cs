using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("Productos")]
    public class Producto
    {
        [Key]
        public int ProductoId { get; set; }

        [Required]
        [StringLength(50)]
        public string NombreProducto { get; set; }

        [Required]
        [StringLength(200)]
        public string DescripcionProducto { get; set; }

        [Precision(10, 2)]
        public decimal PrecioProducto { get; set; }

        public int Stock { get; set; }

        public bool Disponible { get; set; } = true;
    }
}
