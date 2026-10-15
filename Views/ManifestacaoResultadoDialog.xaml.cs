using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ControleEnvios.Models;
using ControleEnvios.Services;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using WpfClipboard = System.Windows.Clipboard;

namespace ControleEnvios.Views
{
    public partial class ManifestacaoResultadoDialog : Window
    {
        private readonly Envio _envio;
        private ResultadoManifestacao _resultado;
        private readonly Func<Envio, Task<ResultadoManifestacao>>? _onReenviarApi;

        public ManifestacaoResultadoDialog(Envio envio, ResultadoManifestacao resultado, Func<Envio, Task<ResultadoManifestacao>>? onReenviarApi = null)
        {
            InitializeComponent();
            _envio = envio;
            _resultado = resultado;
            _onReenviarApi = onReenviarApi;

            ExibirResultado(_resultado);
        }

        private void ExibirResultado(ResultadoManifestacao res)
        {
            _resultado = res;

            if (res.Sucesso)
            {
                // Modo Sucesso
                BadgeIcone.Background = new SolidColorBrush((Color)MediaColorConverter.ConvertFromString("#064e3b"));
                TxtIcone.Text = "✅";
                TxtTitulo.Text = "Reclamação Aberta nos Correios com Sucesso!";
                TxtTitulo.Foreground = new SolidColorBrush((Color)MediaColorConverter.ConvertFromString("#34d399"));
                TxtSubtitulo.Text = $"Protocolo oficial registrado para o pedido #{_envio.NumeroPedido} ({_envio.Cliente}).";

                PainelSucesso.Visibility = Visibility.Visible;
                PainelRecusa.Visibility = Visibility.Collapsed;

                TxtProtocolo.Text = res.Protocolo;
                BtnReenviarApi.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Modo Recusa / Negado
                BadgeIcone.Background = new SolidColorBrush((Color)MediaColorConverter.ConvertFromString("#450a0a"));
                TxtIcone.Text = "⚠️";
                TxtTitulo.Text = "Abertura Automática Negada pelos Correios";
                TxtTitulo.Foreground = new SolidColorBrush((Color)MediaColorConverter.ConvertFromString("#f87171"));
                TxtSubtitulo.Text = $"Os Correios recusaram a abertura do protocolo neste momento.";

                PainelSucesso.Visibility = Visibility.Collapsed;
                PainelRecusa.Visibility = Visibility.Visible;

                TxtMotivoRecusa.Text = !string.IsNullOrWhiteSpace(res.MotivoRecusa) ? res.MotivoRecusa : "Motivo não detalhado pela API dos Correios.";

                if (res.DataPermitidaReenvio.HasValue)
                {
                    TxtDataPermitida.Text = $"A partir de: {res.DataPermitidaReenvioFormatada} (1º dia útil após o vencimento do prazo)";
                }
                else
                {
                    TxtDataPermitida.Text = "Após o vencimento do prazo oficial de entrega da encomenda.";
                }

                BtnReenviarApi.Visibility = Visibility.Visible;
            }
        }

        private void BtnAbrirPortalCorreios_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Copia os dados para a área de transferência primeiro para facilitar o preenchimento
                WpfClipboard.SetText(_envio.DadosReclamacaoCorreios);

                string url = "https://faleconosco.correios.com.br/faleconosco/app/index.php";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                TxtFeedbackAcao.Text = "✓ Dados copiados para a área de transferência e portal dos Correios aberto no navegador!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível abrir o navegador: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCopiarDados_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WpfClipboard.SetText(_envio.DadosReclamacaoCorreios);
                TxtFeedbackAcao.Text = "✓ Dados completos da reclamação (PI) copiados para a área de transferência!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao copiar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAvisarClienteWhats_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WpfClipboard.SetText(_envio.AvisoAtrasoCliente);
                TxtFeedbackAcao.Text = "✓ Mensagem de WhatsApp para o cliente copiada com sucesso!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao copiar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnReenviarApi_Click(object sender, RoutedEventArgs e)
        {
            BtnReenviarApi.IsEnabled = false;
            TxtIconeReenviar.Text = "⏳";
            TxtTituloReenviar.Text = "Conectando à API dos Correios...";
            TxtFeedbackAcao.Text = "Tentando registrar manifestação nos Correios via API...";

            try
            {
                ResultadoManifestacao novoResultado;
                if (_onReenviarApi != null)
                {
                    novoResultado = await _onReenviarApi(_envio);
                }
                else
                {
                    novoResultado = await CorreiosApiService.AbrirManifestacaoAsync(_envio);
                }

                ExibirResultado(novoResultado);
                TxtFeedbackAcao.Text = novoResultado.Sucesso 
                    ? "✓ Manifestação enviada e protocolo registrado com sucesso!" 
                    : "⚠️ Resposta recebida dos Correios: " + novoResultado.MotivoRecusa;
            }
            catch (Exception ex)
            {
                TxtFeedbackAcao.Text = "Erro ao conectar: " + ex.Message;
            }
            finally
            {
                BtnReenviarApi.IsEnabled = true;
                TxtIconeReenviar.Text = "🔄";
                TxtTituloReenviar.Text = "Tentar Reenviar via API Agora";
            }
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
