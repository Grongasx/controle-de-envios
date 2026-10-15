using System;
using System.Windows;
using System.Windows.Controls;
using ControleEnvios.Models;
using ControleEnvios.Services;

namespace ControleEnvios.Views
{
    public partial class FornecedoresDialog : Window
    {
        private Fornecedor? _fornecedorAtual;

        public FornecedoresDialog()
        {
            InitializeComponent();
            CarregarLista();
        }

        private void CarregarLista()
        {
            var lista = FornecedorService.ListarFornecedores(somenteAtivos: true);
            ListFornecedores.ItemsSource = lista;
            if (lista.Count > 0)
            {
                ListFornecedores.SelectedIndex = 0;
            }
            else
            {
                LimparFormulario();
            }
        }

        private void ListFornecedores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListFornecedores.SelectedItem is Fornecedor f)
            {
                _fornecedorAtual = f;
                TxtTituloForm.Text = $"Editar: {f.DisplayName}";
                TxtNome.Text = f.Nome;
                TxtApelido.Text = f.Apelido;
                TxtCnpj.Text = f.CnpjCpf;
                TxtTelefone.Text = f.Telefone;
                TxtEmail.Text = f.Email;
                TxtCep.Text = f.Cep;
                TxtLogradouro.Text = f.Logradouro;
                TxtNumero.Text = f.Numero;
                TxtComplemento.Text = f.Complemento;
                TxtBairro.Text = f.Bairro;
                TxtCidade.Text = f.Cidade;
                TxtUf.Text = f.Uf;
                TxtObservacoes.Text = f.Observacoes;
                BtnExcluir.Visibility = Visibility.Visible;
            }
        }

        private void BtnNovo_Click(object sender, RoutedEventArgs e)
        {
            LimparFormulario();
        }

        private void LimparFormulario()
        {
            _fornecedorAtual = null;
            ListFornecedores.SelectedItem = null;
            TxtTituloForm.Text = "Novo Fornecedor";
            TxtNome.Text = "";
            TxtApelido.Text = "";
            TxtCnpj.Text = "";
            TxtTelefone.Text = "";
            TxtEmail.Text = "";
            TxtCep.Text = "";
            TxtLogradouro.Text = "";
            TxtNumero.Text = "";
            TxtComplemento.Text = "";
            TxtBairro.Text = "";
            TxtCidade.Text = "";
            TxtUf.Text = "";
            TxtObservacoes.Text = "";
            BtnExcluir.Visibility = Visibility.Collapsed;
            TxtNome.Focus();
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNome.Text))
            {
                MessageBox.Show("Informe a Razão Social ou Nome do fornecedor.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNome.Focus();
                return;
            }

            var f = _fornecedorAtual ?? new Fornecedor();
            f.Nome = TxtNome.Text.Trim();
            f.Apelido = TxtApelido.Text.Trim();
            f.CnpjCpf = TxtCnpj.Text.Trim();
            f.Telefone = TxtTelefone.Text.Trim();
            f.Email = TxtEmail.Text.Trim();
            f.Cep = TxtCep.Text.Trim();
            f.Logradouro = TxtLogradouro.Text.Trim();
            f.Numero = TxtNumero.Text.Trim();
            f.Complemento = TxtComplemento.Text.Trim();
            f.Bairro = TxtBairro.Text.Trim();
            f.Cidade = TxtCidade.Text.Trim();
            f.Uf = TxtUf.Text.Trim().ToUpperInvariant();
            f.Observacoes = TxtObservacoes.Text.Trim();
            f.Ativo = true;

            FornecedorService.Salvar(f);
            MessageBox.Show("Fornecedor salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);

            CarregarLista();
        }

        private void BtnExcluir_Click(object sender, RoutedEventArgs e)
        {
            if (_fornecedorAtual == null) return;

            var r = MessageBox.Show($"Deseja realmente desativar o fornecedor {_fornecedorAtual.DisplayName}?", "Confirmar Exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                FornecedorService.Excluir(_fornecedorAtual.Id);
                CarregarLista();
            }
        }

        private bool _isFormattingCnpj = false;
        private void TxtCnpj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingCnpj) return;
            string raw = TxtCnpj.Text;
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
                _isFormattingCnpj = true;
                TxtCnpj.Text = formatado;
                TxtCnpj.CaretIndex = formatado.Length;
                _isFormattingCnpj = false;
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
                        if (sobrescreverTudo || string.IsNullOrWhiteSpace(TxtLogradouro.Text))
                        {
                            TxtLogradouro.Text = res.Logradouro;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(res.Bairro))
                    {
                        if (sobrescreverTudo || string.IsNullOrWhiteSpace(TxtBairro.Text))
                        {
                            TxtBairro.Text = res.Bairro;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(TxtNumero.Text))
                    {
                        TxtNumero.Focus();
                    }
                    else if (!string.IsNullOrWhiteSpace(TxtLogradouro.Text))
                    {
                        TxtLogradouro.Focus();
                        TxtLogradouro.CaretIndex = TxtLogradouro.Text.Length;
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
    }
}
