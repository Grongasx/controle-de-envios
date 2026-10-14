using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ControleEnvios.Data;
using ControleEnvios.Models;
using ControleEnvios.Services;

namespace ControleEnvios.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DatabaseContext.InitializeDatabase();
            EnvioService.CorrigirInconsistenciasStatus();

            // Configura monitoramento em background
            BackgroundMonitorService.Instance.LogProgresso += OnLogProgresso;
            BackgroundMonitorService.Instance.AtualizacaoConcluida += OnAtualizacaoConcluida;
            BackgroundMonitorService.Instance.Iniciar();

            CarregarDados();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Lê o tamanho da área de trabalho do monitor e impede que a janela extrapole a tela
            var workArea = SystemParameters.WorkArea;
            if (ActualWidth > workArea.Width || Width > workArea.Width)
            {
                Width = Math.Max(800, workArea.Width - 30);
            }
            if (ActualHeight > workArea.Height || Height > workArea.Height)
            {
                Height = Math.Max(550, workArea.Height - 30);
            }

            // Centraliza na tela
            Left = (workArea.Width - Width) / 2 + workArea.Left;
            Top = (workArea.Height - Height) / 2 + workArea.Top;
        }

        private void OnLogProgresso(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatusInferior.Text = msg;
            });
        }

        private void OnAtualizacaoConcluida()
        {
            Dispatcher.Invoke(() =>
            {
                CarregarDados();
            });
        }

        public void CarregarDados()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(CarregarDados);
                return;
            }

            // Executa verificação e arquivamento automático após 7 dias de entregue
            if (DatabaseContext.ObterConfig("arquivar_entregues_7_dias", "1") == "1")
            {
                EnvioService.VerificarEArquivarEntregues7Dias();
            }

            // Métricas
            var m = EnvioService.ObterMetricas();
            TxtCardTotal.Text = m.Total.ToString();
            TxtCardEmTransito.Text = m.EmTransito.ToString();
            TxtCardSaiuEntrega.Text = m.SaiuEntrega.ToString();
            TxtCardRetirada.Text = m.AguardandoRetirada.ToString();
            TxtCardEntregues.Text = m.Entregues.ToString();

            // Card Total Frete da Loja (calculado à parte do saldo / diferença de valor)
            TxtCardTotalFrete.Text = m.TotalFrete.ToString("C2");

            // Card Total Gasto com Correios (comprovantes e-mails de coleta)
            TxtCardGastoCorreios.Text = m.TotalGastoCorreios.ToString("C2");

            // Card Diferença de Valor
            if (m.DiferencaValorTotal > 0)
            {
                TxtCardDiferencaValor.Text = $"+ {m.DiferencaValorTotal:C2}";
                TxtCardDiferencaValor.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#34d399"));
            }
            else if (m.DiferencaValorTotal < 0)
            {
                TxtCardDiferencaValor.Text = $"- {Math.Abs(m.DiferencaValorTotal):C2}";
                TxtCardDiferencaValor.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#f87171"));
            }
            else
            {
                TxtCardDiferencaValor.Text = "R$ 0,00";
                TxtCardDiferencaValor.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#38bdf8"));
            }

            // Filtros
            string busca = TxtBusca.Text;
            string statusFiltro = (CmbFiltroStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TODOS";
            string origemFiltro = (CmbFiltroOrigem.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TODAS";

            var envios = EnvioService.ListarEnvios(busca, statusFiltro, origemFiltro);

            // Reset limpo do ItemsSource para forçar recriação imediata de containers e evitar bug de linhas sem pintura
            GridEnvios.ItemsSource = null;
            GridEnvios.ItemsSource = envios;
            GridEnvios.UpdateLayout();
            GridEnvios.InvalidateVisual();

            int cobradosSemPdf = envios.Count(e => e.CobradoSemPdf);
            if (cobradosSemPdf > 0)
            {
                TxtStatusInferior.Text = $"⚠️ ATENÇÃO: {cobradosSemPdf} envio(s) cobrado(s) pelos Correios (via Gmail/Coleta) aguardando extração do PDF da Tray.";
                TxtStatusInferior.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#fb923c"));
            }
            else
            {
                TxtStatusInferior.Text = "Pronto. Novo Envio disponível com extração automática de PDF da Tray.";
                TxtStatusInferior.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748b"));
            }
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        {
            CarregarDados();
        }

        private void CmbFiltros_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridEnvios != null)
            {
                CarregarDados();
            }
        }

        private void BtnNovoEnvio_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new EnvioDialog { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    CarregarDados();
                }));
            }
        }

        private void AbrirComPdf(string caminhoPdf)
        {
            var dlg = new EnvioDialog(caminhoPdfParaImportar: caminhoPdf) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    CarregarDados();
                }));
            }
        }

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string f = files[0];
                    if (Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        AbrirComPdf(f);
                    }
                    else if (Path.GetExtension(f).Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                             Path.GetExtension(f).Equals(".htm", StringComparison.OrdinalIgnoreCase))
                    {
                        var (atualizados, novos, _) = CorreiosColetaReader.ProcessarArquivoHtml(f);
                        CarregarDados();
                        MessageBox.Show($"Comprovante processado!\n\n• {atualizados} pedido(s) existentes atualizados com custo e rastreio.\n• {novos} novo(s) envio(s) cadastrado(s).", "Coleta Correios", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void BtnImportarHtmlColeta_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Comprovante Coleta Correios (*.html;*.htm)|*.html;*.htm|Todos os Arquivos (*.*)|*.*",
                Title = "Selecione o arquivo HTML de Coleta recebido por e-mail"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var (atualizados, novos, _) = CorreiosColetaReader.ProcessarArquivoHtml(ofd.FileName);
                    CarregarDados();
                    MessageBox.Show($"Comprovante processado!\n\n• {atualizados} pedido(s) existentes atualizados com custo dos Correios e rastreio.\n• {novos} novo(s) envio(s) cadastrado(s).", "Coleta Correios", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao processar HTML: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnFornecedores_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FornecedoresDialog { Owner = this };
            dlg.ShowDialog();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                CarregarDados();
            }));
        }

        private async void BtnAtualizarTodos_Click(object sender, RoutedEventArgs e)
        {
            BtnAtualizarTodos.IsEnabled = false;
            BtnAtualizarTodos.Content = "Consultando...";
            TxtStatusInferior.Text = "Consultando status dos envios na API dos Correios...";

            try
            {
                await BackgroundMonitorService.Instance.VerificarAgoraAsync();
                CarregarDados();
                MessageBox.Show("Verificação concluída!", "Correios API", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao atualizar rastreios: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnAtualizarTodos.IsEnabled = true;
                BtnAtualizarTodos.Content = "🔄 Atualizar Rastreios";
            }
        }

        private void BtnConfiguracoes_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ConfigDialog { Owner = this };
            dlg.ShowDialog();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                CarregarDados();
            }));
        }

        private void BtnCopiarCodigo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Envio env)
            {
                CopiarCodigoPostagem(env);
            }
        }

        private void MenuCopiarCodigo_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                CopiarCodigoPostagem(env);
            }
        }

        private void MenuCopiarPedido_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                CopiarTextoParaClipboard(env.NumeroPedido, $"Número do Pedido #{env.NumeroPedido}");
            }
        }

        private void BtnVerProdutos_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Envio env)
            {
                AbrirProdutosPedido(env.Id);
            }
        }

        private void MenuVerProdutos_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                AbrirProdutosPedido(env.Id);
            }
        }

        private void AbrirProdutosPedido(int envioId)
        {
            var dlg = new ProdutosDetalhesDialog(envioId) { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnVerOrigem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Envio env)
            {
                AbrirOrigemEnvio(env);
            }
        }

        private void MenuVerOrigem_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                AbrirOrigemEnvio(env);
            }
        }

        private void AbrirOrigemEnvio(Envio env)
        {
            var dlg = new OrigemDetalhesDialog(env) { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnAbrirNotaFiscal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Envio env)
            {
                AbrirNotaFiscal(env);
            }
        }

        private void MenuAbrirNotaFiscal_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                AbrirNotaFiscal(env);
            }
        }

        private void AbrirNotaFiscal(Envio env)
        {
            if (string.IsNullOrWhiteSpace(env.NotaFiscal))
            {
                MessageBox.Show($"O pedido #{env.NumeroPedido} ({env.Cliente}) ainda não possui número de Nota Fiscal cadastrado.", "Nota Fiscal", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string pastaNfe = @"C:\Users\Windows\Documents\Gustavo\Nfe Gusta\nfe";
            string numeroLimpo = env.NotaFiscal.Trim();
            string caminhoArquivo = Path.Combine(pastaNfe, $"nf {numeroLimpo}.pdf");

            if (File.Exists(caminhoArquivo))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = caminhoArquivo,
                        UseShellExecute = true
                    });
                    TxtStatusInferior.Text = $"✅ Nota Fiscal {numeroLimpo} aberta com sucesso ({caminhoArquivo})!";
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao abrir o PDF da Nota Fiscal: {ex.Message}", "Erro ao Abrir Arquivo", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Tenta variações na pasta (sem espaço "nf1750.pdf", ou contendo o número)
            if (Directory.Exists(pastaNfe))
            {
                var arquivos = Directory.GetFiles(pastaNfe, $"*{numeroLimpo}*.pdf");
                if (arquivos.Length > 0)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = arquivos[0],
                            UseShellExecute = true
                        });
                        TxtStatusInferior.Text = $"✅ Nota Fiscal {numeroLimpo} localizada e aberta ({arquivos[0]})!";
                        return;
                    }
                    catch { }
                }
            }

            var res = MessageBox.Show(
                $"Arquivo de Nota Fiscal não encontrado:\n\n{caminhoArquivo}\n\nDeseja abrir a pasta das notas fiscais para verificar?",
                "Nota Fiscal Não Encontrada",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    if (!Directory.Exists(pastaNfe))
                    {
                        Directory.CreateDirectory(pastaNfe);
                    }
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pastaNfe,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Não foi possível abrir o diretório: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuCopiarAvisoCliente_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                CopiarTextoParaClipboard(env.AvisoTentativaCliente, $"Aviso WhatsApp ({env.AlertaTentativaFormatada}) para {env.Cliente}");
            }
        }

        private void CopiarCodigoPostagem(Envio env)
        {
            if (string.IsNullOrWhiteSpace(env.CodigoRastreio))
            {
                TxtStatusInferior.Text = $"⚠️ O Pedido #{env.NumeroPedido} ({env.Cliente}) ainda não possui código de postagem/rastreio cadastrado.";
                MessageBox.Show($"O pedido #{env.NumeroPedido} ainda não possui código de postagem cadastrado.", "Código de Postagem", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CopiarTextoParaClipboard(env.CodigoRastreio, $"Código de Postagem {env.CodigoRastreio}");
        }

        private void CopiarTextoParaClipboard(string texto, string descricao)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(texto, true);
                TxtStatusInferior.Text = $"✅ {descricao} copiado para a área de transferência com sucesso!";
            }
            catch
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetText(texto);
                    TxtStatusInferior.Text = $"✅ {descricao} copiado para a área de transferência com sucesso!";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Não foi possível copiar para a área de transferência: {ex.Message}", "Erro ao Copiar", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnVerRastreio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Envio env)
            {
                AbrirDetalhesRastreio(env.Id);
            }
        }

        private void MenuVerRastreio_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                AbrirDetalhesRastreio(env.Id);
            }
        }

        private void AbrirDetalhesRastreio(int envioId)
        {
            var dlg = new RastreioDetalhesDialog(envioId) { Owner = this };
            dlg.ShowDialog();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                CarregarDados();
            }));
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Envio env)
            {
                AbrirEdicaoEnvio(env.Id);
            }
        }

        private void MenuEditar_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                AbrirEdicaoEnvio(env.Id);
            }
        }

        private void AbrirEdicaoEnvio(int envioId)
        {
            var dlg = new EnvioDialog(envioId: envioId) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    CarregarDados();
                }));
            }
        }

        private void BtnVerCliente_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is Envio env)
            {
                AbrirEdicaoEnvio(env.Id);
            }
        }

        private void MenuVerCliente_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                AbrirEdicaoEnvio(env.Id);
            }
        }

        private void BtnVerStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is Envio env)
            {
                AbrirDetalhesRastreio(env.Id);
            }
        }

        private void BtnVerValoresFrete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is Envio env)
            {
                string tipoDiferenca = env.DiferencaValor >= 0 
                    ? "Margem positiva (Lucro sobre frete cobrado)" 
                    : "Diferença negativa (Despesa maior nos Correios)";

                string msg = $"📦 DETALHES DE VALORES DO ENVIO\n\n" +
                             $"• Pedido: #{env.NumeroPedido} ({env.Cliente})\n" +
                             $"• Forma de Envio (Loja): {env.FormaEnvioLoja}\n" +
                             $"• Status Correios: {env.StatusCorreios}\n" +
                             $"• Código Rastreio: {(string.IsNullOrWhiteSpace(env.CodigoRastreio) ? "Não informado" : env.CodigoRastreio)}\n\n" +
                             $"💵 Frete Cobrado na Loja: {env.ValorFreteLoja:C2}\n" +
                             $"📬 Custo Correios (Coleta): {env.ValorCorreios:C2}\n" +
                             $"⚖️ Diferença de Valor: {env.DiferencaValorFormatada} ({tipoDiferenca})\n\n" +
                             $"Deseja abrir a edição completa deste envio?";

                var res = MessageBox.Show(msg, $"Valores do Pedido #{env.NumeroPedido}", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (res == MessageBoxResult.Yes)
                {
                    AbrirEdicaoEnvio(env.Id);
                }
            }
        }

        private void BtnArquivarItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Envio env)
            {
                AlternarArquivamentoEnvio(env);
            }
        }

        private void MenuAlternarArquivado_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                AlternarArquivamentoEnvio(env);
            }
        }

        private void AlternarArquivamentoEnvio(Envio env)
        {
            bool novoStatus = !env.Arquivado;
            string acao = novoStatus ? "arquivar" : "desarquivar";
            var res = MessageBox.Show($"Deseja {acao} o envio do pedido #{env.NumeroPedido} ({env.Cliente})?", "Arquivamento", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                EnvioService.AlternarArquivamento(env.Id, novoStatus);
                TxtStatusInferior.Text = novoStatus 
                    ? $"📦 Pedido #{env.NumeroPedido} arquivado com sucesso." 
                    : $"📦 Pedido #{env.NumeroPedido} desarquivado com sucesso.";
                CarregarDados();
            }
        }

        private void MenuAlternarAvisos_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                bool novoStatus = !env.DesativarAvisos;
                env.DesativarAvisos = novoStatus;
                EnvioService.SalvarEnvio(env);
                TxtStatusInferior.Text = novoStatus
                    ? $"🔕 Avisos desativados para o pedido #{env.NumeroPedido}."
                    : $"🔔 Avisos ativados para o pedido #{env.NumeroPedido}.";
                CarregarDados();
            }
        }

        private void BtnExcluir_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Envio env)
            {
                ExcluirEnvioComConfirmacao(env);
            }
        }

        private void MenuExcluir_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                ExcluirEnvioComConfirmacao(env);
            }
        }

        private void ExcluirEnvioComConfirmacao(Envio env)
        {
            var r = MessageBox.Show($"Deseja excluir o registro do pedido #{env.NumeroPedido} ({env.Cliente})?", "Confirmar Exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                EnvioService.ExcluirEnvio(env.Id);
                CarregarDados();
            }
        }

        private void GridEnvios_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GridEnvios.SelectedItem is Envio env)
            {
                var dlg = new RastreioDetalhesDialog(env.Id) { Owner = this };
                dlg.ShowDialog();
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    CarregarDados();
                }));
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            bool minimizar = DatabaseContext.ObterConfig("minimizar_para_tray", "1") == "1";
            if (minimizar)
            {
                e.Cancel = true;
                Hide();
                App.Notificar("Controle de Envios em Segundo Plano", "O aplicativo continua monitorando seus envios perto do relógio do Windows.");
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }
}
