// En la carpeta Implementations
using SistemaMasajes.Integracion.Services.Interfaces;
using System.Text;
using System.Text.Json;
using System.Net.Http; // Necessary for HttpRequestException
using Microsoft.Extensions.Configuration; // Necessary for IConfiguration
using System; // Necessary for Exception

namespace SistemaMasajes.Integracion.Services.Implementations // Ensure the namespace is correct
{
    public class CoreService : ICoreService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public CoreService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;

            // Important Note on BaseAddress:
            // It's generally best practice to configure the BaseAddress
            // directly in Program.cs when you register the HttpClient using AddHttpClient.
            // If you're already doing that, this block below is redundant and can be removed
            // to avoid potential conflicts or unnecessary logic.
            if (_httpClient.BaseAddress == null)
            {
                var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001/";
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
        }

        /// <summary>
        /// Sends a GET request to the Core API and deserializes the response to the specified type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response content to.</typeparam>
        /// <param name="endpoint">The API endpoint (e.g., "Clientes").</param>
        /// <returns>The deserialized object of type T.</returns>
        /// <exception cref="HttpRequestException">Thrown if the HTTP request is unsuccessful.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during the operation.</exception>
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

                // Throw HttpRequestException for non-success status codes, including detailed message
                throw new HttpRequestException($"Error in GET request to {endpoint}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }
            catch (HttpRequestException) // Catch and re-throw the specific HTTP exception
            {
                throw;
            }
            catch (Exception ex)
            {
                // Consider using ILogger for proper logging in a production environment
                throw new Exception($"Unexpected error in GET to {endpoint}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sends a POST request to the Core API and deserializes the response to the specified type.
        /// This version is for when you need the Core API's response data (e.g., generated ID).
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response content to.</typeparam>
        /// <param name="endpoint">The API endpoint (e.g., "Clientes").</param>
        /// <param name="data">The object to be sent in the request body.</param>
        /// <returns>The deserialized object of type T from the Core API's response.</returns>
        /// <exception cref="HttpRequestException">Thrown if the HTTP request is unsuccessful.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during the operation.</exception>
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

                throw new HttpRequestException($"Error in POST request to {endpoint}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error in POST to {endpoint}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sends a POST request to the Core API. This version is for when you only need to
        /// confirm the operation's success and don't need to deserialize the response body.
        /// Primarily used by background synchronization services.
        /// </summary>
        /// <param name="endpoint">The API endpoint (e.g., "Clientes").</param>
        /// <param name="data">The object to be sent in the request body.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">Thrown if the HTTP request is unsuccessful.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during the operation.</exception>
        public async Task PostAsync(string endpoint, object data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);

                // We only care if the operation was successful. No deserialization needed.
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Error in POST request to {endpoint}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error in POST to {endpoint}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sends a PUT request to the Core API and deserializes the response to the specified type.
        /// This version is for when you need the Core API's response data (e.g., the updated entity).
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response content to.</typeparam>
        /// <param name="endpoint">The API endpoint (e.g., "Clientes/{id}").</param>
        /// <param name="data">The object to be sent in the request body.</param>
        /// <returns>The deserialized object of type T from the Core API's response.</returns>
        /// <exception cref="HttpRequestException">Thrown if the HTTP request is unsuccessful.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during the operation.</exception>
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

                throw new HttpRequestException($"Error in PUT request to {endpoint}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error in PUT to {endpoint}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sends a PUT request to the Core API. This version is for when you only need to
        /// confirm the operation's success and don't need to deserialize the response body.
        /// Primarily used by background synchronization services.
        /// </summary>
        /// <param name="endpoint">The API endpoint (e.g., "Clientes/{id}").</param>
        /// <param name="data">The object to be sent in the request body.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">Thrown if the HTTP request is unsuccessful.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during the operation.</exception>
        public async Task PutAsync(string endpoint, object data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(endpoint, content);

                // We only care if the operation was successful. No deserialization needed.
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Error in PUT request to {endpoint}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error in PUT to {endpoint}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sends a DELETE request to the Core API. Only confirms operation success.
        /// </summary>
        /// <param name="endpoint">The API endpoint (e.g., "Clientes/{id}").</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">Thrown if the HTTP request is unsuccessful.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during the operation.</exception>
        public async Task DeleteAsync(string endpoint)
        {
            try
            {
                var response = await _httpClient.DeleteAsync(endpoint);

                // We only care if the operation was successful. No boolean return needed.
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Error in DELETE request to {endpoint}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Unexpected error in DELETE to {endpoint}: {ex.Message}", ex);
            }
        }
    }
}