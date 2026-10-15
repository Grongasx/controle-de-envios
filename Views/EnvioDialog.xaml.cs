using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ControleEnvios.Models;
using ControleEnvios.Services;

namespace ControleEnvios.Views
{
    public partial class EnvioDialog : Window
    {
        private readonly int? _envioId;
        private Envio? _envioEdicao;
        private ObservableCollection<ItemEnvio> _itens = new();
        private string _caminhoPdf = string.Empty;

        public EnvioDialog(int? envioId = null, string? caminhoPdfParaImportar = null)
        {
            InitializeComponent();
            _envioId = envioId;
            GridItens.ItemsSource = _itens;
            _itens.CollectionChanged += (s, e) => AtualizarResumoItens();

            CarregarFornecedores();
            CmbFormaEnvio.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler(TxtValoresFrete_TextChanged));

            if (_envioId.HasValue && _envioId.Value > 0)
            {
                TxtTituloModal.Text = "Editar Envio de Pedido";
                CarregarEnvioExistente(_envioId.Value);
            }
            else
            {
                TxtDataEnvio.Text = DateTime.Now.ToString("dd/MM/yyyy");
                TxtEnderecoOrigemPreview.Text = "Remetente: WebRiders • Estoque Próprio";
                AtualizarResumoItens();

                if (!string.IsNullOrEmpty(caminhoPdfParaImportar) && File.Exists(caminhoPdfParaImportar))
                {
                    ProcessarArquivoPdf(caminhoPdfParaImportar);
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Ajusta dimensões para que o modal nunca extrapole a tela
            var workArea = SystemParameters.WorkArea;
            if (ActualHeight > workArea.Height || Height > workArea.Height)
            {
                Height = Math.Max(520, workArea.Height - 30);
            }
            if (ActualWidth > workArea.Width || Width > workArea.Width)
            {
                Width = Math.Max(750, workArea.Width - 30);
            }

            Left = (workArea.Width - Width) / 2 + workArea.Left;
            Top = (workArea.Height - Height) / 2 + workArea.Top;
        }

        private void CarregarFornecedores()
        {
            var fornecedores = FornecedorService.ListarFornecedores(somenteAtivos: true);
            CmbFornecedores.ItemsSource = fornecedores;
            if (fornecedores.Count > 0 && CmbFornecedores.SelectedIndex == -1)
            {
                CmbFornecedores.SelectedIndex = 0;
            }
        }

        private void CarregarEnvioExistente(int id)
        {
            _envioEdicao = EnvioService.ObterEnvioCompleto(id);
            if (_envioEdicao == null)
            {
                MessageBox.Show("Envio não localizado.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            _caminhoPdf = _envioEdicao.CaminhoPdf;

            TxtNumeroPedido.Text = _envioEdicao.NumeroPedido;
            TxtCliente.Text = _envioEdicao.Cliente;
            TxtCpf.Text = _envioEdicao.ClienteCpf;
            TxtCelular.Text = _envioEdicao.ClienteCelular;
            TxtEmail.Text = _envioEdicao.ClienteEmail;
            TxtEndereco.Text = _envioEdicao.EnderecoEntrega;
            TxtComplemento.Text = _envioEdicao.ComplementoEntrega;
            TxtCidade.Text = _envioEdicao.CidadeDestino;
            TxtUf.Text = _envioEdicao.UfDestino;
            TxtCep.Text = _envioEdicao.CepDestino;

            CmbFormaEnvio.Text = _envioEdicao.FormaEnvioLoja;
            TxtPeso.Text = _envioEdicao.PesoGramas.ToString("F0");
            TxtValorPedido.Text = _envioEdicao.ValorPedido.ToString("F2");
            TxtValorFreteLoja.Text = _envioEdicao.ValorFreteLoja > 0 ? _envioEdicao.ValorFreteLoja.ToString("F2") : "";
            TxtValorCorreios.Text = _envioEdicao.ValorCorreios > 0 ? _envioEdicao.ValorCorreios.ToString("F2") : "";

            TxtNotaFiscal.Text = _envioEdicao.NotaFiscal;
            TxtCodigoRastreio.Text = _envioEdicao.CodigoRastreio;
            TxtDataEnvio.Text = _envioEdicao.DataEnvio;
            TxtObservacoes.Text = _envioEdicao.Observacoes;
            ChkDesativarAvisos.IsChecked = _envioEdicao.DesativarAvisos;

            if (_envioEdicao.TipoRastreio == "REVERSA" || _envioEdicao.IsReversa)
            {
                CmbTipoRastreio.SelectedIndex = 2;
                PainelReversa.Visibility = Visibility.Visible;
            }
            else if (_envioEdicao.TipoRastreio == "PRE_POSTAGEM")
            {
                CmbTipoRastreio.SelectedIndex = 1;
                PainelReversa.Visibility = Visibility.Collapsed;
            }
            else
            {
                CmbTipoRastreio.SelectedIndex = 0;
                PainelReversa.Visibility = Visibility.Collapsed;
            }

            TxtCodigoAutorizacaoReversa.Text = _envioEdicao.CodigoAutorizacaoReversa;
            TxtDataLimiteReversa.Text = _envioEdicao.DataLimiteReversa;

            if (_envioEdicao.OrigemTipo == "DROPSHIPPING")
            {
                RbOrigemDrop.IsChecked = true;
                if (_envioEdicao.FornecedorId.HasValue)
                {
                    CmbFornecedores.SelectedValue = _envioEdicao.FornecedorId.Value;
                }
            }
            else
            {
                RbOrigemProprio.IsChecked = true;
            }

            _itens.Clear();
            foreach (var item in _envioEdicao.Itens)
            {
                _itens.Add(item);
            }

            if (_envioEdicao.CobradoSemPdf && !_envioEdicao.DesativarAvisos)
            {
                BannerCobradoSemPdf.Visibility = Visibility.Visible;
                TxtDetalheCobradoSemPdf.Text = $"⚠️ Este envio foi adicionado via comprovante dos Correios (Gmail) e já teve frete cobrado ({_envioEdicao.ValorCorreios:C2}), porém o PDF da Tray ainda não foi extraído. Arraste ou selecione o PDF abaixo para completar cliente, itens e entrega.";
            }
            else
            {
                BannerCobradoSemPdf.Visibility = Visibility.Collapsed;
            }

            AtualizarPreviewDiferencaFrete();
        }

        private void ProcessarArquivoPdf(string caminhoPdf)
        {
            try
            {
                _caminhoPdf = caminhoPdf;
                var extraido = TrayPdfExtractor.ExtrairDados(caminhoPdf);
                BannerCobradoSemPdf.Visibility = Visibility.Collapsed;

                TxtNumeroPedido.Text = extraido.NumeroPedido;
                TxtCliente.Text = extraido.Cliente;
                TxtCpf.Text = extraido.ClienteCpf;
                TxtCelular.Text = extraido.ClienteCelular;
                TxtEmail.Text = extraido.ClienteEmail;
                TxtEndereco.Text = extraido.EnderecoEntrega;
                TxtComplemento.Text = extraido.ComplementoEntrega;
                TxtCidade.Text = extraido.CidadeDestino;
                TxtUf.Text = extraido.UfDestino;
                TxtCep.Text = extraido.CepDestino;

                CmbFormaEnvio.Text = extraido.FormaEnvioLoja;
                TxtPeso.Text = extraido.PesoGramas > 0 ? extraido.PesoGramas.ToString("F0") : "";
                TxtValorFreteLoja.Text = extraido.ValorFreteLoja > 0 ? extraido.ValorFreteLoja.ToString("F2") : "";
                TxtValorPedido.Text = extraido.ValorPedido > 0 ? extraido.ValorPedido.ToString("F2") : "";

                _itens.Clear();
                foreach (var it in extraido.Itens)
                {
                    _itens.Add(it);
                }

                AtualizarPreviewDiferencaFrete();

                MessageBox.Show($"Dados do Pedido #{extraido.NumeroPedido} ({extraido.Cliente}) extraídos com sucesso do PDF da Tray!", "PDF Processado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao extrair informações do PDF: {ex.Message}", "Falha na Leitura", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtValoresFrete_TextChanged(object sender, TextChangedEventArgs e)
        {
            AtualizarPreviewDiferencaFrete();
        }

        private void CmbFormaEnvio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarPreviewDiferencaFrete();
        }

        private void ChkDesativarAvisos_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkDesativarAvisos.IsChecked == true)
            {
                BannerCobradoSemPdf.Visibility = Visibility.Collapsed;
            }
            else if (_envioEdicao != null && _envioEdicao.ValorCorreios > 0 && !_envioEdicao.TemPdfVinculado)
            {
                BannerCobradoSemPdf.Visibility = Visibility.Visible;
            }
        }

        private void BtnSilenciarAvisosBanner_Click(object sender, RoutedEventArgs e)
        {
            ChkDesativarAvisos.IsChecked = true;
            BannerCobradoSemPdf.Visibility = Visibility.Collapsed;
        }

        private bool _isFormattingCpf = false;
        private void TxtCpf_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingCpf) return;
            string raw = TxtCpf.Text;
            string dig = System.Text.RegularExpressions.Regex.Replace(raw ?? "", @"[^\d]", "");
            if (dig.Length > 14) dig = dig.Substring(0, 14);

            string formatado;
            if (dig.Length <= 11)
            {
                if (dig.Length <= 3) formatado = dig;
                else if (dig.Length <= 6) formatado = $"{dig.Substring(0, 3)}.{dig.Substring(3)}";
                else if (dig.Length <= 9) formatado = $"{dig.Substring(0, 3)}.{dig.Substring(3, 3)}.{dig.Substring(6)}";
                else formatado = $"{dig.Substring(0, 3)}.{dig.Substring(3, 3)}.{dig.Substring(6, 3)}-{dig.Substring(9)}";
            }
            else
            {
                if (dig.Length <= 12) formatado = $"{dig.Substring(0, 2)}.{dig.Substring(2, 3)}.{dig.Substring(5, 3)}/{dig.Substring(8)}";
                else formatado = $"{dig.Substring(0, 2)}.{dig.Substring(2, 3)}.{dig.Substring(5, 3)}/{dig.Substring(8, 4)}-{dig.Substring(12)}";
            }

            if (formatado != raw)
            {
                _isFormattingCpf = true;
                TxtCpf.Text = formatado;
                TxtCpf.CaretIndex = formatado.Length;
                _isFormattingCpf = false;
            }
        }

        private bool _isConsultandoCep = false;
        private string _ultimoCepConsultado = "";

        private async void TxtCep_TextChanged(object sender, TextChangedEventArgs e)
        {
            string limpo = CepService.LimparCep(TxtCep.Text);
            if (limpo.Length == 8 && limpo != _ultimoCepConsultado)
            {
                await ExecutarBuscaCepAsync(sobrescreverTudo: false);
            }
        }

        private async void TxtCep_LostFocus(object sender, RoutedEventArgs e)
        {
            string limpo = CepService.LimparCep(TxtCep.Text);
            if (limpo.Length == 8 && limpo != _ultimoCepConsultado)
            {
                await ExecutarBuscaCepAsync(sobrescreverTudo: false);
            }
        }

        private async void BtnBuscarCep_Click(object sender, RoutedEventArgs e)
        {
            await ExecutarBuscaCepAsync(sobrescreverTudo: true);
        }

        private async System.Threading.Tasks.Task ExecutarBuscaCepAsync(bool sobrescreverTudo)
        {
            string cepRaw = TxtCep.Text;
            string limpo = CepService.LimparCep(cepRaw);
            if (limpo.Length != 8)
            {
                if (sobrescreverTudo)
                {
                    MessageBox.Show("Informe um CEP válido com 8 dígitos.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtCep.Focus();
                }
                return;
            }

            if (_isConsultandoCep) return;
            _isConsultandoCep = true;
            _ultimoCepConsultado = limpo;

            if (BtnBuscarCep != null)
            {
                BtnBuscarCep.IsEnabled = false;
                BtnBuscarCep.Content = "⏳";
            }

            try
            {
                var res = await CepService.ConsultarCepAsync(limpo);
                if (res.Sucesso)
                {
                    TxtCep.Text = res.Cep;
                    TxtCidade.Text = res.Cidade;
                    TxtUf.Text = res.Uf;

                    if (!string.IsNullOrWhiteSpace(res.Logradouro))
                    {
                        if (sobrescreverTudo || string.IsNullOrWhiteSpace(TxtEndereco.Text))
                        {
                            TxtEndereco.Text = res.Logradouro;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(res.Bairro))
                    {
                        if (sobrescreverTudo || string.IsNullOrWhiteSpace(TxtComplemento.Text))
                        {
                            TxtComplemento.Text = res.Bairro;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(TxtEndereco.Text))
                    {
                        TxtEndereco.Focus();
                        TxtEndereco.CaretIndex = TxtEndereco.Text.Length;
                    }
                }
                else if (sobrescreverTudo)
                {
                    MessageBox.Show(res.MensagemErro, "CEP", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                if (sobrescreverTudo)
                {
                    MessageBox.Show($"Falha na consulta do CEP: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                _isConsultandoCep = false;
                if (BtnBuscarCep != null)
                {
                    BtnBuscarCep.IsEnabled = true;
                    BtnBuscarCep.Content = "🔍";
                }
            }
        }

        private void AtualizarPreviewDiferencaFrete()
        {
            if (TxtDiferencaFretePreview == null) return;

            string forma = CmbFormaEnvio?.Text?.Trim().ToLowerInvariant() ?? "";
            if (forma.Contains("grátis") || forma.Contains("gratis") || forma.Contains("retirada") || forma.Contains("retirar"))
            {
                TxtDiferencaFretePreview.Text = "— (Isento / Frete Grátis ou Retirada)";
                TxtDiferencaFretePreview.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#94a3b8"));
                return;
            }

            decimal freteLoja = 0;
            decimal custoCorreios = 0;

            if (decimal.TryParse(TxtValorFreteLoja.Text.Replace("R$", "").Replace(".", "").Replace(",", ".").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var fLoja))
                freteLoja = fLoja;

            if (decimal.TryParse(TxtValorCorreios.Text.Replace("R$", "").Replace(".", "").Replace(",", ".").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var cCor))
                custoCorreios = cCor;

            if (custoCorreios <= 0)
            {
                TxtDiferencaFretePreview.Text = "— (Aguardando Coleta Correios)";
                TxtDiferencaFretePreview.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#94a3b8"));
                return;
            }

            decimal dif = freteLoja - custoCorreios;
            if (dif > 0)
            {
                TxtDiferencaFretePreview.Text = $"+ {dif:C2} (Diferença Positiva / Economia)";
                TxtDiferencaFretePreview.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#34d399"));
            }
            else if (dif < 0)
            {
                TxtDiferencaFretePreview.Text = $"- {Math.Abs(dif):C2} (Diferença Negativa / Custo Extra)";
                TxtDiferencaFretePreview.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#f87171"));
            }
            else
            {
                TxtDiferencaFretePreview.Text = "R$ 0,00 (Valores Iguais)";
                TxtDiferencaFretePreview.Foreground = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#38bdf8"));
            }
        }

        private void DropZonePdf_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string f = files[0];
                    if (Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        ProcessarArquivoPdf(f);
                    }
                }
            }
        }

        private void DropZonePdf_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZonePdf_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Pedidos Tray (PDF)|*.pdf|Todos os Arquivos (*.*)|*.*",
                Title = "Selecione o PDF de Pedido da Tray"
            };
            if (ofd.ShowDialog() == true)
            {
                ProcessarArquivoPdf(ofd.FileName);
            }
        }

        private void RbOrigem_Checked(object sender, RoutedEventArgs e)
        {
            if (GridFornecedorDrop == null) return;

            if (RbOrigemDrop.IsChecked == true)
            {
                GridFornecedorDrop.Visibility = Visibility.Visible;
                AtualizarEnderecoPreviewFornecedor();
            }
            else
            {
                GridFornecedorDrop.Visibility = Visibility.Collapsed;
                TxtEnderecoOrigemPreview.Text = "Remetente: WebRiders • Estoque Próprio";
            }
        }

        private void CmbFornecedores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarEnderecoPreviewFornecedor();
        }

        private void AtualizarEnderecoPreviewFornecedor()
        {
            if (CmbFornecedores.SelectedItem is Fornecedor f)
            {
                TxtEnderecoOrigemPreview.Text = $"Remetente: {f.DisplayName} • Endereço de Coleta: {f.EnderecoCompleto}";
            }
            else
            {
                TxtEnderecoOrigemPreview.Text = "Selecione um fornecedor cadastrado.";
            }
        }

        private void BtnNovoFornecedor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FornecedoresDialog { Owner = this };
            dlg.ShowDialog();
            CarregarFornecedores();
        }

        private void BtnAdicionarItem_Click(object sender, RoutedEventArgs e)
        {
            _itens.Add(new ItemEnvio
            {
                NomeProduto = "Novo Produto",
                Quantidade = 1,
                ValorUnitario = 0,
                ValorTotal = 0
            });
            AtualizarResumoItens();
        }

        private void BtnRemoverItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ItemEnvio item)
            {
                _itens.Remove(item);
                AtualizarResumoItens();
            }
        }

        private void GridItens_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                AtualizarResumoItens();
            });
        }

        private void AtualizarResumoItens()
        {
            if (TxtBadgeItens == null || TxtResumoQtdItens == null || TxtResumoValorItens == null) return;

            int totalItens = _itens.Count;
            int totalPecas = _itens.Sum(i => i.Quantidade > 0 ? i.Quantidade : 1);
            decimal somaValores = _itens.Sum(i => i.ValorTotal);

            TxtBadgeItens.Text = $"{totalItens} {(totalItens == 1 ? "item" : "itens")}";
            TxtResumoQtdItens.Text = $"{totalPecas} un. ({totalItens} {(totalItens == 1 ? "item cadastrado" : "itens cadastrados")})";
            TxtResumoValorItens.Text = $"{somaValores:C2}";

            if (PanelItensVazio != null)
            {
                PanelItensVazio.Visibility = totalItens == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNumeroPedido.Text))
            {
                MessageBox.Show("Informe o Número do Pedido.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNumeroPedido.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtCliente.Text))
            {
                MessageBox.Show("Informe o Nome do Cliente.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCliente.Focus();
                return;
            }

            var envio = _envioEdicao ?? new Envio();
            envio.NumeroPedido = TxtNumeroPedido.Text.Trim();
            envio.Cliente = TxtCliente.Text.Trim();
            envio.ClienteCpf = TxtCpf.Text.Trim();
            envio.ClienteCelular = TxtCelular.Text.Trim();
            envio.ClienteEmail = TxtEmail.Text.Trim();
            envio.EnderecoEntrega = TxtEndereco.Text.Trim();
            envio.ComplementoEntrega = TxtComplemento.Text.Trim();
            envio.CidadeDestino = TxtCidade.Text.Trim();
            envio.UfDestino = TxtUf.Text.Trim().ToUpperInvariant();
            envio.CepDestino = TxtCep.Text.Trim();

            envio.FormaEnvioLoja = (CmbFormaEnvio.Text ?? "").Trim();
            envio.DesativarAvisos = ChkDesativarAvisos.IsChecked == true;

            double.TryParse(TxtPeso.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double peso);
            envio.PesoGramas = peso;

            decimal.TryParse(TxtValorPedido.Text.Replace("R$", "").Replace(".", "").Replace(",", ".").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal vPed);
            envio.ValorPedido = vPed;

            decimal.TryParse(TxtValorFreteLoja.Text.Replace("R$", "").Replace(".", "").Replace(",", ".").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal vFreteLoja);
            envio.ValorFreteLoja = vFreteLoja;

            decimal.TryParse(TxtValorCorreios.Text.Replace("R$", "").Replace(".", "").Replace(",", ".").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal vCor);
            envio.ValorCorreios = vCor;

            envio.OrigemTipo = RbOrigemDrop.IsChecked == true ? "DROPSHIPPING" : "PROPRIO";
            if (envio.OrigemTipo == "DROPSHIPPING" && CmbFornecedores.SelectedValue is int fId)
            {
                envio.FornecedorId = fId;
            }
            else
            {
                envio.FornecedorId = null;
            }

            envio.NotaFiscal = TxtNotaFiscal.Text.Trim();
            envio.CodigoRastreio = TxtCodigoRastreio.Text.Trim().ToUpperInvariant();

            if (CmbTipoRastreio.SelectedIndex == 2)
                envio.TipoRastreio = "REVERSA";
            else if (CmbTipoRastreio.SelectedIndex == 1)
                envio.TipoRastreio = "PRE_POSTAGEM";
            else
                envio.TipoRastreio = "NORMAL";

            envio.CodigoAutorizacaoReversa = TxtCodigoAutorizacaoReversa.Text.Trim();
            envio.DataLimiteReversa = TxtDataLimiteReversa.Text.Trim();
            envio.DataEnvio = TxtDataEnvio.Text.Trim();
            envio.Observacoes = TxtObservacoes.Text.Trim();
            if (!string.IsNullOrWhiteSpace(_caminhoPdf))
            {
                envio.CaminhoPdf = _caminhoPdf;
            }

            envio.Itens = _itens.ToList();

            EnvioService.SalvarEnvio(envio);

            MessageBox.Show("Envio gravado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void CmbTipoRastreio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PainelReversa != null)
            {
                PainelReversa.Visibility = CmbTipoRastreio.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void TxtCodigoRastreio_TextChanged(object sender, TextChangedEventArgs e)
        {
            string texto = (TxtCodigoRastreio.Text ?? "").Trim();
            // Se colar um código numérico de 8 a 11 dígitos no campo de rastreio, detecta reversa
            if (System.Text.RegularExpressions.Regex.IsMatch(texto, @"^\d{8,11}$") && CmbTipoRastreio.SelectedIndex != 2)
            {
                CmbTipoRastreio.SelectedIndex = 2;
                if (PainelReversa != null) PainelReversa.Visibility = Visibility.Visible;
                TxtCodigoAutorizacaoReversa.Text = texto;
                TxtCodigoRastreio.Text = "";
                TxtFeedbackReversa.Text = $"💡 Código numérico ({texto}) identificado como Autorização de Logística Reversa!";
            }
        }

        private async void BtnConsultarReversaApi_Click(object sender, RoutedEventArgs e)
        {
            string cod = TxtCodigoAutorizacaoReversa.Text.Trim();
            if (string.IsNullOrWhiteSpace(cod))
            {
                MessageBox.Show("Informe o Código de Autorização de Postagem para consultar na API dos Correios.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnConsultarReversaApi.IsEnabled = false;
            TxtFeedbackReversa.Text = "⏳ Conectando aos Correios para verificar status da autorização...";

            try
            {
                var res = await CorreiosApiService.ConsultarAutorizacaoReversaAsync(cod);
                if (res.Postado)
                {
                    if (!string.IsNullOrWhiteSpace(res.CodigoObjetoGerado))
                    {
                        TxtCodigoRastreio.Text = res.CodigoObjetoGerado;
                    }
                    TxtFeedbackReversa.Text = $"✅ O objeto já foi postado pelo cliente na agência dos Correios! " +
                                              (!string.IsNullOrWhiteSpace(res.CodigoObjetoGerado) ? $"Código SRO: {res.CodigoObjetoGerado}." : "");
                }
                else
                {
                    TxtFeedbackReversa.Text = $"ℹ️ Status nos Correios: {res.Status}. {res.Detalhe}" +
                                              (res.DataValidade.HasValue ? $" Validade até {res.DataValidade.Value:dd/MM/yyyy}." : "");
                    if (res.DataValidade.HasValue && string.IsNullOrWhiteSpace(TxtDataLimiteReversa.Text))
                    {
                        TxtDataLimiteReversa.Text = res.DataValidade.Value.ToString("dd/MM/yyyy");
                    }
                }
            }
            catch (Exception ex)
            {
                TxtFeedbackReversa.Text = $"⚠️ Erro ao consultar API: {ex.Message}";
            }
            finally
            {
                BtnConsultarReversaApi.IsEnabled = true;
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
