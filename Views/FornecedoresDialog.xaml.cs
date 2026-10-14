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
    }
}
