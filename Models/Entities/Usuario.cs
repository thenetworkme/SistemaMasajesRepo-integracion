using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("Usuarios")]
    public class Usuario
    {
        [Key]
        public int UsuarioId { get; set; }

        [Required]
        [StringLength(50)]
        public string UsuarioNombre { get; set; }

        [Required]
        [StringLength(20)]
        public string ClaveUsuario { get; set; }

        [Required]
        public int RolId { get; set; }

        [ForeignKey("RolId")]
        public virtual Rol? Rol { get; set; }

        public bool Activo { get; set; } = true;

        public int? EmpleadoId { get; set; }

        public virtual Empleado? Empleado { get; set; }

    }
}
