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
        private string ip = Config.StoreBackendUrl;

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

        // Dodaj na górze metody BackendService
        public async Task<(string Name, string Description)?> GetExternalProductInfo(string barcode)
        {
            using (HttpClient externalClient = new HttpClient())
            {
                try
                {
                    // Adres zewnętrznego API
                    string url = $"{Config.ZdroweZakupyUrl}/{barcode}";

                    var response = await externalClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        // Parsujemy odpowiedź
                        var json = await response.Content.ReadAsStringAsync();
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            // Wyciągamy pola name i description (sprawdź w dokumentacji API dokładne nazwy pól)
                            // Zakładam standardowe: "name" i "description"
                            string name = doc.RootElement.GetProperty("name").GetString();
                            string desc = doc.RootElement.TryGetProperty("description", out var descProp)
                                          ? descProp.GetString()
                                          : "";

                            return (name, desc);
                        }
                    }
                    return null; // Kod 400, 404 itp.
                }
                catch
                {
                    return null; // Błąd sieci
                }
            }
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

        // Dodaj do klasy BackendService
        public async Task<bool> UpdateProduct(string barcode, Product updatedProduct)
        {
            try
            {
                // Adres: /api/products/update/123456
                string url = $"{ip}/api/products/update/{barcode}";

                // Podobnie jak przy dodawaniu, wysyłamy obiekt bez ID
                var dataToSend = new
                {
                    barcode = updatedProduct.Barcode,
                    name = updatedProduct.Name,
                    description = updatedProduct.Description,
                    price = updatedProduct.Price,
                    stockQuantity = updatedProduct.StockQuantity
                };

                // Zazwyczaj do aktualizacji używa się PUT, ale jeśli Twój serwer 
                // obsługuje POST pod tym adresem, zamień PutAsJsonAsync na PostAsJsonAsync
                var response = await _httpClient.PutAsJsonAsync(url, dataToSend, _jsonOptions);

                if (response.IsSuccessStatusCode) return true;

                string error = await response.Content.ReadAsStringAsync();
                MessageBox.Show($"Błąd aktualizacji: {error}");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wyjątek podczas aktualizacji: " + ex.Message);
                return false;
            }
        }


        public async Task<bool> AddQuantity(string barcode, int amount)
        {
            try
            {
                string url = $"{ip}/api/products/addQuantity/{barcode}?amount={amount}";
                var response = await _httpClient.PatchAsync(url, null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false; // Błąd połączenia obsłużymy w UI
            }
        }

        public async Task<bool> FinalizeSale(System.Collections.Generic.List<BasketItem> basket)
        {
            try
            {
                var saleRequest = new
                {
                    items = basket.Select(b => new { barcode = b.Barcode, quantity = b.Quantity }).ToList()
                };
                var response = await _httpClient.PostAsJsonAsync($"{ip}/api/products/sale", saleRequest, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }


        // --- OBSŁUGA BLIK POPRZEZ Backend Kasy (Port 8080) ---

        public async Task<string> InitiateBlikPayment(string code, decimal amount, string storeName)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string amountStr = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    // Uderzamy pod adres naszego Sklepu
                    string url = $"{ip}/api/products/blik/initiate?code={code}&amount={amountStr}&storeName={Uri.EscapeDataString(storeName)}";

                    var response = await client.PostAsync(url, null);
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return $"BŁĄD SIECI: {ex.Message}";
                }
            }
        }

        public async Task<string> CheckBlikStatus(string code)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Uderzamy do naszego Sklepu
                    string url = $"{ip}/api/products/blik/status/{code}";
                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                        return await response.Content.ReadAsStringAsync();

                    return "ERROR";
                }
                catch
                {
                    return "ERROR";
                }
            }
        }

        // --- OBSŁUGA TERMINALA KART ---

        public class TerminalStateDto
        {
            public string Status { get; set; }
            public decimal Amount { get; set; }
            public string CardUid { get; set; }
        }

        public async Task<bool> InitiateTerminalPayment(decimal amount)
        {
            string amountStr = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var response = await _httpClient.PostAsync($"{ip}/api/terminal/initiate?amount={amountStr}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<TerminalStateDto> CheckTerminalStatus()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<TerminalStateDto>($"{ip}/api/terminal/status", _jsonOptions);
            }
            catch { return null; }
        }

        public async Task<string> ProcessCardPayment(string cardUid, decimal amount, string storeName, string pin)
        {
            try
            {
                string amountStr = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string url = $"{ip}/api/products/card/charge?cardUid={cardUid}&amount={amountStr}&storeName={Uri.EscapeDataString(storeName)}";
                if (!string.IsNullOrEmpty(pin)) url += $"&pin={pin}";
                var response = await _httpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode) return "SUCCESS";
                return await response.Content.ReadAsStringAsync(); // Zwraca błąd banku
            }
            catch { return "BŁĄD SIECI"; }
        }

        public async Task ClearTerminal()
        {
            await _httpClient.PostAsync($"{ip}/api/terminal/clear", null);
        }
    }
}