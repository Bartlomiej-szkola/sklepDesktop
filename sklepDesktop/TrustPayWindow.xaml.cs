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
    public partial class TrustPayWindow : Window
    {
        private readonly BackendService _service;
        private readonly decimal _amount;
        private readonly string _correlationId;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public bool PaymentSuccessful { get; private set; } = false;

        public TrustPayWindow(BackendService service, decimal amount)
        {
            InitializeComponent();
            _service = service;
            _amount = amount;
            _correlationId = "corr-" + Guid.NewGuid().ToString().Substring(0, 8); // Format np z przykladu
            LblAmount.Text = $"Do zapłaty: {_amount:N2} PLN";
            TxtTrustPayCode.Focus();
        }

        private void TxtTrustPayCode_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private async void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            string code = TxtTrustPayCode.Text;
            if (code.Length != 6)
            {
                LblStatus.Text = "Wpisz dokładnie 6 cyfr!";
                return;
            }

            TxtTrustPayCode.IsEnabled = false;
            BtnPay.IsEnabled = false;
            LblStatus.Text = "Komunikacja z siecią TrustPay...";
            LblStatus.Foreground = System.Windows.Media.Brushes.Orange;

            await ConnectWebSocket();

            // UDERZAMY POD TĄ SAMĄ METODĘ W WPF, ALE WSKAZUJEMY INNĄ STRATEGIĘ "TRUSTPAY"
            string initResult = await _service.InitiateCodePayment("TRUSTPAY", code, _amount, "Sklep WPF", _correlationId);

            if (initResult == "PENDING")
            {
                LblStatus.Text = "Oczekiwanie na autoryzację operatora TrustPay (Webhook)...";
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
                _ = ReceiveLoop();
            }
            catch
            {
                MessageBox.Show("Błąd połączenia. Upewnij się, że serwer Sklepu działa.");
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
                        // Sprawdzamy czy otrzymano zwrotkę dla naszej konkretnej płatności
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
                                    this.DialogResult = true;
                                }
                                else
                                {
                                    if (status == "REJECTED") LblStatus.Text = "Płatność odrzucona przez TrustPay!";
                                    else if (status == "EXPIRED") LblStatus.Text = "Czas sesji wygasł.";
                                    else LblStatus.Text = "Błąd transakcji w sieci TrustPay.";

                                    LblStatus.Foreground = System.Windows.Media.Brushes.Red;
                                    ResetUI();
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void ResetUI()
        {
            TxtTrustPayCode.IsEnabled = true;
            BtnPay.IsEnabled = true;
            TxtTrustPayCode.Clear();
            TxtTrustPayCode.Focus();
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