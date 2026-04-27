using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading.Tasks;
using System.Windows;

namespace sklepDesktop
{
    /// <summary>
    /// Logika interakcji dla klasy CardWindows.xaml
    /// </summary>
    public partial class CardWindow : Window
    {
        private readonly BackendService _service;
        private readonly decimal _amount;
        private bool _isPolling = false;

        public bool PaymentSuccessful { get; private set; } = false;

        public CardWindow(BackendService service, decimal amount)
        {
            InitializeComponent();
            _service = service;
            _amount = amount;
            LblAmount.Text = $"{_amount:N2} PLN";

            StartPaymentProcess();
        }

        private async void StartPaymentProcess()
        {
            // 1. Wybudzamy terminal (telefon na Androidzie)
            bool ready = await _service.InitiateTerminalPayment(_amount);
            if (!ready)
            {
                LblStatus.Text = "Brak połączenia z terminalem!";
                LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            // 2. Czekamy na zbliżenie karty (Odpytywanie co 1 sek.)
            _isPolling = true;
            while (_isPolling)
            {
                await Task.Delay(1000);

                var state = await _service.CheckTerminalStatus();
                if (state != null && state.Status == "CARD_READ")
                {
                    _isPolling = false;
                    LblStatus.Text = "Przetwarzanie w banku...";
                    LblStatus.Foreground = System.Windows.Media.Brushes.Blue;

                    // 3. Wysyłamy żądanie ściągnięcia kasy do Banku Niebieskiego
                    string result = await _service.ProcessCardPayment(state.CardUid, _amount, "Sklep WPF");

                    if (result == "SUCCESS")
                    {
                        PaymentSuccessful = true;
                        LblStatus.Text = "ZAAKCEPTOWANO!";
                        LblStatus.Foreground = System.Windows.Media.Brushes.Green;
                        await Task.Delay(1500);
                        this.DialogResult = true; // Zamyka okno z sukcesem
                    }
                    else
                    {
                        LblStatus.Text = result; // Pokazuje błąd np. "Brak środków"
                        LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                        BtnCancel.Content = "Zamknij";
                    }
                }
            }
        }

        private async void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _isPolling = false;
            await _service.ClearTerminal(); // Usypiamy terminal
            this.DialogResult = false;
        }

        protected override async void OnClosed(EventArgs e)
        {
            _isPolling = false;
            await _service.ClearTerminal(); // Upewniamy się, że terminal się zresetuje
            base.OnClosed(e);
        }
    }
}
