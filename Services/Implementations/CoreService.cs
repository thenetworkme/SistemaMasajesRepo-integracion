// En la carpeta Implementations
using SistemaMasajes.Integracion.Services.Interfaces;
using System.Text;
using System.Text.Json;

public class CoreService : ICoreService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public CoreService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;

        // Configurar BaseAddress si no está configurado
        if (_httpClient.BaseAddress == null)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }
    }

    public async Task<T> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            throw new HttpRequestException($"Error en la petición: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            // Log del error
            throw new Exception($"Error inesperado en GET a {endpoint}: {ex.Message}", ex);
        }
    }

    public async Task<T> PostAsync<T>(string endpoint, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            throw new HttpRequestException($"Error en la petición: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error inesperado en POST a {endpoint}: {ex.Message}", ex);
        }
    }

    public async Task<T> PutAsync<T>(string endpoint, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            throw new HttpRequestException($"Error en la petición: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error inesperado en PUT a {endpoint}: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteAsync(string endpoint)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(endpoint);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error inesperado en DELETE a {endpoint}: {ex.Message}", ex);
        }
    }
}