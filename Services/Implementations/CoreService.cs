using SistemaMasajes.Integracion.Models.DTOs;
using SistemaMasajes.Integracion.Services.Interfaces;
using System.Text.Json;

namespace SistemaMasajes.Integracion.Services.Implementations
{
    public class CoreService : ICoreService
    {
        private readonly HttpClient _httpClient;
        private readonly string _coreApiUrl;

        public CoreService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _coreApiUrl = configuration["ApiUrls:Core"] ?? "https://localhost:5001";
        }

        public async Task<ClienteDTO> ObtenerClienteAsync(int id)
        {
            var response = await _httpClient.GetAsync($"{_coreApiUrl}/api/clientes/{id}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ClienteDTO>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            return null;
        }

        public async Task<IEnumerable<ClienteDTO>> ObtenerClientesAsync()
        {
            var response = await _httpClient.GetAsync($"{_coreApiUrl}/api/clientes");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<IEnumerable<ClienteDTO>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            return new List<ClienteDTO>();
        }

        public async Task<ClienteDTO> CrearClienteAsync(ClienteDTO cliente)
        {
            var json = JsonSerializer.Serialize(cliente);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_coreApiUrl}/api/clientes", content);
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ClienteDTO>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            return null;
        }

        // Implementar métodos similares para CitaDTO...
        public async Task<CitaDTO> CrearCitaAsync(CitaDTO cita)
        {
            // Implementación similar al método CrearClienteAsync
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<CitaDTO>> ObtenerCitasAsync()
        {
            // Implementación similar al método ObtenerClientesAsync
            throw new NotImplementedException();
        }
    }
}