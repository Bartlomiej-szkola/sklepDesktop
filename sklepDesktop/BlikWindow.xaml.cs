using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace sklepDesktop
{
    public partial class BlikWindow : Window
    {
        private readonly BackendService _service;
        private readonly decimal _amount;
        private readonly string _correlationId;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public bool PaymentSuccessful { get; private set; } = false;

        public BlikWindow(BackendService service, decimal amount)
        {
            InitializeComponent();
            _service = service;
            _amount = amount;
            _correlationId = Guid.NewGuid().ToString(); // Unikalne ID naszej sesji płatniczej
            LblAmount.Text = $"Do zapłaty: {_amount:N2} PLN";
            TxtBlikCode.Focus();
        }

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

            TxtBlikCode.IsEnabled = false;
            BtnPay.IsEnabled = false;
            LblStatus.Text = "Łączenie z serwerem BLIK...";
            LblStatus.Foreground = System.Windows.Media.Brushes.Orange;

            // 1. Podłączamy WebSocket (żeby nasłuchiwać odpowiedzi, którą wypchnie asynchroniczny BLIK z backendu)
            await ConnectWebSocket();

            // 2. Zlecamy płatność zlecając naszą unikalną correlationId (Strategia w backendzie wykona resztę)
            string initResult = await _service.InitiateCodePayment("BLIK", code, _amount, Config.ShopName, _correlationId);

            if (initResult == "PENDING")
            {
                LblStatus.Text = "Potwierdź płatność w aplikacji banku (Oczekiwanie WebSocket)...";
            }
            else
            {
                LblStatus.Text = initResult;
                LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                ResetUI();
            }
        }

        private async Task ConnectWebSocket()
        {
            _webSocket = new ClientWebSocket();
            string wsUrl = $"ws://{Config.ServerIp}:8080/ws/terminal";

            try
            {
                await _webSocket.ConnectAsync(new Uri(wsUrl), _cts.Token);
                _ = ReceiveLoop(); // Startujemy nasłuchiwanie w tle
            }
            catch
            {
                MessageBox.Show("Błąd podłączania do WebSocketu Kasy.");
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[1024 * 4];
            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        // Sprawdzamy czy to nasza wiadomość i czy zgadza się ID sesji (żeby nie przechwycić płatności z innej kasy)
                        if (root.TryGetProperty("action", out var act) && act.GetString() == "PAYMENT_RESULT" &&
                            root.TryGetProperty("correlationId", out var corrId) && corrId.GetString() == _correlationId)
                        {
                            string status = root.GetProperty("status").GetString();

                            Dispatcher.Invoke(async () =>
                            {
                                if (status == "COMPLETED")
                                {
                                    PaymentSuccessful = true;
                                    LblStatus.Text = "ZAPŁACONO!";
                                    LblStatus.Foreground = System.Windows.Media.Brushes.Green;
                                    await Task.Delay(1500);
                                    this.DialogResult = true; // Zamyka pomyślnie
                                }
                                else
                                {
                                    if (status == "REJECTED") LblStatus.Text = "Klient odrzucił płatność w telefonie!";
                                    else if (status == "FAILED") LblStatus.Text = "Bank odrzucił transakcję!";
                                    else if (status == "EXPIRED") LblStatus.Text = "Czas na potwierdzenie minął.";

                                    LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                                    ResetUI();
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception) { /* Okno zostało zamknięte */ }
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
            _cts.Cancel();
            this.DialogResult = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts.Cancel();
            base.OnClosed(e);
        }
    }
}