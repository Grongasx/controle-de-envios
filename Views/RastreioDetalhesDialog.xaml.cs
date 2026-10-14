using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ControleEnvios.Models;
using ControleEnvios.Services;

namespace ControleEnvios.Views
{
    public partial class RastreioDetalhesDialog : Window
    {
        private readonly int _envioId;
        private Envio? _envio;

        public RastreioDetalhesDialog(int envioId)
        {
            InitializeComponent();
            _envioId = envioId;
            CarregarDados();
        }

        private void CarregarDados()
        {
            _envio = EnvioService.ObterEnvioCompleto(_envioId);
            if (_envio == null)
            {
                MessageBox.Show("Envio não encontrado.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            TxtCodigo.Text = !string.IsNullOrWhiteSpace(_envio.CodigoRastreio) ? _envio.CodigoRastreio : "SEM RASTREIO";
            TxtStatus.Text = _envio.StatusCorreios;
            TxtPedidoCliente.Text = $"Pedido #{_envio.NumeroPedido} • Cliente: {_envio.Cliente}";
            TxtOrigemDestino.Text = $"Origem: {_envio.OrigemFormatada} ➔ Destino: {_envio.CidadeDestino}/{_envio.UfDestino} (CEP: {_envio.CepDestino})";

            if (_envio.TemAlertaNaoAtendido)
            {
                BannerNaoAtendido.Visibility = Visibility.Visible;
                TxtBannerNaoAtendidoTitulo.Text = $"⚠️ TENTATIVA DE ENTREGA {_envio.AlertaTentativaFormatada} NÃO EFETUADA (CARTEIRO NÃO ATENDIDO)";
            }
            else
            {
                BannerNaoAtendido.Visibility = Visibility.Collapsed;
            }

            AplicarEstiloBadge(_envio.StatusCorreios, _envio.TemAlertaNaoAtendido);
            RenderizarTimeline();
        }

        private void BtnCopiarWhatsApp_Click(object sender, RoutedEventArgs e)
        {
            if (_envio == null) return;
            try
            {
                System.Windows.Clipboard.SetDataObject(_envio.AvisoTentativaCliente, true);
                MessageBox.Show($"Mensagem copiada para a área de transferência!\n\n\"{_envio.AvisoTentativaCliente}\"", "Aviso Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao copiar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AplicarEstiloBadge(string status, bool temAlertaNaoAtendido = false)
        {
            if (temAlertaNaoAtendido)
            {
                BadgeStatus.Background = new SolidColorBrush(Color.FromRgb(67, 20, 7));
                BadgeStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(249, 115, 22));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(251, 146, 60));
                TxtStatus.Text = $"Não Atendido ({_envio?.AlertaTentativaFormatada ?? "1/3"})";
                return;
            }

            string s = (status ?? "").ToLowerInvariant();
            if (s.Contains("entregue"))
            {
                BadgeStatus.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                BadgeStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(5, 150, 105));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            }
            else if (s.Contains("saiu para entrega"))
            {
                BadgeStatus.Background = new SolidColorBrush(Color.FromRgb(59, 7, 100));
                BadgeStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(126, 34, 206));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252));
            }
            else if (s.Contains("aguardando retirada"))
            {
                BadgeStatus.Background = new SolidColorBrush(Color.FromRgb(69, 26, 3));
                BadgeStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
            }
            else if (s.Contains("trânsito") || s.Contains("transito"))
            {
                BadgeStatus.Background = new SolidColorBrush(Color.FromRgb(23, 37, 84));
                BadgeStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250));
            }
            else if (s.Contains("alerta") || s.Contains("problema") || s.Contains("devolvido"))
            {
                BadgeStatus.Background = new SolidColorBrush(Color.FromRgb(69, 10, 10));
                BadgeStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
            else
            {
                BadgeStatus.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                BadgeStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
        }

        private void RenderizarTimeline()
        {
            PanelTimeline.Children.Clear();

            if (_envio == null || _envio.Historico.Count == 0)
            {
                var txtVazio = new TextBlock
                {
                    Text = "Nenhum evento registrado ainda para este código nos Correios.\nClique em 'Consultar Correios Agora' para verificar se já foi postado.",
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontSize = 13,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 40)
                };
                PanelTimeline.Children.Add(txtVazio);
                return;
            }

            foreach (var ev in _envio.Historico)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14)
                };

                var sp = new StackPanel();

                // Cabeçalho do Card: Data/Hora e Local
                var gridHeader = new Grid();
                gridHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txtData = new TextBlock
                {
                    Text = ev.DataHora,
                    Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12
                };
                Grid.SetColumn(txtData, 0);

                var txtLocal = new TextBlock
                {
                    Text = !string.IsNullOrWhiteSpace(ev.UnidadeLocal) ? ev.UnidadeLocal : "",
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    FontSize = 11
                };
                Grid.SetColumn(txtLocal, 1);

                gridHeader.Children.Add(txtData);
                gridHeader.Children.Add(txtLocal);
                sp.Children.Add(gridHeader);

                // Descrição do Evento
                var txtDesc = new TextBlock
                {
                    Text = ev.Descricao,
                    Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14,
                    Margin = new Thickness(0, 6, 0, 0)
                };
                sp.Children.Add(txtDesc);

                // Destino (se for transferência / encaminhamento)
                if (!string.IsNullOrWhiteSpace(ev.UnidadeDestino))
                {
                    var txtDest = new TextBlock
                    {
                        Text = $"➔ Destino: {ev.UnidadeDestino}",
                        Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248)),
                        FontSize = 12,
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    sp.Children.Add(txtDest);
                }

                // Detalhe extra
                if (!string.IsNullOrWhiteSpace(ev.Detalhe))
                {
                    var txtDet = new TextBlock
                    {
                        Text = ev.Detalhe,
                        Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                        FontSize = 12,
                        Margin = new Thickness(0, 4, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    sp.Children.Add(txtDet);
                }

                card.Child = sp;
                PanelTimeline.Children.Add(card);
            }
        }

        private async void BtnAtualizarAgora_Click(object sender, RoutedEventArgs e)
        {
            if (_envio == null || string.IsNullOrWhiteSpace(_envio.CodigoRastreio))
            {
                MessageBox.Show("Código de rastreio não informado para este envio.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnAtualizarAgora.IsEnabled = false;
            BtnAtualizarAgora.Content = "Consultando...";

            try
            {
                var resultado = await CorreiosApiService.RastrearObjetoAsync(_envio.CodigoRastreio);
                if (resultado.Sucesso)
                {
                    EnvioService.AtualizarRastreio(_envio.Id, resultado);
                    CarregarDados();
                    MessageBox.Show("Rastreamento atualizado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Não foi possível obter dados dos Correios: {resultado.Erro}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro na consulta: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnAtualizarAgora.IsEnabled = true;
                BtnAtualizarAgora.Content = "Consultar Correios Agora";
            }
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
