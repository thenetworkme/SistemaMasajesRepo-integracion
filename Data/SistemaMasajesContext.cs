using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Models.Entities;

namespace SistemaMasajes.Integracion.Data
{
    public class SistemaMasajesContext : DbContext
    {
        public SistemaMasajesContext(DbContextOptions<SistemaMasajesContext> options)
            : base(options)
        {
        }

        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Empleado> Empleados { get; set; }
        public DbSet<Cita> Citas { get; set; }
        public DbSet<Servicio> Servicios { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<FacturaDetalle> FacturaDetalles { get; set; }
        public DbSet<CuentaPorCobrar> CuentasPorCobrar { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<HistorialDelSistema> HistorialSistema { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

  
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasIndex(e => e.CorreoCliente).IsUnique();
                entity.Property(e => e.FechaRegistro).HasDefaultValueSql("GETDATE()");
            });

  
            modelBuilder.Entity<Cita>(entity =>
            {
                entity.HasOne(d => d.Cliente)
                      .WithMany(p => p.Citas)
                      .HasForeignKey(d => d.ClienteId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Cliente>().HasData(
                new Cliente
                {
                    Id = 1,
                    NombreCliente = "Juan Pérez",
                    ApellidoCliente = "Pérez",
                    TelefonoCliente = "8095550001",
                    CorreoCliente = "juan@email.com",
                    FechaRegistro = DateTime.Now
                },
                new Cliente
                {
                    Id = 2,
                    NombreCliente = "María",
                    ApellidoCliente = "García",
                    TelefonoCliente = "8095550002",
                    CorreoCliente = "maria@email.com",
                    FechaRegistro = DateTime.Now
                }
            );
        }
    }
}
