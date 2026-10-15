using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ControleEnvios.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ControleEnvios.Services
{
    public static class TrayPdfExtractor
    {
        public static Envio ExtrairDados(string caminhoPdf)
        {
            if (!File.Exists(caminhoPdf))
                throw new FileNotFoundException($"Arquivo PDF não encontrado: {caminhoPdf}");

            var envio = new Envio
            {
                CaminhoPdf = caminhoPdf
            };

            using var document = PdfDocument.Open(caminhoPdf);
            if (document.NumberOfPages == 0) return envio;

            var p1 = document.GetPage(1);
            var p2 = document.NumberOfPages >= 2 ? document.GetPage(2) : null;

            var allWordsP1 = p1.GetWords().ToList();
            var allWordsP2 = p2 != null ? p2.GetWords().ToList() : new List<Word>();

            string p1Raw = string.Join(" ", allWordsP1.Select(w => w.Text));
            string fullRaw = p1Raw + (p2 != null ? " " + string.Join(" ", allWordsP2.Select(w => w.Text)) : "");

            // 1. Número do Pedido
            var mPedido = Regex.Match(fullRaw, @"Pedido\s*#?(\d+)", RegexOptions.IgnoreCase);
            if (mPedido.Success)
            {
                envio.NumeroPedido = mPedido.Groups[1].Value.Trim();
            }
            else
            {
                var mUrl = Regex.Match(fullRaw, @"orders_id=(\d+)", RegexOptions.IgnoreCase);
                if (mUrl.Success)
                    envio.NumeroPedido = mUrl.Groups[1].Value.Trim();
            }

            // 2. Extração via Colunas da Página 1 (Base consistente presente em todos os PDFs)
            ExtrairDadosColunasPagina1(allWordsP1, envio);

            // 3. Extração via Página 2 (Etiqueta Destinatário da Tray - se disponível, sobrescreve endereço com etiqueta oficial)
            if (p2 != null)
            {
                ExtrairDadosDestinatarioPagina2(allWordsP2, envio);
            }

            // 4. Fallback de Celular / E-mail / CPF no texto completo caso ainda estejam vazios
            if (string.IsNullOrWhiteSpace(envio.ClienteCelular))
            {
                var mCelFall = Regex.Match(fullRaw, @"(?:Celular|Telefone):\s*([\(\)\d\s\-]{8,25})", RegexOptions.IgnoreCase);
                if (mCelFall.Success)
                    envio.ClienteCelular = LimparTelefone(mCelFall.Groups[1].Value);
            }

            if (string.IsNullOrWhiteSpace(envio.ClienteEmail))
            {
                var mEmailFall = Regex.Match(fullRaw, @"([a-zA-Z0-9_\.\-]+)\s*@\s*([a-zA-Z0-9_\.\-]+\.(?:com\.br|com|net|org|edu|gov|adv\.br|[a-z]{2,4}\.[a-z]{2}|[a-z]{2,4}))\b", RegexOptions.IgnoreCase);
                if (mEmailFall.Success)
                {
                    string userCand = mEmailFall.Groups[1].Value.Replace(" ", "").Trim();
                    envio.ClienteEmail = $"{userCand}@{mEmailFall.Groups[2].Value.Replace(" ", "")}".ToLowerInvariant().Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(envio.ClienteCpf))
            {
                string? docFall = ExtrairCpfOuCnpjDeTexto(fullRaw);
                if (!string.IsNullOrWhiteSpace(docFall))
                {
                    envio.ClienteCpf = docFall;
                }
            }

            // 5. Forma de Envio na Loja (SEDEX, PAC, FRETE GRÁTIS ou RETIRADA)
            if (string.IsNullOrWhiteSpace(envio.FormaEnvioLoja))
            {
                string formaCrua = "";
                var mForma = Regex.Match(p1Raw, @"Forma de envio\s*([^\r\n]+)", RegexOptions.IgnoreCase);
                if (mForma.Success)
                {
                    formaCrua = mForma.Groups[1].Value.Trim();
                }

                string formaNorm = RemoverAcentos(formaCrua).ToUpperInvariant();
                string fullNorm = RemoverAcentos(fullRaw).ToUpperInvariant();

                if (formaNorm.Contains("FRETE GRATIS") || fullNorm.Contains("FRETE GRATIS"))
                {
                    envio.FormaEnvioLoja = "FRETE GRÁTIS";
                }
                else if (formaNorm.Contains("SEDEX") || fullNorm.Contains("SEDEX"))
                {
                    envio.FormaEnvioLoja = "SEDEX";
                }
                else if (formaNorm.Contains("PAC") || fullNorm.Contains("PAC"))
                {
                    envio.FormaEnvioLoja = "PAC";
                }
                else if (formaNorm.Contains("RETIRAD") || fullNorm.Contains("RETIRAD"))
                {
                    envio.FormaEnvioLoja = "RETIRADA";
                }
                else if (!string.IsNullOrWhiteSpace(formaCrua) && !formaCrua.Equals("Pix", StringComparison.OrdinalIgnoreCase))
                {
                    envio.FormaEnvioLoja = formaCrua.Split('-')[0].Trim();
                }
            }

            // 6. Peso do Pedido (gramas)
            var mPeso = Regex.Match(p1Raw, @"Peso do pedido:\s*([\d\.,]+)\s*gramas", RegexOptions.IgnoreCase);
            if (mPeso.Success && double.TryParse(mPeso.Groups[1].Value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double peso))
            {
                envio.PesoGramas = peso;
            }

            // 7. Valores (Frete Loja e Total do Pedido)
            var mFrete = Regex.Match(p1Raw, @"Valor do frete\s*R\$\s*([\d\.,]+)", RegexOptions.IgnoreCase);
            if (mFrete.Success && decimal.TryParse(mFrete.Groups[1].Value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal frete))
            {
                envio.ValorFreteLoja = frete;
            }

            var mTotal = Regex.Match(p1Raw, @"Total do pedido\s*R\$\s*([\d\.,]+)", RegexOptions.IgnoreCase);
            if (mTotal.Success && decimal.TryParse(mTotal.Groups[1].Value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal total))
            {
                envio.ValorPedido = total;
            }

            // 8. Itens do Pedido
            envio.Itens = ExtrairItensTabela(allWordsP1);

            return envio;
        }

        /// <summary>
        /// Extrai cliente, contatos e endereço a partir das 4 colunas padronizadas da Página 1 da Tray.
        /// </summary>
        private static void ExtrairDadosColunasPagina1(List<Word> wordsP1, Envio envio)
        {
            // Coluna 1: Dados do cliente (Left entre 40 e 175, Y entre 640 e 785)
            var col1Words = wordsP1.Where(w => w.BoundingBox.Left >= 40 && w.BoundingBox.Left < 175 &&
                                               w.BoundingBox.Bottom >= 640 && w.BoundingBox.Bottom <= 785);
            var col1Lines = AgruparPalavrasEmLinhas(col1Words, yTolerance: 3.5);

            var linhasClienteNome = new List<string>();
            var partesTelefone = new List<string>();
            var partesEmail = new List<string>();
            int etapa = 0; // 0 = lendo nome, 1 = lendo telefone, 2 = lendo email

            foreach (var rawLine in col1Lines)
            {
                string linha = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(linha)) continue;

                string norm = RemoverAcentos(linha).ToUpperInvariant();
                if (norm.StartsWith("DADOS") || norm.Contains("CLIENTE"))
                    continue;

                // Se atingir o cabeçalho de produtos, interrompe a leitura da Coluna 1
                if (norm.StartsWith("PRODUTO") || norm.Contains("DISPONIBILIDADE") || norm.StartsWith("CODIGO:") || norm.StartsWith("MARCA:"))
                    break;

                if (etapa == 0)
                {
                    // Checa se iniciou o telefone
                    if (norm.Contains("CELULAR") || norm.Contains("TELEFONE") || Regex.IsMatch(linha, @"\(\d{2}\)"))
                    {
                        etapa = 1;
                        partesTelefone.Add(linha);
                    }
                    else
                    {
                        linhasClienteNome.Add(linha);
                    }
                }
                else if (etapa == 1)
                {
                    // Estamos na fase do telefone. Verifica se já iniciou o email
                    bool ehEmail = linha.Contains("@") || norm.EndsWith(".COM") || norm.EndsWith(".BR") ||
                                   norm.Contains(".COM") || norm.Contains(".BR") ||
                                   (!Regex.IsMatch(linha, @"^\s*[\d\(\)\s\-]+\s*$") && (partesTelefone.Count > 0 && Digitos(string.Join("", partesTelefone)).Length >= 10));

                    if (ehEmail)
                    {
                        etapa = 2;
                        partesEmail.Add(linha);
                    }
                    else if (Regex.IsMatch(linha, @"^\s*[\d\s\-]+\s*$") || (Digitos(string.Join("", partesTelefone)).Length < 10 && !linha.Contains("@")))
                    {
                        // Continuação do telefone quebrado em nova linha (ex: "2686")
                        partesTelefone.Add(linha);
                    }
                    else
                    {
                        etapa = 2;
                        partesEmail.Add(linha);
                    }
                }
                else
                {
                    // Etapa 2: continuação do email (ex: "l.com" ou "com.br")
                    partesEmail.Add(linha);
                }
            }

            if (linhasClienteNome.Count > 0 && string.IsNullOrWhiteSpace(envio.Cliente))
            {
                envio.Cliente = string.Join(" ", linhasClienteNome).Trim();
            }

            if (partesTelefone.Count > 0 && string.IsNullOrWhiteSpace(envio.ClienteCelular))
            {
                string telCru = string.Join(" ", partesTelefone);
                envio.ClienteCelular = LimparTelefone(telCru);
            }

            if (partesEmail.Count > 0 && string.IsNullOrWhiteSpace(envio.ClienteEmail))
            {
                string emailCru = string.Join("", partesEmail.Select(p => p.Trim()));
                var mEm = Regex.Match(emailCru, @"[a-zA-Z0-9_\.\-]+@[a-zA-Z0-9_\.\-]+\.(?:com\.br|com|net|org|edu|gov|adv\.br|[a-z]{2,4}\.[a-z]{2}|[a-z]{2,4})\b", RegexOptions.IgnoreCase);
                if (mEm.Success)
                {
                    envio.ClienteEmail = mEm.Value.ToLowerInvariant();
                }
                else
                {
                    var mEm2 = Regex.Match(emailCru, @"[a-zA-Z0-9_\.\-]+@[a-zA-Z0-9_\.\-]+", RegexOptions.IgnoreCase);
                    if (mEm2.Success)
                        envio.ClienteEmail = mEm2.Value.ToLowerInvariant();
                    else
                        envio.ClienteEmail = emailCru.Trim().ToLowerInvariant();
                }
            }

            // Coluna 2: Informações adicionais / CPF ou CNPJ (Left entre 175 e 290, Y entre 650 e 785)
            var col2Words = wordsP1.Where(w => w.BoundingBox.Left >= 175 && w.BoundingBox.Left < 290 &&
                                               w.BoundingBox.Bottom >= 650 && w.BoundingBox.Bottom <= 785);
            var col2Lines = AgruparPalavrasEmLinhas(col2Words, yTolerance: 3.5);
            foreach (var l in col2Lines)
            {
                string? doc = ExtrairCpfOuCnpjDeTexto(l);
                if (!string.IsNullOrWhiteSpace(doc) && string.IsNullOrWhiteSpace(envio.ClienteCpf))
                {
                    envio.ClienteCpf = doc;
                    break;
                }
            }

            // Coluna 3: Endereço de entrega (Left entre 290 e 425, Y entre 650 e 785)
            var col3Words = wordsP1.Where(w => w.BoundingBox.Left >= 290 && w.BoundingBox.Left < 425 &&
                                               w.BoundingBox.Bottom >= 650 && w.BoundingBox.Bottom <= 785);
            var col3Lines = AgruparPalavrasEmLinhas(col3Words, yTolerance: 3.5);

            if (col3Lines.Count > 0)
            {
                // Remove cabeçalho
                col3Lines = col3Lines.Where(l =>
                {
                    string norm = RemoverAcentos(l).Trim().ToUpperInvariant();
                    return !norm.StartsWith("ENDERECO") && !norm.StartsWith("ENTREGA") && l.Trim().Length > 0;
                }).ToList();

                // Remove linhas de tabelas/disponibilidade que possam ter vazado na margem inferior
                col3Lines = col3Lines.Where(l => !l.Contains("Disponibilidade", StringComparison.OrdinalIgnoreCase) &&
                                                 !l.Contains("Produto", StringComparison.OrdinalIgnoreCase) &&
                                                 !l.Contains("Subtotal", StringComparison.OrdinalIgnoreCase)).ToList();

                // Extrai nome alternativo caso venha "Aos cuidados: Nome"
                for (int i = 0; i < col3Lines.Count; i++)
                {
                    var mAos = Regex.Match(col3Lines[i], @"Aos cuidados:\s*(.+)", RegexOptions.IgnoreCase);
                    if (mAos.Success)
                    {
                        if (string.IsNullOrWhiteSpace(envio.Cliente))
                            envio.Cliente = mAos.Groups[1].Value.Trim();
                        col3Lines.RemoveAt(i);

                        // Se a próxima linha também for continuação do nome do cliente (não é endereço)
                        if (i < col3Lines.Count && !EhLinhaInicioEndereco(col3Lines[i]))
                        {
                            if (!string.IsNullOrWhiteSpace(envio.Cliente) && !envio.Cliente.Contains(col3Lines[i].Trim()))
                                envio.Cliente += " " + col3Lines[i].Trim();
                            col3Lines.RemoveAt(i);
                        }
                        break;
                    }
                }

                // Se o nome do cliente já foi extraído da Coluna 1, remove linhas do topo da Coluna 3 que sejam apenas pedaços do nome
                while (col3Lines.Count > 0 && !EhLinhaInicioEndereco(col3Lines[0]))
                {
                    string candNome = col3Lines[0].Trim();
                    if (!string.IsNullOrWhiteSpace(envio.Cliente) && envio.Cliente.Contains(candNome, StringComparison.OrdinalIgnoreCase))
                    {
                        col3Lines.RemoveAt(0);
                    }
                    else
                    {
                        break;
                    }
                }

                // Extrai CEP (ex: "86813-250")
                for (int i = col3Lines.Count - 1; i >= 0; i--)
                {
                    var mCep = Regex.Match(col3Lines[i], @"\b(\d{5}-\d{3})\b");
                    if (mCep.Success)
                    {
                        if (string.IsNullOrWhiteSpace(envio.CepDestino))
                            envio.CepDestino = mCep.Groups[1].Value;
                        col3Lines.RemoveAt(i);
                        break;
                    }
                }

                // Extrai Cidade e UF (ex: "Apucarana - PR - Brasil -")
                for (int i = col3Lines.Count - 1; i >= 0; i--)
                {
                    var mCidUf = Regex.Match(col3Lines[i], @"([A-Za-zÀ-ÖØ-öø-ÿ\s]+?)\s*-\s*([A-Z]{2})\s*-\s*Brasil", RegexOptions.IgnoreCase);
                    if (mCidUf.Success)
                    {
                        if (string.IsNullOrWhiteSpace(envio.CidadeDestino))
                            envio.CidadeDestino = mCidUf.Groups[1].Value.Trim();
                        if (string.IsNullOrWhiteSpace(envio.UfDestino))
                            envio.UfDestino = mCidUf.Groups[2].Value.Trim();
                        col3Lines.RemoveAt(i);
                        break;
                    }
                    else
                    {
                        var mCidUf2 = Regex.Match(col3Lines[i], @"([A-Za-zÀ-ÖØ-öø-ÿ\s]+?)\s*-\s*([A-Z]{2})", RegexOptions.IgnoreCase);
                        if (mCidUf2.Success && !col3Lines[i].Contains("Rua") && !col3Lines[i].Contains("Avenida"))
                        {
                            if (string.IsNullOrWhiteSpace(envio.CidadeDestino))
                                envio.CidadeDestino = mCidUf2.Groups[1].Value.Trim();
                            if (string.IsNullOrWhiteSpace(envio.UfDestino))
                                envio.UfDestino = mCidUf2.Groups[2].Value.Trim();
                            col3Lines.RemoveAt(i);
                            break;
                        }
                    }
                }

                // As linhas restantes compõem o Endereço e Complemento
                if (col3Lines.Count > 0 && string.IsNullOrWhiteSpace(envio.EnderecoEntrega))
                {
                    ProcessarLinhasEndereco(col3Lines, envio);
                }
            }
        }

        /// <summary>
        /// Extrai e refina os dados a partir da etiqueta de destinatário na Página 2 (Tray).
        /// </summary>
        private static void ExtrairDadosDestinatarioPagina2(List<Word> wordsP2, Envio envio)
        {
            // Na Página 2 da Tray:
            // Letras verticais da margem ficam com Left < 60
            // Bloco DESTINATÁRIO fica entre Left >= 60 e Left <= 290, Y >= 500
            var p2DestWords = wordsP2.Where(w => w.BoundingBox.Left >= 60 && w.BoundingBox.Left <= 290 &&
                                                 w.BoundingBox.Bottom >= 500 && w.BoundingBox.Bottom <= 800);
            var lines = AgruparPalavrasEmLinhas(p2DestWords, yTolerance: 3.5);

            if (lines.Count == 0) return;

            // Helper para identificar cabeçalhos e métodos de envio na etiqueta
            bool EhCabecalhoOuFormaEnvio(string texto)
            {
                if (string.IsNullOrWhiteSpace(texto)) return true;
                string norm = RemoverAcentos(texto).Trim().ToUpperInvariant();
                return norm.StartsWith("DESTINA") ||
                       norm.Contains("REMETENTE") ||
                       norm.Contains("SEDEX") ||
                       norm.Contains("PAC") ||
                       norm.Contains("FRETE") ||
                       norm.Contains("CORREIOS") ||
                       norm.Contains("ENCOMENDA") ||
                       norm.Contains("RETIRADA") ||
                       norm.Contains("RETIRAR");
            }

            // Identifica forma de envio e atualiza caso ainda não esteja definida
            foreach (var l in lines)
            {
                string norm = RemoverAcentos(l).Trim().ToUpperInvariant();
                if (norm.Contains("FRETE GRATIS"))
                {
                    envio.FormaEnvioLoja = "FRETE GRÁTIS";
                }
                else if (norm.Contains("SEDEX") && (string.IsNullOrWhiteSpace(envio.FormaEnvioLoja) || envio.FormaEnvioLoja == "PAC"))
                {
                    envio.FormaEnvioLoja = "SEDEX";
                }
                else if (norm.Contains("PAC") && string.IsNullOrWhiteSpace(envio.FormaEnvioLoja))
                {
                    envio.FormaEnvioLoja = "PAC";
                }
                else if ((norm.Contains("RETIRADA") || norm.Contains("RETIRAR")) && string.IsNullOrWhiteSpace(envio.FormaEnvioLoja))
                {
                    envio.FormaEnvioLoja = "RETIRADA";
                }
            }

            // Remove todos os cabeçalhos e termos de envio do topo
            lines = lines.Where(l => !EhCabecalhoOuFormaEnvio(l)).ToList();
            if (lines.Count == 0) return;

            // 1. Extração de Cidade, UF e CEP (últimas linhas da etiqueta)
            // Pode vir em 1 linha: "São José do Rio Preto - SP - Brasil, CEP: 15063-020"
            // Ou quebrado em 2 linhas:
            // Penúltima: "São José do Rio Preto - SP - Brasil"
            // Última: "CEP: 15063-020"
            for (int i = lines.Count - 1; i >= Math.Max(0, lines.Count - 2); i--)
            {
                string linha = lines[i];

                var mCidUfCep = Regex.Match(linha, @"([A-Za-zÀ-ÖØ-öø-ÿ\s]+?)\s*-\s*([A-Z]{2})\s*-\s*Brasil,?\s*CEP:\s*([\d\-]+)", RegexOptions.IgnoreCase);
                if (mCidUfCep.Success)
                {
                    envio.CidadeDestino = mCidUfCep.Groups[1].Value.Trim().Trim(',', ' ');
                    envio.UfDestino = mCidUfCep.Groups[2].Value.Trim();
                    envio.CepDestino = mCidUfCep.Groups[3].Value.Trim();
                    lines.RemoveAt(i);
                    break;
                }

                var mCep = Regex.Match(linha, @"CEP:\s*([\d\-]+)", RegexOptions.IgnoreCase);
                if (mCep.Success)
                {
                    envio.CepDestino = mCep.Groups[1].Value.Trim();
                    lines.RemoveAt(i);
                    // Checa linha imediatamente anterior para Cidade e UF
                    if (i - 1 >= 0)
                    {
                        var mCidUfAnt = Regex.Match(lines[i - 1], @"([A-Za-zÀ-ÖØ-öø-ÿ\s]+?)\s*-\s*([A-Z]{2})", RegexOptions.IgnoreCase);
                        if (mCidUfAnt.Success)
                        {
                            envio.CidadeDestino = mCidUfAnt.Groups[1].Value.Trim().Trim(',', ' ');
                            envio.UfDestino = mCidUfAnt.Groups[2].Value.Trim();
                            lines.RemoveAt(i - 1);
                        }
                    }
                    break;
                }
            }

            if (lines.Count == 0) return;

            // 2. Extrai e remove linhas explícitas de CPF, CNPJ e Telefone
            // (evita que caiam dentro do Endereço de entrega!)
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                string? doc = ExtrairCpfOuCnpjDeTexto(lines[i]);
                if (!string.IsNullOrWhiteSpace(doc))
                {
                    if (string.IsNullOrWhiteSpace(envio.ClienteCpf))
                        envio.ClienteCpf = doc;
                    lines.RemoveAt(i);
                    continue;
                }

                var mTel = Regex.Match(lines[i], @"(?:Telefone|Celular)?:\s*([\(\)\d\s\-]+)", RegexOptions.IgnoreCase);
                if (mTel.Success && (lines[i].Contains("Telefone") || lines[i].Contains("Celular")))
                {
                    if (string.IsNullOrWhiteSpace(envio.ClienteCelular))
                        envio.ClienteCelular = LimparTelefone(mTel.Groups[1].Value);
                    lines.RemoveAt(i);
                    continue;
                }
            }

            if (lines.Count == 0) return;

            // 3. Nome do Cliente: primeira(s) linha(s) antes do logradouro
            // Detecta se o nome quebrou em 2 linhas (ex: "FRANCISCO DE ASSIS" / "NASCIMENTO JUNIOR")
            var linhasNome = new List<string>();
            while (lines.Count > 0)
            {
                string prox = lines[0].Trim();
                if (EhLinhaInicioEndereco(prox))
                    break;

                linhasNome.Add(prox);
                lines.RemoveAt(0);

                // No máximo 2 linhas para nome
                if (linhasNome.Count >= 2) break;
            }

            if (linhasNome.Count > 0)
            {
                string nomeCompleto = string.Join(" ", linhasNome).Trim();
                if (!EhCabecalhoOuFormaEnvio(nomeCompleto))
                {
                    envio.Cliente = nomeCompleto;
                }
            }

            // 4. As linhas restantes são 100% Endereço e Complemento
            if (lines.Count > 0)
            {
                ProcessarLinhasEndereco(lines, envio);
            }
        }

        /// <summary>
        /// Processa lista de linhas de endereço separando logradouro, número e complementos/bairro.
        /// </summary>
        private static void ProcessarLinhasEndereco(List<string> linhasEnd, Envio envio)
        {
            if (linhasEnd.Count == 0) return;

            // Se a primeira linha não contém número (ex: "Rua Carlos Ranzani" e na linha seguinte "94, Apto..."),
            // junta a linha seguinte para formar o logradouro completo.
            string textoEndCru = string.Join(", ", linhasEnd.Select(x => x.Trim().TrimEnd(',')));

            // Tenta encontrar o padrão "Logradouro, Nº"
            // Ex: "Rua Antonio Tarichi, 270, quadra 04, lote 12" -> Endereço: "Rua Antonio Tarichi, 270" | Compl: "quadra 04, lote 12"
            // Ex: "Avenida Jerônimo Vervloet, 631, Gráfica Ebenezer" -> Endereço: "Avenida Jerônimo Vervloet, 631" | Compl: "Gráfica Ebenezer"
            var mEnd = Regex.Match(textoEndCru, @"^(.+?,\s*(?:s/n|sn|\d+[A-Za-z]?))\s*(?:,\s*(.+))?$", RegexOptions.IgnoreCase);
            if (mEnd.Success)
            {
                envio.EnderecoEntrega = mEnd.Groups[1].Value.Trim();
                string compl = mEnd.Groups[2].Success ? mEnd.Groups[2].Value.Trim() : "";
                if (!string.IsNullOrWhiteSpace(compl))
                {
                    envio.ComplementoEntrega = compl;
                }
            }
            else
            {
                // Fallback: primeira linha vira endereço, demais viram complemento
                envio.EnderecoEntrega = linhasEnd[0].Trim().TrimEnd(',');
                if (linhasEnd.Count > 1)
                {
                    envio.ComplementoEntrega = string.Join(" - ", linhasEnd.Skip(1).Select(x => x.Trim().TrimEnd(',')));
                }
            }
        }

        private static bool EhLinhaInicioEndereco(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            string norm = RemoverAcentos(texto).Trim().ToUpperInvariant();

            string[] prefixos = { "RUA", "AVENIDA", "AV.", "AV ", "TRAVESSA", "TRAV ", "ALAMEDA", "AL ", "RODOVIA", "ROD ", "ROD.", "PRACA", "ESTRADA", "SERVIDAO", "PASSAGEM", "QUADRA", "QD", "LOTE", "CONDOMINIO", "CHACARA", "SITIO", "FAZENDA", "BECO", "SETOR" };
            foreach (var p in prefixos)
            {
                if (norm.StartsWith(p)) return true;
            }

            // Ou se tem padrão de vírgula com número: "..., 123"
            if (Regex.IsMatch(texto, @",\s*\d+")) return true;

            return false;
        }

        private class LinhaTextoComY
        {
            public double Y { get; set; }
            public string Text { get; set; } = string.Empty;
        }

        private class BlocoProdutoItem
        {
            public double TopY { get; set; }
            public double BottomY { get; set; }
            public List<string> Linhas { get; set; } = new();
        }

        private static List<ItemEnvio> ExtrairItensTabela(List<Word> wordsP1)
        {
            var itens = new List<ItemEnvio>();

            var prodHeader = wordsP1.FirstOrDefault(w => w.Text.Equals("Produto", StringComparison.OrdinalIgnoreCase));
            var subtotalWord = wordsP1.FirstOrDefault(w => w.Text.Equals("Subtotal", StringComparison.OrdinalIgnoreCase));

            if (prodHeader != null && subtotalWord != null && prodHeader.BoundingBox.Bottom > subtotalWord.BoundingBox.Bottom)
            {
                double topY = prodHeader.BoundingBox.Bottom - 2;
                double bottomY = subtotalWord.BoundingBox.Bottom + 2;

                var tableWords = wordsP1.Where(w => w.BoundingBox.Bottom < topY && w.BoundingBox.Bottom > bottomY).ToList();

                // Linhas descritivas do produto (Left < 430)
                var prodWords = tableWords.Where(w => w.BoundingBox.Left < 430);
                var prodLinesWithY = AgruparPalavrasEmLinhasComY(prodWords, yTolerance: 3.5);

                if (prodLinesWithY.Count == 0) return itens;

                // Agrupa em blocos de produtos individuais
                var blocos = new List<BlocoProdutoItem>();
                BlocoProdutoItem? blocoAtual = null;

                for (int i = 0; i < prodLinesWithY.Count; i++)
                {
                    var linha = prodLinesWithY[i];

                    if (blocoAtual == null)
                    {
                        blocoAtual = new BlocoProdutoItem { TopY = linha.Y + 8 };
                        blocoAtual.Linhas.Add(linha.Text);
                    }
                    else
                    {
                        blocoAtual.Linhas.Add(linha.Text);
                    }

                    // Critério de término de bloco de um produto:
                    // 1. Linha atual possui "Código:" ou "Codigo:" (sempre o fechamento do bloco na Tray)
                    // 2. OU a próxima linha existe e é um novo título (não é atributo como Tamanho, Disponibilidade, Ref, etc.)
                    bool linhaTemCodigo = linha.Text.Contains("Código:", StringComparison.OrdinalIgnoreCase) ||
                                          linha.Text.Contains("Codigo:", StringComparison.OrdinalIgnoreCase);

                    bool proximaEhNovoProduto = false;
                    if (!linhaTemCodigo && i + 1 < prodLinesWithY.Count)
                    {
                        string prox = prodLinesWithY[i + 1].Text.Trim();
                        bool ehAtributo = prox.StartsWith("Tamanho:", StringComparison.OrdinalIgnoreCase) ||
                                          prox.StartsWith("(Disponibilidade:", StringComparison.OrdinalIgnoreCase) ||
                                          prox.StartsWith("Disponibilidade:", StringComparison.OrdinalIgnoreCase) ||
                                          prox.StartsWith("(Ref", StringComparison.OrdinalIgnoreCase) ||
                                          prox.StartsWith("Ref:", StringComparison.OrdinalIgnoreCase) ||
                                          prox.StartsWith("Código:", StringComparison.OrdinalIgnoreCase) ||
                                          prox.StartsWith("Codigo:", StringComparison.OrdinalIgnoreCase) ||
                                          prox.StartsWith("Marca:", StringComparison.OrdinalIgnoreCase);

                        // Se a próxima linha NÃO for um atributo e o bloco atual já contém um código ou marca ou tamanho,
                        // significa que a próxima linha é início de outro produto.
                        bool blocoJaTemAtributo = blocoAtual.Linhas.Any(l =>
                            l.Contains("Tamanho:", StringComparison.OrdinalIgnoreCase) ||
                            l.Contains("Código:", StringComparison.OrdinalIgnoreCase) ||
                            l.Contains("Codigo:", StringComparison.OrdinalIgnoreCase) ||
                            l.Contains("Marca:", StringComparison.OrdinalIgnoreCase));

                        if (!ehAtributo && blocoJaTemAtributo)
                        {
                            proximaEhNovoProduto = true;
                        }
                    }

                    if (linhaTemCodigo || proximaEhNovoProduto || i == prodLinesWithY.Count - 1)
                    {
                        blocoAtual.BottomY = linha.Y - 5;
                        blocos.Add(blocoAtual);
                        blocoAtual = null;
                    }
                }

                // Para cada bloco de produto, extrai nome (inclusive quebras de linha), atributos, quantidade e valor
                for (int i = 0; i < blocos.Count; i++)
                {
                    var bloco = blocos[i];
                    double rangeTop = bloco.TopY;
                    double rangeBottom = (i < blocos.Count - 1) ? blocos[i + 1].TopY : bottomY;

                    // Quantidade (430 <= Left < 475) nesse range vertical
                    int qtd = 1;
                    var qtdWords = tableWords.Where(w => w.BoundingBox.Left >= 430 && w.BoundingBox.Left < 475 &&
                                                         w.BoundingBox.Bottom <= rangeTop && w.BoundingBox.Bottom >= rangeBottom).ToList();
                    foreach (var qw in qtdWords)
                    {
                        if (int.TryParse(qw.Text, out int q) && q > 0)
                        {
                            qtd = q;
                            break;
                        }
                    }

                    // Valor Total (Left >= 475)
                    decimal valorTotal = 0;
                    var valWords = tableWords.Where(w => w.BoundingBox.Left >= 475 &&
                                                         w.BoundingBox.Bottom <= rangeTop && w.BoundingBox.Bottom >= rangeBottom)
                                             .OrderByDescending(w => w.BoundingBox.Bottom)
                                             .Select(w => w.Text);
                    string valText = string.Join(" ", valWords);
                    var mVal = Regex.Match(valText, @"R\$\s*([\d\.,]+)");
                    if (mVal.Success)
                    {
                        decimal.TryParse(mVal.Groups[1].Value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out valorTotal);
                    }

                    decimal unitario = qtd > 0 ? valorTotal / qtd : valorTotal;

                    string textoCompletoProduto = string.Join(" ", bloco.Linhas);
                    string refProduto = "";
                    string tamProduto = "";
                    string codProduto = "";

                    // Referência
                    var mRef = Regex.Match(textoCompletoProduto, @"\(Ref\.?\s*([^\)]+)\)", RegexOptions.IgnoreCase);
                    if (!mRef.Success)
                        mRef = Regex.Match(textoCompletoProduto, @"Ref:?\s*([\w\.\-]+)", RegexOptions.IgnoreCase);
                    if (mRef.Success) refProduto = mRef.Groups[1].Value.Trim();

                    // Tamanho
                    var mTam = Regex.Match(textoCompletoProduto, @"Tamanho:\s*([^\(\n\r]+?)(?=\s*\(|\s*Código|\s*Codigo|\s*Marca|$)", RegexOptions.IgnoreCase);
                    if (mTam.Success) tamProduto = mTam.Groups[1].Value.Trim();

                    // Código
                    var mCod = Regex.Match(textoCompletoProduto, @"(?:Código|Codigo):\s*(\w+)", RegexOptions.IgnoreCase);
                    if (mCod.Success) codProduto = mCod.Groups[1].Value.Trim();

                    // Nome do Produto: reúne todas as linhas antes dos atributos (resolve quebra de linhas longas de produtos!)
                    var linhasNome = new List<string>();
                    foreach (var linha in bloco.Linhas)
                    {
                        string trimmed = linha.Trim();
                        // Se a linha começa com atributo ou código ou disponibilidade, para de adicionar no nome
                        if (trimmed.StartsWith("Tamanho:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Código:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Codigo:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Marca:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("(Disponibilidade:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Disponibilidade:", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        // Remove atributos inline da linha de nome
                        string linhaLimpa = Regex.Replace(trimmed, @"\(Ref\.?\s*[^\)]+\)", "", RegexOptions.IgnoreCase);
                        linhaLimpa = Regex.Replace(linhaLimpa, @"Ref:?\s*[\w\.\-]+", "", RegexOptions.IgnoreCase);
                        linhaLimpa = Regex.Replace(linhaLimpa, @"\([^\)]*Disponibilidade[^\)]*\)", "", RegexOptions.IgnoreCase);
                        linhaLimpa = Regex.Replace(linhaLimpa, @"Disponibilidade:[^\n\r]+", "", RegexOptions.IgnoreCase);
                        linhaLimpa = Regex.Replace(linhaLimpa, @"\(?\s*(?:Disponível|Disponivel)[^\)]*\)?", "", RegexOptions.IgnoreCase);
                        linhaLimpa = Regex.Replace(linhaLimpa, @"\(?\s*em\s+\d+\s+dias?\s*úteis?\)?", "", RegexOptions.IgnoreCase);
                        linhaLimpa = Regex.Replace(linhaLimpa, @"Tamanho:[^\n\r]+", "", RegexOptions.IgnoreCase);
                        linhaLimpa = Regex.Replace(linhaLimpa, @"(?:Código|Codigo):[^\n\r]+", "", RegexOptions.IgnoreCase);
                        linhaLimpa = linhaLimpa.Replace("°", "").Trim();

                        if (!string.IsNullOrWhiteSpace(linhaLimpa))
                        {
                            linhasNome.Add(linhaLimpa);
                        }
                    }

                    string nomeProduto = linhasNome.Count > 0 ? string.Join(" ", linhasNome) : bloco.Linhas[0];
                    // Remove resquícios e caracteres estranhos como "°" final ou múltiplos espaços
                    nomeProduto = Regex.Replace(nomeProduto, @"\([^\)]*Disponibilidade[^\)]*\)", "", RegexOptions.IgnoreCase);
                    nomeProduto = Regex.Replace(nomeProduto, @"\(?\s*(?:Disponível|Disponivel)[^\)]*\)?", "", RegexOptions.IgnoreCase);
                    nomeProduto = Regex.Replace(nomeProduto, @"\(?\s*em\s+\d+\s+dias?\s*úteis?\)?", "", RegexOptions.IgnoreCase);
                    nomeProduto = nomeProduto.Replace("°", "");
                    nomeProduto = nomeProduto.TrimEnd(' ', '-', '·', ',').Trim();
                    nomeProduto = Regex.Replace(nomeProduto, @"\s+", " ").Trim();

                    itens.Add(new ItemEnvio
                    {
                        NomeProduto = nomeProduto,
                        Referencia = refProduto,
                        Codigo = codProduto,
                        Tamanho = tamProduto,
                        Quantidade = qtd,
                        ValorUnitario = unitario,
                        ValorTotal = valorTotal
                    });
                }
            }

            return itens;
        }

        private static List<LinhaTextoComY> AgruparPalavrasEmLinhasComY(IEnumerable<Word> words, double yTolerance = 3.5)
        {
            var result = new List<LinhaTextoComY>();
            // Agrupa primeiro por Y aproximado
            var sortedWords = words.OrderByDescending(w => w.BoundingBox.Bottom).ToList();

            var currentLineWords = new List<Word>();
            double currentY = -1;

            foreach (var w in sortedWords)
            {
                if (currentY < 0 || Math.Abs(w.BoundingBox.Bottom - currentY) > yTolerance)
                {
                    if (currentLineWords.Count > 0)
                    {
                        // Ordena palavras dentro da linha por X da esquerda para a direita (elimina texto invertido)
                        result.Add(new LinhaTextoComY
                        {
                            Y = currentY,
                            Text = string.Join(" ", currentLineWords.OrderBy(x => x.BoundingBox.Left).Select(x => x.Text))
                        });
                        currentLineWords.Clear();
                    }
                    currentY = w.BoundingBox.Bottom;
                }
                currentLineWords.Add(w);
            }

            if (currentLineWords.Count > 0)
            {
                result.Add(new LinhaTextoComY
                {
                    Y = currentY,
                    Text = string.Join(" ", currentLineWords.OrderBy(x => x.BoundingBox.Left).Select(x => x.Text))
                });
            }

            return result;
        }

        private static List<string> AgruparPalavrasEmLinhas(IEnumerable<Word> words, double yTolerance = 3.5)
        {
            var result = new List<string>();
            var sortedWords = words.OrderByDescending(w => w.BoundingBox.Bottom).ToList();

            var currentLineWords = new List<Word>();
            double currentY = -1;

            foreach (var w in sortedWords)
            {
                if (currentY < 0 || Math.Abs(w.BoundingBox.Bottom - currentY) > yTolerance)
                {
                    if (currentLineWords.Count > 0)
                    {
                        // Sempre ordena as palavras da linha por Left para manter a ordem correta da frase
                        result.Add(string.Join(" ", currentLineWords.OrderBy(x => x.BoundingBox.Left).Select(x => x.Text)));
                        currentLineWords.Clear();
                    }
                    currentY = w.BoundingBox.Bottom;
                }
                currentLineWords.Add(w);
            }

            if (currentLineWords.Count > 0)
            {
                result.Add(string.Join(" ", currentLineWords.OrderBy(x => x.BoundingBox.Left).Select(x => x.Text)));
            }

            return result;
        }

        private static string Digitos(string s) => Regex.Replace(s ?? string.Empty, @"[^\d]", "");

        private static string LimparTelefone(string telCru)
        {
            if (string.IsNullOrWhiteSpace(telCru)) return string.Empty;
            string apenasDig = Digitos(telCru);
            if (apenasDig.Length == 11)
            {
                return $"({apenasDig.Substring(0, 2)}) {apenasDig.Substring(2, 5)}-{apenasDig.Substring(7, 4)}";
            }
            if (apenasDig.Length == 10)
            {
                return $"({apenasDig.Substring(0, 2)}) {apenasDig.Substring(2, 4)}-{apenasDig.Substring(6, 4)}";
            }
            if (apenasDig.Length == 13 && apenasDig.StartsWith("55"))
            {
                return $"({apenasDig.Substring(2, 2)}) {apenasDig.Substring(4, 5)}-{apenasDig.Substring(9, 4)}";
            }
            if (apenasDig.Length == 12 && apenasDig.StartsWith("55"))
            {
                return $"({apenasDig.Substring(2, 2)}) {apenasDig.Substring(4, 4)}-{apenasDig.Substring(8, 4)}";
            }
            return telCru.Trim();
        }

        private static string RemoverAcentos(string texto) => EnvioService.RemoverAcentos(texto);

        public static bool ValidarCpf(string? cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf)) return false;
            string dig = Digitos(cpf);
            if (dig.Length != 11) return false;
            if (new string(dig[0], 11) == dig) return false;

            int[] mult1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] mult2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

            int soma = 0;
            for (int i = 0; i < 9; i++) soma += (dig[i] - '0') * mult1[i];
            int resto = soma % 11;
            int dig1 = resto < 2 ? 0 : 11 - resto;
            if (dig[9] - '0' != dig1) return false;

            soma = 0;
            for (int i = 0; i < 10; i++) soma += (dig[i] - '0') * mult2[i];
            resto = soma % 11;
            int dig2 = resto < 2 ? 0 : 11 - resto;
            return dig[10] - '0' == dig2;
        }

        public static bool ValidarCnpj(string? cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj)) return false;
            string dig = Digitos(cnpj);
            if (dig.Length != 14) return false;
            if (new string(dig[0], 14) == dig) return false;

            int[] mult1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] mult2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

            int soma = 0;
            for (int i = 0; i < 12; i++) soma += (dig[i] - '0') * mult1[i];
            int resto = soma % 11;
            int dig1 = resto < 2 ? 0 : 11 - resto;
            if (dig[12] - '0' != dig1) return false;

            soma = 0;
            for (int i = 0; i < 13; i++) soma += (dig[i] - '0') * mult2[i];
            resto = soma % 11;
            int dig2 = resto < 2 ? 0 : 11 - resto;
            return dig[13] - '0' == dig2;
        }

        public static string FormatarCpfCnpj(string? doc)
        {
            if (string.IsNullOrWhiteSpace(doc)) return string.Empty;
            string dig = Digitos(doc);
            if (dig.Length == 11)
                return $"{dig.Substring(0, 3)}.{dig.Substring(3, 3)}.{dig.Substring(6, 3)}-{dig.Substring(9, 2)}";
            if (dig.Length == 14)
                return $"{dig.Substring(0, 2)}.{dig.Substring(2, 3)}.{dig.Substring(5, 3)}/{dig.Substring(8, 4)}-{dig.Substring(12, 2)}";
            return doc.Trim();
        }

        public static string? ExtrairCpfOuCnpjDeTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            // 1. Tenta CNPJ formatado primeiro (14 dígitos)
            var mCnpj = Regex.Match(texto, @"\b(\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2})\b");
            if (mCnpj.Success)
            {
                string val = mCnpj.Groups[1].Value.Trim();
                if (ValidarCnpj(val)) return val;
            }

            // 2. Tenta CPF formatado (11 dígitos)
            var mCpf = Regex.Match(texto, @"\b(\d{3}\.\d{3}\.\d{3}-\d{2})\b");
            if (mCpf.Success)
            {
                string val = mCpf.Groups[1].Value.Trim();
                if (ValidarCpf(val)) return val;
            }

            // 3. Tenta prefixos explícitos "CNPJ:", "CPF:", "Doc:"
            var mPrefix = Regex.Match(texto, @"(?:CNPJ|CPF|Doc(?:umento)?):\s*([\d\.\-\/]+)", RegexOptions.IgnoreCase);
            if (mPrefix.Success)
            {
                string raw = mPrefix.Groups[1].Value.Trim();
                string d = Digitos(raw);
                if (d.Length == 14 && ValidarCnpj(d)) return FormatarCpfCnpj(d);
                if (d.Length == 11 && ValidarCpf(d)) return FormatarCpfCnpj(d);
                if (d.Length == 11 || d.Length == 14) return FormatarCpfCnpj(d);
            }

            return null;
        }
    }
}
