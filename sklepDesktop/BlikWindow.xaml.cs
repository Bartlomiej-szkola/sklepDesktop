using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace sklepDesktop
{
    public partial class BlikWindow : Window
    {
        private readonly BackendService _service;
        private readonly decimal _amount;
        private string _currentCode;
        private bool _isPolling = false;

        public bool PaymentSuccessful { get; private set; } = false;

        public BlikWindow(BackendService service, decimal amount)
        {
            InitializeComponent();
            _service = service;
            _amount = amount;
            LblAmount.Text = $"Do zapłaty: {_amount:N2} PLN";
            TxtBlikCode.Focus();
        }

        // Zezwalamy tylko na wpisywanie cyfr w polu BLIK
        private void TxtBlikCode_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private async void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            string code = TxtBlikCode.Text;
            if (code.Length != 6)
            {
                LblStatus.Text = "Wpisz dokładnie 6 cyfr!";
                return;
            }

            // Blokujemy interfejs na czas płatności
            _currentCode = code;
            TxtBlikCode.IsEnabled = false;
            BtnPay.IsEnabled = false;
            LblStatus.Text = "Łączenie z serwerem BLIK...";
            LblStatus.Foreground = System.Windows.Media.Brushes.Orange;

            // Krok 1: Wysyłamy żądanie do BLIKa
            string initResult = await _service.InitiateBlikPayment(_currentCode, _amount, "Sklep WPF");

            if (initResult == "PENDING")
            {
                // Krok 2: Uruchamiamy pętlę sprawdzającą status
                LblStatus.Text = "Potwierdź płatność w aplikacji banku...";
                StartPollingStatus();
            }
            else
            {
                // Błąd inicjacji (np. nieważny kod)
                LblStatus.Text = initResult;
                LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                ResetUI();
            }
        }

        private async void StartPollingStatus()
        {
            _isPolling = true;
            while (_isPolling)
            {
                await Task.Delay(2000); // Odpytuj co 2 sekundy

                string status = await _service.CheckBlikStatus(_currentCode);

                if (status == "COMPLETED")
                {
                    _isPolling = false;
                    PaymentSuccessful = true;
                    LblStatus.Text = "ZAPŁACONO!";
                    LblStatus.Foreground = System.Windows.Media.Brushes.Green;
                    await Task.Delay(1500); // Pokazujemy na chwilę "Zapłacono!"
                    this.DialogResult = true; // Zamyka okno i zwraca true do MainWindow
                }
                else if (status == "REJECTED")
                {
                    _isPolling = false;
                    LblStatus.Text = "Klient odrzucił płatność w telefonie!";
                    LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                    ResetUI();
                }
                else if (status == "FAILED")
                {
                    _isPolling = false;
                    LblStatus.Text = "Bank odrzucił transakcję (Brak środków?)";
                    LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                    ResetUI();
                }
                else if (status == "EXPIRED")
                {
                    _isPolling = false;
                    LblStatus.Text = "Czas na potwierdzenie minął (2 min).";
                    LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                    ResetUI();
                }
                // Jeśli "PENDING_AUTHORIZATION", pętla po prostu idzie dalej i czeka
            }
        }

        private void ResetUI()
        {
            TxtBlikCode.IsEnabled = true;
            BtnPay.IsEnabled = true;
            TxtBlikCode.Clear();
            TxtBlikCode.Focus();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _isPolling = false;
            this.DialogResult = false;
        }

        // Zatrzymujemy pętlę przy zamknięciu okna przez "X"
        protected override void OnClosed(EventArgs e)
        {
            _isPolling = false;
            base.OnClosed(e);
        }
    }
}