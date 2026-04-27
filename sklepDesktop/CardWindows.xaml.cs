using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace sklepDesktop
{
    public partial class CardWindow : Window
    {
        private readonly BackendService _service;
        private readonly decimal _amount;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private string _currentCardUid;

        public bool PaymentSuccessful { get; private set; } = false;

        public CardWindow(BackendService service, decimal amount)
        {
            InitializeComponent();
            _service = service;
            _amount = amount;
            LblAmount.Text = $"{_amount:N2} PLN";

            ConnectAndStartProcess();
        }

        private async void ConnectAndStartProcess()
        {
            try
            { // LOGIKA WEBSOCKET NIE POWINNA BYĆ W TYM PLIKU, DO POPRAWY
                _webSocket = new ClientWebSocket();
                string wsUrl = $"ws://{Config.ServerIp}:8080/ws/terminal";
                await _webSocket.ConnectAsync(new Uri(wsUrl), _cts.Token);

                _ = ReceiveLoop();

                string amountStr = _amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                await SendMessageAsync($"{{\"action\":\"INIT\", \"amount\":\"{amountStr}\"}}");
            }
            catch (Exception)
            {
                LblStatus.Text = "Błąd połączenia z terminalem!";
                LblStatus.Foreground = System.Windows.Media.Brushes.Red;
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
                        string action = doc.RootElement.GetProperty("action").GetString();

                        if (action == "CARD_READ")
                        {
                            _currentCardUid = doc.RootElement.GetProperty("cardUid").GetString();

                            Dispatcher.Invoke(() => {
                                LblStatus.Text = "Przetwarzanie...";
                                LblStatus.Foreground = System.Windows.Media.Brushes.Blue;
                            });

                            // 1. Zmiana: Próbujemy bez PIN-u. To bank zdecyduje!
                            await AttemptBankPayment(null);
                        }
                        else if (action == "PIN_ENTERED")
                        {
                            // Terminal dosłał PIN
                            string pin = doc.RootElement.GetProperty("pin").GetString();
                            await AttemptBankPayment(pin);
                        }
                    }
                }
            }
            catch (Exception) { /* Ignorowane podczas zamykania */ }
        }

        private async Task AttemptBankPayment(string pin)
        {
            string bankResult = await _service.ProcessCardPayment(_currentCardUid, _amount, "Sklep WPF", pin);

            if (bankResult == "SUCCESS")
            {
                await SendMessageAsync("{\"action\":\"RESULT\", \"status\":\"SUCCESS\"}");
                Dispatcher.Invoke(async () => {
                    PaymentSuccessful = true;
                    LblStatus.Text = "ZAAKCEPTOWANO!";
                    LblStatus.Foreground = System.Windows.Media.Brushes.Green;
                    await Task.Delay(1500);
                    this.DialogResult = true;
                });
            }
            else if (bankResult == "PIN_REQUIRED")
            {
                // Bank kazał podać PIN! Żądamy go od terminala.
                await SendMessageAsync("{\"action\":\"REQUIRE_PIN\"}");

                Dispatcher.Invoke(() => {
                    LblStatus.Text = "Oczekiwanie na PIN na terminalu...";
                    LblStatus.Foreground = System.Windows.Media.Brushes.Orange;
                });
            }
            else
            {
                // Odrzucenie (brak środków itp.)
                await SendMessageAsync($"{{\"action\":\"RESULT\", \"status\":\"{bankResult}\"}}");
                Dispatcher.Invoke(() => {
                    // Mapowanie błędów na czytelny tekst w kasie
                    if (bankResult == "INVALID_PIN") LblStatus.Text = "ODRZUCONO: Błędny PIN!";
                    else if (bankResult == "NO_FUNDS") LblStatus.Text = "ODRZUCONO: Brak środków!";
                    else LblStatus.Text = $"ODRZUCONO: {bankResult}";

                    LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                    BtnCancel.Content = "Zamknij";
                });
            }
        }

        private async Task SendMessageAsync(string message)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
            }
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