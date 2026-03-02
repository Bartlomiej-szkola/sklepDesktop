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
        private string ip = "http://192.168.0.14:8080";

        public BackendService()
        {
            _httpClient = new HttpClient();
            // ADRES TWOJEGO BACKENDU SPRINGOWEGO:
            _httpClient.BaseAddress = new Uri(ip + "/api/products/");

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
                // 1. Definiujemy pełny adres (żeby uniknąć błędu 404 ze slashem)
                string url = ip + "/api/products";

                // 2. Tworzymy obiekt anonimowy BEZ pola Id.
                // Wyślemy tylko to, co serwer akceptuje.
                var dataToSend = new
                {
                    barcode = newProduct.Barcode,
                    name = newProduct.Name,
                    description = newProduct.Description,
                    price = newProduct.Price,
                    stockQuantity = newProduct.StockQuantity
                };

                // 3. Wysyłamy obiekt anonimowy
                // Używamy _jsonOptions, aby zachować camelCase (np. stockQuantity)
                var response = await _httpClient.PostAsJsonAsync(url, dataToSend, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Serwer odrzucił produkt ({response.StatusCode}):\n{errorBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wyjątek podczas dodawania: " + ex.Message);
                return false;
            }
        }
    }
}