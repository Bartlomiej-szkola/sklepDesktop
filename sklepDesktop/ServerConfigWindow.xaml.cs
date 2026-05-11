using System.Windows;
using System.Windows.Media;

namespace sklepDesktop
{
    public partial class ServerConfigWindow : Window
    {
        private readonly BackendService _service;

        public ServerConfigWindow()
        {
            InitializeComponent();
            _service = new BackendService();

            // Wczytaj aktualne dane z Config
            TxtIp.Text = Config.ServerIp;
            TxtPort.Text = Config.ServerPort;
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            // Tymczasowo nadpisujemy Config, żeby przetestować wpisane wartości
            string originalIp = Config.ServerIp;
            string originalPort = Config.ServerPort;

            Config.ServerIp = TxtIp.Text.Trim();
            Config.ServerPort = TxtPort.Text.Trim();

            LblTestStatus.Text = "Łączenie...";
            LblTestStatus.Foreground = Brushes.Orange;
            BtnTest.IsEnabled = false;

            bool isAlive = await _service.TestConnection();

            if (isAlive)
            {
                LblTestStatus.Text = "POŁĄCZONO POMYŚLNIE! ✅";
                LblTestStatus.Foreground = Brushes.Green;
            }
            else
            {
                LblTestStatus.Text = "BŁĄD POŁĄCZENIA! ❌";
                LblTestStatus.Foreground = Brushes.Red;
            }

            // Przywracamy stare wartości (zapis nastąpi dopiero po kliknięciu Zapisz)
            Config.ServerIp = originalIp;
            Config.ServerPort = originalPort;
            BtnTest.IsEnabled = true;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtIp.Text) || string.IsNullOrWhiteSpace(TxtPort.Text))
            {
                MessageBox.Show("Pola nie mogą być puste!");
                return;
            }

            Config.ServerIp = TxtIp.Text.Trim();
            Config.ServerPort = TxtPort.Text.Trim();

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}