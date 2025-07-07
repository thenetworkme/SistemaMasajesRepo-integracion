using SistemaMasajes.Integracion.Models.DTOs;

namespace SistemaMasajes.Integracion.Services.Interfaces
{
    public interface IClienteDbService
    {
        Task<IEnumerable<ClienteDTO>> ObtenerClientesAsync();
        Task<ClienteDTO> ObtenerClientePorIdAsync(int id);
        Task<ClienteDTO> CrearClienteAsync(ClienteDTO clienteDto);
        Task<bool> ActualizarClienteAsync(int id, ClienteDTO clienteDto);
        Task<bool> EliminarClienteAsync(int id);
    }
}