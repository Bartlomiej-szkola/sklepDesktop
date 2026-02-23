using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace sklepDesktop
{
    public class BackendService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public BackendService()
        {
            _httpClient = new HttpClient();
            // ADRES TWOJEGO BACKENDU SPRINGOWEGO:
            _httpClient.BaseAddress = new Uri("http://192.168.0.32:8080/api/products/");

            // KONFIGURACJA JSON: To naprawia problem nulli w Javie!
            // Zamienia automatycznie Barcode -> barcode, StockQuantity -> stockQuantity
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        // Metoda do pobierania produktu (skanowanie)
        public async Task<Product?> GetProductByBarcode(string barcode)
        {
            try
            {
                // GET /api/products/scan/{barcode}
                return await _httpClient.GetFromJsonAsync<Product>($"scan/{barcode}", _jsonOptions);
            }
            catch (HttpRequestException)
            {
                // Błąd HTTP (np. 404 Not Found)
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd połączenia: " + ex.Message);
                return null;
            }
        }

        // Metoda do dodawania produktu
        public async Task<bool> AddProduct(Product newProduct)
        {
            try
            {
                // POST /api/products
                var response = await _httpClient.PostAsJsonAsync("", newProduct, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się dodać: " + ex.Message);
                return false;
            }
        }
    }
}