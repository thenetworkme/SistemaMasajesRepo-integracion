using Microsoft.EntityFrameworkCore;
using SistemaMasajes.Integracion.Data;
using SistemaMasajes.Integracion.Models.DTOs;
using SistemaMasajes.Integracion.Models.Entities;
using SistemaMasajes.Integracion.Services.Interfaces;

namespace SistemaMasajes.Integracion.Services.Implementations
{
    public class ClienteDbService : IClienteDbService
    {
        private readonly SistemaMasajesContext _context;

        public ClienteDbService(SistemaMasajesContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ClienteDTO>> ObtenerClientesAsync()
        {
            var clientes = await _context.Clientes.ToListAsync();
            return clientes.Select(c => new ClienteDTO
            {
                Id = c.Id,
                Nombre = c.NombreCliente,
                Telefono = c.TelefonoCliente,
                Apellido = c.ApellidoCliente,
                Email = c.CorreoCliente,
                FechaRegistro = c.FechaRegistro
            });
        }

        public async Task<ClienteDTO> ObtenerClientePorIdAsync(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return null;

            return new ClienteDTO
            {
                Id = cliente.Id,
                Nombre = cliente.NombreCliente,
                Apellido = cliente.ApellidoCliente,
                Telefono = cliente.TelefonoCliente,
                Email = cliente.CorreoCliente,
                FechaRegistro = cliente.FechaRegistro
            };
        }

        public async Task<ClienteDTO> CrearClienteAsync(ClienteDTO clienteDto)
        {
            var cliente = new Cliente
            {
                NombreCliente = clienteDto.Nombre,
                ApellidoCliente = clienteDto.Apellido,   // agregar
                TelefonoCliente = clienteDto.Telefono,
                CorreoCliente = clienteDto.Email,
                FechaRegistro = DateTime.Now
            };


            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            clienteDto.Id = cliente.Id;
            clienteDto.FechaRegistro = cliente.FechaRegistro;
            return clienteDto;
        }

        public async Task<bool> ActualizarClienteAsync(int id, ClienteDTO clienteDto)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return false;

            cliente.NombreCliente = clienteDto.Nombre;
            cliente.TelefonoCliente = clienteDto.Telefono;
            cliente.CorreoCliente = clienteDto.Email;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> EliminarClienteAsync(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return false;

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}