using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaMasajes.Integracion.Models.Entities
{
    [Table("Roles")]
    public class Rol
    {
        [Key]
        public int RolId { get; set; }

        [Required]
        [StringLength(50)]
        public string NombreRol { get; set; }  
        public virtual ICollection<Usuario>? Usuarios { get; set; }
    }
}
