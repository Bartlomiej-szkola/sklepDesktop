using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace sklepDesktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly BackendService _service;

        public MainWindow()
        {
            InitializeComponent();
            _service = new BackendService();

            // Ustawiamy kursor od razu w polu skanowania po uruchomieniu
            TxtScan.Focus();
        }

        // --- OBSŁUGA SKANOWANIA ---

        // To zdarzenie wykrywa wciśnięcie klawisza w polu skanowania
        private async void TxtScan_KeyDown(object sender, KeyEventArgs e)
        {
            // Skanery kodów kreskowych na końcu wysyłają znak ENTER.
            // Dzięki temu wiemy, że skanowanie się zakończyło.
            if (e.Key == Key.Enter)
            {
                string barcode = TxtScan.Text;
                if (!string.IsNullOrWhiteSpace(barcode))
                {
                    await SearchProduct(barcode);

                    // Opcjonalnie: wyczyść pole po znalezieniu, żeby gotowe było na kolejny towar
                    TxtScan.SelectAll();
                }
            }
        }

        // Przycisk ręczny (jakby ktoś wpisał kod i kliknął myszką)
        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            await SearchProduct(TxtScan.Text);
        }

        private async System.Threading.Tasks.Task SearchProduct(string barcode)
        {
            LblResultName.Text = "Szukam...";

            Product? product = await _service.GetProductByBarcode(barcode);

            if (product != null)
            {
                LblResultName.Text = product.Name;
                LblResultPrice.Text = $"{product.Price:C} PLN"; // Format walutowy
                LblResultDesc.Text = product.Description;
                LblResultStock.Text = $"Stan magazynowy: {product.StockQuantity}";
            }
            else
            {
                LblResultName.Text = "NIE ZNALEZIONO TOWARU";
                LblResultPrice.Text = "-";
                LblResultDesc.Text = "";
                LblResultStock.Text = "";
            }
        }

        // --- OBSŁUGA DODAWANIA ---
        private async void TxtNewBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            // Jeśli skaner wysłał Enter lub użytkownik kliknął Enter
            if (e.Key == Key.Enter)
            {
                string barcode = TxtNewBarcode.Text.Trim();
                if (string.IsNullOrWhiteSpace(barcode)) return;

                // 1. Zaznaczamy, że pobieramy dane
                LblExternalStatus.Text = "Pobieranie danych z bazy zewnętrznej...";
                LblExternalStatus.Foreground = System.Windows.Media.Brushes.Orange;

                // 2. Wywołujemy API zewnętrzne
                var externalInfo = await _service.GetExternalProductInfo(barcode);

                if (externalInfo != null)
                {
                    // 3. Sukces - uzupełniamy pola
                    TxtNewName.Text = externalInfo.Value.Name;
                    TxtNewDesc.Text = externalInfo.Value.Description;

                    LblExternalStatus.Text = "Dane pobrane automatycznie!";
                    LblExternalStatus.Foreground = System.Windows.Media.Brushes.Green;

                    // Przeskakujemy od razu do ceny, żeby było szybciej
                    TxtNewPrice.Focus();
                }
                else
                {
                    // 4. Błąd / Brak produktu
                    LblExternalStatus.Text = "Nie znaleziono produktu w bazie zewnętrznej. Wpisz ręcznie.";
                    LblExternalStatus.Foreground = System.Windows.Media.Brushes.Red;

                    // Ustawiamy fokus na nazwę, żeby wpisać ręcznie
                    TxtNewName.Focus();
                }
            }
        }
        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Prosta walidacja
            if (!decimal.TryParse(TxtNewPrice.Text, out decimal price))
            {
                MessageBox.Show("Błędna cena!");
                return;
            }
            if (!int.TryParse(TxtNewQty.Text, out int qty))
            {
                MessageBox.Show("Błędna ilość!");
                return;
            }

            var newProduct = new Product
            {
                Barcode = TxtNewBarcode.Text,
                Name = TxtNewName.Text,
                Description = TxtNewDesc.Text,
                Price = price,
                StockQuantity = qty
            };

            LblStatus.Text = "Wysyłanie...";
            bool success = await _service.AddProduct(newProduct);

            if (success)
            {
                LblStatus.Text = "Dodano pomyślnie!";
                // Wyczyść formularz
                TxtNewBarcode.Clear();
                TxtNewName.Clear();
                TxtNewDesc.Clear();
                TxtNewPrice.Clear();
                TxtNewQty.Clear();

                // Przywróć fokus na skaner, żeby można było od razu pracować
                TxtScan.Focus();
            }
            else
            {
                LblStatus.Text = "Błąd podczas dodawania (sprawdź czy kod już nie istnieje).";
            }
        }


        // UPDATE panel
        // 1. Skanowanie kodu w celu pobrania danych do edycji
        private async void TxtUpdateSearchBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string barcode = TxtUpdateSearchBarcode.Text.Trim();
                if (string.IsNullOrWhiteSpace(barcode)) return;

                LblUpdateSearchStatus.Text = "Pobieranie danych z bazy...";
                var product = await _service.GetProductByBarcode(barcode);

                if (product != null)
                {
                    // Wypełniamy pola edycji
                    TxtUpdateName.Text = product.Name;
                    TxtUpdateDesc.Text = product.Description;
                    TxtUpdatePrice.Text = product.Price.ToString();
                    TxtUpdateQty.Text = product.StockQuantity.ToString();

                    // Aktywujemy pola edycji
                    GroupEditFields.IsEnabled = true;
                    LblUpdateSearchStatus.Text = "Produkt znaleziony. Możesz edytować.";
                    LblUpdateSearchStatus.Foreground = System.Windows.Media.Brushes.Green;

                    TxtUpdateName.Focus();
                }
                else
                {
                    GroupEditFields.IsEnabled = false;
                    LblUpdateSearchStatus.Text = "Nie znaleziono produktu w Twojej bazie!";
                    LblUpdateSearchStatus.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
        }

        // 2. Zapisywanie zaktualizowanych danych
        private async void BtnUpdateSave_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja pól
            if (!decimal.TryParse(TxtUpdatePrice.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price) ||
                !int.TryParse(TxtUpdateQty.Text, out int qty))
            {
                MessageBox.Show("Błędna cena lub ilość!");
                return;
            }

            var updatedProduct = new Product
            {
                Barcode = TxtUpdateSearchBarcode.Text, // Oryginalny kod
                Name = TxtUpdateName.Text,
                Description = TxtUpdateDesc.Text,
                Price = price,
                StockQuantity = qty
            };

            // Uwaga: metoda w BackendService przyjmuje barcode i obiekt.
            bool result = await _service.UpdateProduct(updatedProduct.Barcode, updatedProduct);

            if (result)
            {
                LblUpdateResult.Text = "Zaktualizowano pomyślnie!";
                LblUpdateResult.Foreground = System.Windows.Media.Brushes.Green;

                // Opcjonalnie: wyczyść pola i zablokuj panel
                GroupEditFields.IsEnabled = false;
                TxtUpdateSearchBarcode.Clear();
                TxtUpdateSearchBarcode.Focus();
            }
            else
            {
                LblUpdateResult.Text = "Błąd podczas zapisu!";
                LblUpdateResult.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
    }
}