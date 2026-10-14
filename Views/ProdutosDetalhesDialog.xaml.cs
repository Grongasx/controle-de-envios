using System;
using System.Linq;
using System.Windows;
using ControleEnvios.Models;
using ControleEnvios.Services;

namespace ControleEnvios.Views
{
    public partial class ProdutosDetalhesDialog : Window
    {
        public ProdutosDetalhesDialog(int envioId)
        {
            InitializeComponent();
            CarregarItens(envioId);
        }

        private void CarregarItens(int envioId)
        {
            var envio = EnvioService.ObterEnvioCompleto(envioId);
            if (envio == null)
            {
                MessageBox.Show("Envio não encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            TxtTituloPedido.Text = $"Produtos do Pedido #{envio.NumeroPedido}";
            TxtCliente.Text = $"Cliente: {envio.Cliente}";

            if (envio.Itens != null && envio.Itens.Count > 0)
            {
                GridItens.ItemsSource = envio.Itens;
                int totalQtd = envio.Itens.Sum(i => i.Quantidade);
                decimal totalValor = envio.Itens.Sum(i => i.ValorTotal > 0 ? i.ValorTotal : (i.Quantidade * i.ValorUnitario));

                TxtTotalItens.Text = $"{totalQtd} {(totalQtd == 1 ? "peça" : "peças")}";
                TxtValorTotal.Text = totalValor.ToString("C2");
                PanelSemItens.Visibility = Visibility.Collapsed;
            }
            else
            {
                PanelSemItens.Visibility = Visibility.Visible;
                TxtResumoGenerico.Text = !string.IsNullOrWhiteSpace(envio.ResumoProdutos) 
                    ? $"Resumo: {envio.ResumoProdutos}" 
                    : "Nenhum resumo de produto disponível.";
                TxtTotalItens.Text = "0 peças";
                TxtValorTotal.Text = envio.ValorPedido > 0 ? envio.ValorPedido.ToString("C2") : "R$ 0,00";
            }
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
