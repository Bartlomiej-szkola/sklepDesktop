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
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;


namespace sklepDesktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // --- WIN32 API CONSTANTS ---
        private const int WM_SYSCOMMAND = 0x0112;
        private const int MF_STRING = 0x0000;
        private const int MF_SEPARATOR = 0x0800;
        private const int IDM_CHANGESERVER = 1001; // Nasze unikalne ID menu

        // --- WIN32 API IMPORT ---
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool InsertMenu(IntPtr hMenu, int uPosition, uint uFlags, uint uIDNewItem, string lpNewItem);
        // --- WIN32 END ---


        private readonly BackendService _service;
        public ObservableCollection<BasketItem> Basket { get; set; } = new ObservableCollection<BasketItem>();

        public MainWindow()
        {
            InitializeComponent();
            _service = new BackendService();

            // Ustawiamy kursor od razu w polu skanowania po uruchomieniu
            TxtScan.Focus();
            DgBasket.ItemsSource = Basket;

            // Rejestrujemy zdarzenie załadowania okna
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Pobieramy uchwyt (Handle) okna WPF
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr hMenu = GetSystemMenu(hwnd, false);

            // Dodajemy separator i nową opcję
            InsertMenu(hMenu, -1, MF_SEPARATOR, 0, string.Empty);
            InsertMenu(hMenu, -1, MF_STRING, IDM_CHANGESERVER, "Ustawienia serwera...");

            // Podpinamy się pod pętlę komunikatów Windows
            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);
        }

        // Metoda przechwytująca kliknięcia w menu systemowe
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SYSCOMMAND && wParam.ToInt32() == IDM_CHANGESERVER)
            {
                ShowChangeServerDialog();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void ShowChangeServerDialog()
        {
            var configWindow = new ServerConfigWindow();
            configWindow.Owner = this; // Żeby okno było wyśrodkowane względem głównego

            if (configWindow.ShowDialog() == true)
            {
                // Jeśli użytkownik kliknął Zapisz
                MessageBox.Show($"Zaktualizowano adres serwera!\nNowy adres: {Config.StoreBackendUrl}",
                                "Konfiguracja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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


        // --- LOGIKA PANELU DOSTAW ---

        private async void TxtDeliveryBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string barcode = TxtDeliveryBarcode.Text.Trim();
                if (string.IsNullOrWhiteSpace(barcode)) return;

                // Reset statusów
                LblDeliveryScanStatus.Text = "Szukanie w bazie...";
                LblDeliveryScanStatus.Foreground = System.Windows.Media.Brushes.Orange;
                LblDeliveryResult.Text = "";

                try
                {
                    var product = await _service.GetProductByBarcode(barcode);

                    if (product != null)
                    {
                        // SUKCES: Produkt znaleziony
                        LblDeliveryProdName.Text = $"Produkt: {product.Name}";
                        LblDeliveryCurrentStock.Text = $"Obecny stan: {product.StockQuantity} szt.";
                        LblDeliveryScanStatus.Text = "Znaleziono. Wpisz ilość po prawej.";
                        LblDeliveryScanStatus.Foreground = System.Windows.Media.Brushes.Green;

                        GroupDeliveryFields.IsEnabled = true;
                        TxtDeliveryAmount.Focus();
                    }
                    else
                    {
                        // BŁĄD: Brak produktu
                        LblDeliveryScanStatus.Text = "BŁĄD: Nie znaleziono produktu w bazie!";
                        LblDeliveryScanStatus.Foreground = System.Windows.Media.Brushes.Red;
                        LblDeliveryProdName.Text = "Produkt: -";
                        LblDeliveryCurrentStock.Text = "Obecny stan: -";
                        GroupDeliveryFields.IsEnabled = false;
                    }
                }
                catch (Exception)
                {
                    // BŁĄD: Brak połączenia
                    LblDeliveryScanStatus.Text = "BŁĄD: Brak połączenia z serwerem!";
                    LblDeliveryScanStatus.Foreground = System.Windows.Media.Brushes.Red;
                    GroupDeliveryFields.IsEnabled = false;
                }
            }
        }

        private async void BtnDeliverySave_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtDeliveryAmount.Text, out int amount) || amount <= 0)
            {
                LblDeliveryResult.Text = "Wpisz poprawną liczbę!";
                LblDeliveryResult.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            LblDeliveryResult.Text = "Zapisywanie...";
            LblDeliveryResult.Foreground = System.Windows.Media.Brushes.Orange;

            bool success = await _service.AddQuantity(TxtDeliveryBarcode.Text, amount);

            if (success)
            {
                LblDeliveryResult.Text = $"SUKCES: Dodano {amount} sztuk!";
                LblDeliveryResult.Foreground = System.Windows.Media.Brushes.Green;

                // Czyścimy wszystko po 2 sekundach lub czekamy na nowy skan
                GroupDeliveryFields.IsEnabled = false;
                TxtDeliveryBarcode.Clear();
                TxtDeliveryBarcode.Focus();
                LblDeliveryProdName.Text = "Produkt: -";
                LblDeliveryCurrentStock.Text = "Obecny stan: -";
                LblDeliveryScanStatus.Text = "Czekam na kolejny skan...";
                LblDeliveryScanStatus.Foreground = System.Windows.Media.Brushes.Gray;
            }
            else
            {
                LblDeliveryResult.Text = "BŁĄD: Nie udało się zaktualizować bazy.";
                LblDeliveryResult.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        // KASA
        // 1. Skanowanie w kasie
        private async void TxtCashierScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string barcode = TxtCashierScan.Text.Trim();
                TxtCashierScan.Clear();
                if (string.IsNullOrWhiteSpace(barcode)) return;

                var product = await _service.GetProductByBarcode(barcode);

                if (product != null)
                {
                    // Sprawdź czy produkt już jest w koszyku
                    var existing = Basket.FirstOrDefault(b => b.Barcode == barcode);
                    if (existing != null)
                    {
                        existing.Quantity++;
                    }
                    else
                    {
                        Basket.Add(new BasketItem
                        {
                            Barcode = product.Barcode,
                            Name = product.Name,
                            Price = product.Price,
                            Quantity = 1
                        });
                    }
                    UpdateTotal();
                    LblCashierStatus.Text = $"Dodano: {product.Name}";
                    LblCashierStatus.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    LblCashierStatus.Text = "BŁĄD: Nieznany produkt!";
                    LblCashierStatus.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
        }

        // 2. Aktualizacja sumy
        private void UpdateTotal()
        {
            decimal total = Basket.Sum(item => item.Total);
            LblTotalSum.Text = $"{total:N2} PLN";
        }

        // 3. Finalizacja sprzedaży
        private async void BtnFinalizeSale_Click(object sender, RoutedEventArgs e)
        {
            if (Basket.Count == 0)
            {
                MessageBox.Show("Koszyk jest pusty!");
                return;
            }

            decimal totalAmount = Basket.Sum(item => item.Total);

            // KROK 1: Wywołujemy terminal płatniczy
            BlikWindow blikTerminal = new BlikWindow(_service, totalAmount);
            blikTerminal.Owner = this; // Wyświetla okno na środku głównego ekranu

            // Kod czeka tutaj, dopóki okienko BLIKa się nie zamknie
            bool? paymentResult = blikTerminal.ShowDialog();

            // KROK 2: Jeśli płatność się powiodła (zwrócono true)
            if (paymentResult == true)
            {
                // Pieniądze są już pobrane z Banku Niebieskiego przez serwer BLIK.
                // Teraz aktualizujemy stany magazynowe w Sklepie.
                bool storeUpdated = await _service.FinalizeSale(Basket.ToList());

                if (storeUpdated)
                {
                    MessageBox.Show("PARAGON WYDRUKOWANY. Dziękujemy za zakupy!", "Sukces");
                    Basket.Clear();
                    UpdateTotal();
                    LblCashierStatus.Text = "Zeskanuj produkt...";
                }
                else
                {
                    // Płatność przeszła, ale padł serwer sklepowy
                    MessageBox.Show("UWAGA: Płatność pobrana, ale wystąpił błąd z aktualizacją bazy sklepu!", "Krytyczny Błąd");
                }
            }
            else
            {
                // Klient zamknął terminal albo anulował płatność
                MessageBox.Show("Płatność anulowana. Możesz kontynuować dodawanie do koszyka.", "Kasa");
            }
        }

        private async void BtnPayCard_Click(object sender, RoutedEventArgs e)
        {
            if (Basket.Count == 0) return;

            decimal totalAmount = Basket.Sum(item => item.Total);

            // Wywołujemy terminal w WPF (który wybudza telefon)
            CardWindow cardTerminal = new CardWindow(_service, totalAmount);
            cardTerminal.Owner = this;

            bool? paymentResult = cardTerminal.ShowDialog();

            if (paymentResult == true)
            {
                // Aktualizujemy stany magazynowe w Sklepie
                bool storeUpdated = await _service.FinalizeSale(Basket.ToList());

                if (storeUpdated)
                {
                    MessageBox.Show("PARAGON WYDRUKOWANY. Dziękujemy za zakupy kartą!", "Sukces");
                    Basket.Clear();
                    UpdateTotal();
                    LblCashierStatus.Text = "Zeskanuj produkt...";
                }
            }
        }

        // 4. Funkcje pomocnicze
        private void BtnRemoveLast_Click(object sender, RoutedEventArgs e)
        {
            if (Basket.Count > 0) { Basket.RemoveAt(Basket.Count - 1); UpdateTotal(); }
        }

        private void BtnClearBasket_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Czy na pewno wyczyścić cały koszyk?", "Kasa", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Basket.Clear();
                UpdateTotal();
            }
        }




    }
}