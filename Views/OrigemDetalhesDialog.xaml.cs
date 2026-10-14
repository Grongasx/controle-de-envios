using System;
using System.Windows;
using System.Windows.Media;
using ControleEnvios.Data;
using ControleEnvios.Models;
using ControleEnvios.Services;

namespace ControleEnvios.Views
{
    public partial class OrigemDetalhesDialog : Window
    {
        private readonly Envio _envio;
        private bool _isProprio;

        public OrigemDetalhesDialog(Envio envio)
        {
            InitializeComponent();
            _envio = envio;
            CarregarDados();
        }

        private void CarregarDados()
        {
            _isProprio = _envio.OrigemTipo == "PROPRIO";

            if (_isProprio)
            {
                TxtIconeTipo.Text = "🏢";
                TxtTituloOrigem.Text = "Origem: Estoque Próprio (Loja)";
                TxtSubtitulo.Text = "Endereço e dados de remetente da própria loja";
                TxtTipoBadge.Text = "ESTOQUE PRÓPRIO";
                BadgeTipo.Background = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#059669"));

                BtnAbrirFornecedores.Visibility = Visibility.Collapsed;
                BtnSalvarLoja.Visibility = Visibility.Visible;

                // Carrega dados da Loja das configurações ou padrão
                TxtNome.Text = DatabaseContext.ObterConfig("loja_origem_nome", "Loja Principal - Estoque Próprio");
                TxtApelido.Text = DatabaseContext.ObterConfig("loja_origem_apelido", "Estoque Próprio");
                TxtCnpj.Text = DatabaseContext.ObterConfig("loja_origem_cnpj", "");
                TxtTelefone.Text = DatabaseContext.ObterConfig("loja_origem_telefone", "");
                TxtEmail.Text = DatabaseContext.ObterConfig("loja_origem_email", "");
                TxtCep.Text = DatabaseContext.ObterConfig("loja_origem_cep", "");
                TxtLogradouro.Text = DatabaseContext.ObterConfig("loja_origem_logradouro", "");
                TxtNumero.Text = DatabaseContext.ObterConfig("loja_origem_numero", "");
                TxtComplemento.Text = DatabaseContext.ObterConfig("loja_origem_complemento", "");
                TxtBairro.Text = DatabaseContext.ObterConfig("loja_origem_bairro", "");
                TxtCidade.Text = DatabaseContext.ObterConfig("loja_origem_cidade", "");
                TxtUf.Text = DatabaseContext.ObterConfig("loja_origem_uf", "");
                TxtObservacoes.Text = DatabaseContext.ObterConfig("loja_origem_obs", "Origem de expedição padrão do estoque da loja.");

                TxtNome.IsReadOnly = false;
                TxtApelido.IsReadOnly = false;
                TxtCnpj.IsReadOnly = false;
                TxtTelefone.IsReadOnly = false;
                TxtEmail.IsReadOnly = false;
                TxtCep.IsReadOnly = false;
                TxtLogradouro.IsReadOnly = false;
                TxtNumero.IsReadOnly = false;
                TxtComplemento.IsReadOnly = false;
                TxtBairro.IsReadOnly = false;
                TxtCidade.IsReadOnly = false;
                TxtUf.IsReadOnly = false;
                TxtObservacoes.IsReadOnly = false;
            }
            else
            {
                TxtIconeTipo.Text = "🏬";
                string nomeForn = !string.IsNullOrWhiteSpace(_envio.FornecedorApelido) ? _envio.FornecedorApelido : (_envio.FornecedorNome ?? "Fornecedor");
                TxtTituloOrigem.Text = $"Origem: {nomeForn} (Dropshipping)";
                TxtSubtitulo.Text = "Mercadoria despachada diretamente pelo fornecedor parceiro";
                TxtTipoBadge.Text = "DROPSHIPPING";
                BadgeTipo.Background = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString("#0284c7"));

                BtnAbrirFornecedores.Visibility = Visibility.Visible;
                BtnSalvarLoja.Visibility = Visibility.Collapsed;

                Fornecedor? f = null;
                if (_envio.FornecedorId.HasValue)
                {
                    f = FornecedorService.ObterPorId(_envio.FornecedorId.Value);
                }

                if (f != null)
                {
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
                }
                else
                {
                    TxtNome.Text = _envio.FornecedorNome ?? "Fornecedor não identificado";
                    TxtApelido.Text = _envio.FornecedorApelido ?? "";
                    TxtObservacoes.Text = "Fornecedor sem cadastro detalhado ou excluído.";
                }

                TxtNome.IsReadOnly = true;
                TxtApelido.IsReadOnly = true;
                TxtCnpj.IsReadOnly = true;
                TxtTelefone.IsReadOnly = true;
                TxtEmail.IsReadOnly = true;
                TxtCep.IsReadOnly = true;
                TxtLogradouro.IsReadOnly = true;
                TxtNumero.IsReadOnly = true;
                TxtComplemento.IsReadOnly = true;
                TxtBairro.IsReadOnly = true;
                TxtCidade.IsReadOnly = true;
                TxtUf.IsReadOnly = true;
                TxtObservacoes.IsReadOnly = true;
            }
        }

        private void BtnSalvarLoja_Click(object sender, RoutedEventArgs e)
        {
            if (!_isProprio) return;

            DatabaseContext.SalvarConfig("loja_origem_nome", TxtNome.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_apelido", TxtApelido.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_cnpj", TxtCnpj.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_telefone", TxtTelefone.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_email", TxtEmail.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_cep", TxtCep.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_logradouro", TxtLogradouro.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_numero", TxtNumero.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_complemento", TxtComplemento.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_bairro", TxtBairro.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_cidade", TxtCidade.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_uf", TxtUf.Text.Trim());
            DatabaseContext.SalvarConfig("loja_origem_obs", TxtObservacoes.Text.Trim());

            MessageBox.Show("Dados de origem da Loja salvos com sucesso!", "Configuração Salva", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void BtnAbrirFornecedores_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FornecedoresDialog { Owner = this };
            dlg.ShowDialog();
            CarregarDados();
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
