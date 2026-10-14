using System;
using System.Windows;
using Microsoft.Win32;
using ControleEnvios.Data;
using ControleEnvios.Services;

namespace ControleEnvios.Views
{
    public partial class ConfigDialog : Window
    {
        public ConfigDialog()
        {
            InitializeComponent();
            AjustarTamanhoJanela();
            CarregarConfiguracoes();
        }

        private void AjustarTamanhoJanela()
        {
            var workArea = SystemParameters.WorkArea;
            if (Height > workArea.Height)
            {
                Height = Math.Max(500, workArea.Height - 40);
            }
            if (Width > workArea.Width)
            {
                Width = Math.Max(550, workArea.Width - 40);
            }
        }

        private void CarregarConfiguracoes()
        {
            // Correios
            TxtUsuario.Text = DatabaseContext.ObterConfig("correios_usuario", "");
            TxtCodigoAcesso.Password = DatabaseContext.ObterConfig("correios_codigo_acesso", "");

            // Background
            TxtIntervalo.Text = DatabaseContext.ObterConfig("intervalo_minutos_verificacao", "60");
            ChkMinimizarTray.IsChecked = DatabaseContext.ObterConfig("minimizar_para_tray", "1") == "1";
            ChkNotificar.IsChecked = DatabaseContext.ObterConfig("notificar_desktop", "1") == "1";

            // E-mail Coleta
            TxtEmailHost.Text = DatabaseContext.ObterConfig("email_host", "imap.gmail.com");
            TxtEmailPorta.Text = DatabaseContext.ObterConfig("email_porta", "993");
            TxtEmailUsuario.Text = DatabaseContext.ObterConfig("email_usuario", "");
            TxtEmailSenha.Password = DatabaseContext.ObterConfig("email_senha", "");
            TxtEmailRemetenteFiltro.Text = DatabaseContext.ObterConfig("email_remetente_filtro", "naorespondacorreios@gmail.com");

            // NFe API
            TxtNfeApiUrl.Text = DatabaseContext.ObterConfig("nfe_api_url", "");
            TxtNfeApiToken.Password = DatabaseContext.ObterConfig("nfe_api_token", "");

            // Arquivamento Automático
            ChkArquivar7Dias.IsChecked = DatabaseContext.ObterConfig("arquivar_entregues_7_dias", "1") == "1";
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            // Correios
            DatabaseContext.SalvarConfig("correios_usuario", TxtUsuario.Text.Trim());
            if (!string.IsNullOrEmpty(TxtCodigoAcesso.Password))
            {
                DatabaseContext.SalvarConfig("correios_codigo_acesso", TxtCodigoAcesso.Password.Trim());
            }

            // Monitoramento
            if (int.TryParse(TxtIntervalo.Text, out int intervalo) && intervalo > 0)
            {
                DatabaseContext.SalvarConfig("intervalo_minutos_verificacao", intervalo.ToString());
            }
            DatabaseContext.SalvarConfig("minimizar_para_tray", ChkMinimizarTray.IsChecked == true ? "1" : "0");
            DatabaseContext.SalvarConfig("notificar_desktop", ChkNotificar.IsChecked == true ? "1" : "0");

            // Arquivamento Automático
            DatabaseContext.SalvarConfig("arquivar_entregues_7_dias", ChkArquivar7Dias.IsChecked == true ? "1" : "0");

            // E-mail Coleta
            DatabaseContext.SalvarConfig("email_host", TxtEmailHost.Text.Trim());
            DatabaseContext.SalvarConfig("email_porta", TxtEmailPorta.Text.Trim());
            DatabaseContext.SalvarConfig("email_usuario", TxtEmailUsuario.Text.Trim());
            if (!string.IsNullOrEmpty(TxtEmailSenha.Password))
            {
                DatabaseContext.SalvarConfig("email_senha", TxtEmailSenha.Password.Replace(" ", "").Trim());
            }
            DatabaseContext.SalvarConfig("email_remetente_filtro", TxtEmailRemetenteFiltro.Text.Trim());

            // NFe API
            DatabaseContext.SalvarConfig("nfe_api_url", TxtNfeApiUrl.Text.Trim());
            if (!string.IsNullOrEmpty(TxtNfeApiToken.Password))
            {
                DatabaseContext.SalvarConfig("nfe_api_token", TxtNfeApiToken.Password.Trim());
            }

            MessageBox.Show("Configurações salvas com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void BtnExecutarArquivamentoAgora_Click(object sender, RoutedEventArgs e)
        {
            int qtd = EnvioService.VerificarEArquivarEntregues7Dias();
            TxtStatusArquivamento.Text = qtd > 0
                ? $"✅ {qtd} envio(s) entregue(s) há mais de 7 dias foram arquivados com sucesso!"
                : "ℹ️ Nenhum envio entregue pendente de arquivamento encontrado.";
        }

        private async void BtnTestarConexao_Click(object sender, RoutedEventArgs e)
        {
            BtnTestarConexao.IsEnabled = false;
            TxtStatusTeste.Text = "Validando Chave de Acesso dos Correios...";

            string usuario = TxtUsuario.Text.Trim();
            string codigo = !string.IsNullOrEmpty(TxtCodigoAcesso.Password) ? TxtCodigoAcesso.Password.Trim() : DatabaseContext.ObterConfig("correios_codigo_acesso", "");

            var (sucesso, token, msg) = await CorreiosApiService.TestarAutenticacaoAsync(usuario, codigo);

            if (sucesso)
            {
                TxtStatusTeste.Foreground = System.Windows.Media.Brushes.LightGreen;
                TxtStatusTeste.Text = "✓ Chave validada com sucesso! Conexão ativa com os Correios.";
            }
            else
            {
                TxtStatusTeste.Foreground = System.Windows.Media.Brushes.Salmon;
                TxtStatusTeste.Text = $"✕ Falha de conexão: {msg}";
            }

            BtnTestarConexao.IsEnabled = true;
        }

        private async void BtnTestarEmail_Click(object sender, RoutedEventArgs e)
        {
            BtnTestarEmail.IsEnabled = false;
            TxtStatusEmail.Text = "Testando conexão e buscando e-mails na caixa postal...";

            // Salva dados temporários para testar
            DatabaseContext.SalvarConfig("email_host", TxtEmailHost.Text.Trim());
            DatabaseContext.SalvarConfig("email_porta", TxtEmailPorta.Text.Trim());
            DatabaseContext.SalvarConfig("email_usuario", TxtEmailUsuario.Text.Trim());
            if (!string.IsNullOrEmpty(TxtEmailSenha.Password))
            {
                DatabaseContext.SalvarConfig("email_senha", TxtEmailSenha.Password.Replace(" ", "").Trim());
            }
            DatabaseContext.SalvarConfig("email_remetente_filtro", TxtEmailRemetenteFiltro.Text.Trim());

            var (atualizados, novos, msg) = await CorreiosColetaReader.VerificarEmailsColetaAsync();
            TxtStatusEmail.Text = msg;
            BtnTestarEmail.IsEnabled = true;
        }

        private void BtnCarregarHtmlColeta_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Comprovante Coleta Correios (*.html;*.htm)|*.html;*.htm|Todos os Arquivos (*.*)|*.*",
                Title = "Selecione o arquivo HTML recebido por e-mail da Coleta dos Correios"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var (atualizados, novos, itens) = CorreiosColetaReader.ProcessarArquivoHtml(ofd.FileName);
                    if (atualizados > 0 || novos > 0)
                    {
                        MessageBox.Show($"Sucesso!\n\n• {atualizados} pedido(s) existentes atualizados com o Custo Real e Rastreio.\n• {novos} novo(s) envio(s) cadastrado(s) no sistema.", "Coleta Processada", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (itens.Count > 0)
                    {
                        MessageBox.Show($"Foram encontrados {itens.Count} registros no HTML, porém todos já estavam sincronizados no banco.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Nenhum código de rastreio ou valor foi identificado no arquivo HTML selecionado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao processar HTML: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnImportarExcel_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Planilhas Excel (*.xlsx)|*.xlsx|Todos os Arquivos (*.*)|*.*",
                Title = "Selecione a Planilha de Envios para Importação"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var (importados, atualizados) = ExcelService.ImportarEnvios(ofd.FileName);
                    TxtStatusExcel.Text = $"✓ Importação concluída! Novos: {importados} | Atualizados: {atualizados}.";
                    MessageBox.Show($"Planilha processada com sucesso!\n\n• Novos envios cadastrados: {importados}\n• Envios existentes atualizados: {atualizados}", "Importação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao importar planilha: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Planilha Excel (*.xlsx)|*.xlsx",
                FileName = $"Controle_Envios_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                Title = "Exportar Envios para Excel"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var envios = EnvioService.ListarEnvios();
                    ExcelService.ExportarEnvios(sfd.FileName, envios);
                    TxtStatusExcel.Text = $"✓ Planilha exportada para: {sfd.FileName}";
                    MessageBox.Show("Planilha Excel exportada com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao exportar planilha: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
