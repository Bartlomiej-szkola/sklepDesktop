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
    }
}