using System;
using System.Threading;
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
        private CancellationTokenSource _cts;
        private string _webhookUrl;
        private const int WEBHOOK_PORT = 9091; // Port dla WPF (Android ma 9090)

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
            _cts = new CancellationTokenSource();

            // 1. Uruchamiamy lokalny serwer webhook
            try
            {
                _webhookUrl = _service.StartWebhookListener(WEBHOOK_PORT);
            }
            catch (Exception ex)
            {
                LblStatus.Text = "Błąd uruchomienia webhook: " + ex.Message;
                LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            // 2. Wybudzamy terminal (telefon na Androidzie)
            bool ready = await _service.InitiateTerminalPayment(_amount);
            if (!ready)
            {
                LblStatus.Text = "Brak połączenia z terminalem!";
                LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                _service.StopWebhookListener();
                return;
            }

            // 3. Rejestrujemy nasz webhook w backendzie
            await _service.RegisterWebhook(_webhookUrl);

            LblStatus.Text = "Oczekuję na zbliżenie karty...";
            LblStatus.Foreground = System.Windows.Media.Brushes.White;

            // 4. Nasłuchujemy na webhooki (backend wyśle POST gdy stan się zmieni)
            try
            {
                await _service.ListenForWebhooks(OnStateReceived, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Normalne zamknięcie
            }
        }

        // Wywoływane przez webhook przy każdej zmianie stanu terminala
        private async void OnStateReceived(BackendService.TerminalStateDto state)
        {
            if (state.Status != "CARD_READ") return;

            // Karta odczytana — zatrzymujemy nasłuchiwanie
            _cts?.Cancel();

            // Aktualizacja UI musi być na wątku UI
            Dispatcher.Invoke(() =>
            {
                LblStatus.Text = "Przetwarzanie w banku...";
                LblStatus.Foreground = System.Windows.Media.Brushes.Blue;
            });

            // 3. Wysyłamy żądanie do Banku (przez sklep) Z PINEM!
            string result = await _service.ProcessCardPayment(state.CardUid, _amount, "Sklep WPF", state.Pin);

            // 4. Mówimy terminalowi jaki dźwięk ma zagrać
            if (result == "SUCCESS")
            {
                await _service.SetTerminalResult("SUCCESS");
                PaymentSuccessful = true;

                Dispatcher.Invoke(() =>
                {
                    LblStatus.Text = "ZAAKCEPTOWANO!";
                    LblStatus.Foreground = System.Windows.Media.Brushes.Green;
                });

                await Task.Delay(1500);
                Dispatcher.Invoke(() => { this.DialogResult = true; });
            }
            else
            {
                // Mapujemy błąd z banku na status terminala
                string terminalStatus = result == "INVALID_PIN" ? "REJECTED_PIN" :
                                        result == "NO_FUNDS" ? "REJECTED_FUNDS" : "REJECTED_GENERAL";

                await _service.SetTerminalResult(terminalStatus);

                Dispatcher.Invoke(() =>
                {
                    LblStatus.Text = "TRANSAKCJA ODRZUCONA: " + result;
                    LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                    BtnCancel.Content = "Zamknij";
                });
            }
        }

        private async void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            if (_webhookUrl != null) await _service.UnregisterWebhook(_webhookUrl);
            _service.StopWebhookListener();
            await _service.ClearTerminal();
            this.DialogResult = false;
        }

        protected override async void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            if (_webhookUrl != null) await _service.UnregisterWebhook(_webhookUrl);
            _service.StopWebhookListener();
            await _service.ClearTerminal();
            base.OnClosed(e);
        }
    }
}
