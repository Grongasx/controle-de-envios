using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using ControleEnvios.Models;

namespace ControleEnvios.Services
{
    public static class ExcelService
    {
        public static (int importados, int atualizados) ImportarEnvios(string caminhoArquivo)
        {
            if (!File.Exists(caminhoArquivo))
                throw new FileNotFoundException($"Arquivo não encontrado: {caminhoArquivo}");

            int importados = 0;
            int atualizados = 0;

            using var workbook = new XLWorkbook(caminhoArquivo);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) return (0, 0);

            var rangeUsed = worksheet.RangeUsed();
            if (rangeUsed == null) return (0, 0);
            var rows = rangeUsed.RowsUsed().ToList();
            if (rows.Count < 2) return (0, 0);

            // 1. Mapeamento de Cabeçalhos
            var headerRow = rows[0];
            var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var cell in headerRow.Cells())
            {
                string headerName = cell.GetString().Trim().ToLowerInvariant();
                // Normaliza variações
                if (headerName.Contains("pedido")) colIndex["pedido"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("cliente") || headerName.Contains("destinatário")) colIndex["cliente"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("produto")) colIndex["produto"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("quantid") || headerName.Contains("qtd")) colIndex["qtd"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("origem")) colIndex["origem"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("nota") || headerName.Contains("fiscal") || headerName == "nf") colIndex["nf"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("rastreio") || headerName.Contains("código")) colIndex["rastreio"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("data")) colIndex["data_envio"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("status")) colIndex["status"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("correio") || headerName.Contains("custo") || headerName.Contains("valor")) colIndex["valor_correios"] = cell.Address.ColumnNumber;
                else if (headerName.Contains("obs")) colIndex["observacoes"] = cell.Address.ColumnNumber;
            }

            // 2. Leitura das linhas de dados
            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                string pedido = ObterTextoCelula(r, colIndex, "pedido");
                string cliente = ObterTextoCelula(r, colIndex, "cliente");
                string produto = ObterTextoCelula(r, colIndex, "produto");
                string qtdStr = ObterTextoCelula(r, colIndex, "qtd");
                string origemStr = ObterTextoCelula(r, colIndex, "origem");
                string nf = ObterTextoCelula(r, colIndex, "nf");
                string rastreio = ObterTextoCelula(r, colIndex, "rastreio");
                string dataEnvio = ObterTextoCelula(r, colIndex, "data_envio");
                string status = ObterTextoCelula(r, colIndex, "status");
                string valCorreiosStr = ObterTextoCelula(r, colIndex, "valor_correios");
                string obs = ObterTextoCelula(r, colIndex, "observacoes");

                if (string.IsNullOrWhiteSpace(pedido) && string.IsNullOrWhiteSpace(cliente) && string.IsNullOrWhiteSpace(rastreio))
                    continue;

                // Tenta buscar envio existente por pedido ou rastreio
                Envio? envioExistente = null;
                if (!string.IsNullOrWhiteSpace(pedido))
                {
                    var busca = EnvioService.ListarEnvios(busca: pedido);
                    envioExistente = busca.FirstOrDefault(e => e.NumeroPedido.Equals(pedido, StringComparison.OrdinalIgnoreCase));
                }
                if (envioExistente == null && !string.IsNullOrWhiteSpace(rastreio))
                {
                    var busca = EnvioService.ListarEnvios(busca: rastreio);
                    envioExistente = busca.FirstOrDefault(e => e.CodigoRastreio.Equals(rastreio, StringComparison.OrdinalIgnoreCase));
                }

                bool ehNovo = envioExistente == null;
                var envio = envioExistente ?? new Envio();

                if (!string.IsNullOrWhiteSpace(pedido)) envio.NumeroPedido = pedido;
                if (!string.IsNullOrWhiteSpace(cliente)) envio.Cliente = cliente;
                if (!string.IsNullOrWhiteSpace(nf)) envio.NotaFiscal = nf;
                if (!string.IsNullOrWhiteSpace(rastreio)) envio.CodigoRastreio = rastreio.ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(dataEnvio)) envio.DataEnvio = dataEnvio;
                if (!string.IsNullOrWhiteSpace(obs)) envio.Observacoes = obs;

                // Origem
                if (!string.IsNullOrWhiteSpace(origemStr))
                {
                    if (origemStr.Contains("Drop", StringComparison.OrdinalIgnoreCase))
                    {
                        envio.OrigemTipo = "DROPSHIPPING";
                        envio.FornecedorNome = origemStr;
                    }
                    else
                    {
                        envio.OrigemTipo = "PROPRIO";
                    }
                }

                // Valor Correios
                if (!string.IsNullOrWhiteSpace(valCorreiosStr))
                {
                    string limpo = valCorreiosStr.Replace("R$", "").Replace(" ", "").Trim();
                    if (decimal.TryParse(limpo.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal vCor))
                    {
                        envio.ValorCorreios = vCor;
                    }
                }

                // Status Correios
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status.Equals("Enviado", StringComparison.OrdinalIgnoreCase) && envio.StatusCorreios == "Pendente")
                    {
                        envio.StatusCorreios = "Em Trânsito";
                    }
                    else if (status.Equals("Entregue", StringComparison.OrdinalIgnoreCase))
                    {
                        envio.StatusCorreios = "Entregue";
                        envio.Entregue = true;
                    }
                }

                // Produto
                if (!string.IsNullOrWhiteSpace(produto))
                {
                    int qtd = 1;
                    if (!string.IsNullOrWhiteSpace(qtdStr) && int.TryParse(qtdStr, out int q) && q > 0)
                    {
                        qtd = q;
                    }

                    // Se não tiver itens, adiciona
                    if (envio.Itens.Count == 0)
                    {
                        envio.Itens.Add(new ItemEnvio
                        {
                            NomeProduto = produto,
                            Quantidade = qtd,
                            ValorTotal = envio.ValorPedido
                        });
                    }
                }

                EnvioService.SalvarEnvio(envio);

                if (ehNovo) importados++;
                else atualizados++;
            }

            return (importados, atualizados);
        }

        private static string ObterTextoCelula(IXLRangeRow row, Dictionary<string, int> colIndex, string chave)
        {
            if (colIndex.TryGetValue(chave, out int col) && col > 0)
            {
                var cell = row.Cell(col);
                if (cell != null && !cell.IsEmpty())
                {
                    return cell.GetString().Trim();
                }
            }
            return string.Empty;
        }

        public static void ExportarEnvios(string caminhoArquivo, List<Envio> envios)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Controle de Envios");

            // Cabeçalho idêntico e aprimorado com base na planilha modelo do usuário
            string[] colunas = {
                "Nº do Pedido",
                "Cliente",
                "Produto",
                "Quantidade",
                "Origem do Produto",
                "Nota Fiscal",
                "Código de Rastreio",
                "Data de envio",
                "Status do envio",
                "Frete da Loja (R$)",
                "Valor Correios (R$)",
                "Diferença de Valor (R$)",
                "Observações"
            };

            for (int i = 0; i < colunas.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = colunas[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#334155");
            }

            int linha = 2;
            foreach (var e in envios)
            {
                worksheet.Cell(linha, 1).Value = e.NumeroPedido;
                worksheet.Cell(linha, 2).Value = e.Cliente;
                worksheet.Cell(linha, 3).Value = !string.IsNullOrWhiteSpace(e.ResumoProdutos) ? e.ResumoProdutos : (e.Itens.FirstOrDefault()?.NomeProduto ?? "");
                
                int totalQtd = e.Itens.Sum(it => it.Quantidade);
                worksheet.Cell(linha, 4).Value = totalQtd > 0 ? totalQtd : 1;
                worksheet.Cell(linha, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Origem (Drop BR ou Loja)
                worksheet.Cell(linha, 5).Value = e.OrigemFormatada;
                if (e.OrigemTipo == "DROPSHIPPING")
                {
                    worksheet.Cell(linha, 5).Style.Font.FontColor = XLColor.FromHtml("#dc2626"); // Vermelho como no exemplo
                    worksheet.Cell(linha, 5).Style.Font.Bold = true;
                }
                else
                {
                    worksheet.Cell(linha, 5).Style.Font.FontColor = XLColor.FromHtml("#059669"); // Verde como no exemplo
                    worksheet.Cell(linha, 5).Style.Font.Bold = true;
                }

                worksheet.Cell(linha, 6).Value = e.NotaFiscal;
                worksheet.Cell(linha, 7).Value = e.CodigoRastreio;
                worksheet.Cell(linha, 8).Value = e.DataEnvio;
                
                // Status com badge visual
                worksheet.Cell(linha, 9).Value = e.StatusCorreios;
                if (e.Entregue)
                {
                    worksheet.Cell(linha, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#d1fae5");
                    worksheet.Cell(linha, 9).Style.Font.FontColor = XLColor.FromHtml("#065f46");
                }
                else
                {
                    worksheet.Cell(linha, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#dbeafe");
                    worksheet.Cell(linha, 9).Style.Font.FontColor = XLColor.FromHtml("#1e40af");
                }

                // Frete Loja
                worksheet.Cell(linha, 10).Value = (double)e.ValorFreteLoja;
                worksheet.Cell(linha, 10).Style.NumberFormat.Format = "R$ #,##0.00";

                // Valor Correios
                worksheet.Cell(linha, 11).Value = (double)e.ValorCorreios;
                worksheet.Cell(linha, 11).Style.NumberFormat.Format = "R$ #,##0.00";

                // Diferença do Frete
                if (e.IsFreteGratisOuRetirada)
                {
                    worksheet.Cell(linha, 12).Value = "Isento";
                    worksheet.Cell(linha, 12).Style.Font.FontColor = XLColor.FromHtml("#64748b");
                }
                else
                {
                    decimal dif = e.DiferencaFrete;
                    worksheet.Cell(linha, 12).Value = (double)dif;
                    worksheet.Cell(linha, 12).Style.NumberFormat.Format = "R$ #,##0.00";
                    if (e.ValorCorreios > 0)
                    {
                        if (dif > 0) worksheet.Cell(linha, 12).Style.Font.FontColor = XLColor.FromHtml("#059669");
                        else if (dif < 0) worksheet.Cell(linha, 12).Style.Font.FontColor = XLColor.FromHtml("#dc2626");
                    }
                }

                worksheet.Cell(linha, 13).Value = e.Observacoes;

                linha++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(caminhoArquivo);
        }
    }
}
