using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("Clientes")]
    public class Cliente
    {
        [Key]
        public int Id { get; set; } // clave primaria

        [Required]
        [StringLength(50)]
        public string NombreCliente { get; set; }

        [Required]
        [StringLength(50)]
        public string ApellidoCliente { get; set; }

        [Required]
        [StringLength(13)]
        public string TelefonoCliente { get; set; }

        [StringLength(150)]
        public string? CorreoCliente { get; set; }


        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Navegación
        public virtual ICollection<Cita>? Citas { get; set; }
        public virtual ICollection<Factura>? Facturas { get; set; }
    }
}
