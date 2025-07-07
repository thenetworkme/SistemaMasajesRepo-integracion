using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("Empleados")]
    public class Empleado
    {
        [Key]
        public int EmpleadoId { get; set; }

        [Required]
        [StringLength(50)]
        public string NombreEmpleado { get; set; }

        [Required]
        [StringLength(50)]
        public string ApellidoEmpleado { get; set; }

        [Required]
        [StringLength(13)]
        public string TelefonoEmpleado { get; set; }

        [Required]
        [StringLength(100)]
        public string Cargo { get; set; } // Masajista, recepcionista, etc.

        public bool Activo { get; set; } = true;

        [JsonIgnore]
        public virtual ICollection<Cita>? Citas { get; set; }
    }
}
