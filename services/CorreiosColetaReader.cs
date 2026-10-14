using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ControleEnvios.Data;
using ControleEnvios.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace ControleEnvios.Services
{
    public class ColetaItemResultado
    {
        public string NumeroPedido { get; set; } = string.Empty;
        public string CodigoRastreio { get; set; } = string.Empty;
        public decimal ValorCorreios { get; set; }
        public string Destinatario { get; set; } = string.Empty;
        public string Cep { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
        public string NotaFiscal { get; set; } = string.Empty;
        public string DataPostagem { get; set; } = string.Empty;
        public string StatusVinculo { get; set; } = string.Empty;
    }

    public class ResultadoColetaEmail
    {
        public int TotalAtualizados { get; set; }
        public int TotalNovos { get; set; }
        public List<ColetaItemResultado> NovosItens { get; set; } = new();
        public string Mensagem { get; set; } = string.Empty;

        public void Deconstruct(out int atualizados, out int novos, out string mensagem)
        {
            atualizados = TotalAtualizados;
            novos = TotalNovos;
            mensagem = Mensagem;
        }

        public void Deconstruct(out int atualizados, out int novos, out List<ColetaItemResultado> itensNovos, out string mensagem)
        {
            atualizados = TotalAtualizados;
            novos = TotalNovos;
            itensNovos = NovosItens;
            mensagem = Mensagem;
        }
    }

    public static class CorreiosColetaReader
    {
        static CorreiosColetaReader()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch { }
        }

        public static (int atualizados, int novos, List<ColetaItemResultado> itens) ProcessarArquivoHtml(string caminhoHtml)
        {
            if (!File.Exists(caminhoHtml))
                throw new FileNotFoundException($"Arquivo HTML não encontrado: {caminhoHtml}");

            byte[] bytes = File.ReadAllBytes(caminhoHtml);
            string html = DecodificarBytesHtml(bytes);
            return ProcessarConteudoHtmlOuTexto(html);
        }

        public static string DecodificarBytesHtml(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";

            // Inspeciona charset nos primeiros 1024 bytes
            string preview = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1024));
            if (preview.Contains("windows-1252", StringComparison.OrdinalIgnoreCase) ||
                preview.Contains("iso-8859-1", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Encoding.GetEncoding("windows-1252").GetString(bytes);
                }
                catch { }
            }

            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                try
                {
                    return Encoding.GetEncoding("windows-1252").GetString(bytes);
                }
                catch
                {
                    return Encoding.Default.GetString(bytes);
                }
            }
        }

        public static (int atualizados, int novos, List<ColetaItemResultado> itens) ProcessarConteudoHtmlOuTexto(string conteudo)
        {
            var itens = new List<ColetaItemResultado>();
            int atualizados = 0;
            int novos = 0;

            if (string.IsNullOrWhiteSpace(conteudo))
                return (0, 0, itens);

            // 1. Tenta extrair da estrutura tabular dos Correios (Demonstrativo para Faturamento)
            var trMatches = Regex.Matches(conteudo, @"<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var codigosProcessados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match tr in trMatches)
            {
                var tdMatches = Regex.Matches(tr.Groups[1].Value, @"<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (tdMatches.Count < 5) continue;

                var celulas = new List<string>();
                foreach (Match td in tdMatches)
                {
                    string texto = Regex.Replace(td.Groups[1].Value, @"<[^>]+>", " ");
                    texto = System.Net.WebUtility.HtmlDecode(texto);
                    texto = Regex.Replace(texto, @"\s+", " ").Trim();
                    celulas.Add(texto);
                }

                // Identifica se há código de rastreio em alguma das células (geralmente coluna 8: Qtd./Reg.)
                string codigoRastreio = "";
                for (int i = 0; i < celulas.Count; i++)
                {
                    var mRast = Regex.Match(celulas[i], @"\b([A-Z]{2}\s*\d{9}\s*[A-Z]{2})\b", RegexOptions.IgnoreCase);
                    if (mRast.Success)
                    {
                        codigoRastreio = Regex.Replace(mRast.Value, @"\s+", "").ToUpperInvariant();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(codigoRastreio)) continue;
                if (codigosProcessados.Contains(codigoRastreio)) continue;
                codigosProcessados.Add(codigoRastreio);

                // Coluna 1: Nome Destinatário
                string dest = celulas.Count > 1 ? celulas[1] : "";
                if (dest.StartsWith("Total", StringComparison.OrdinalIgnoreCase)) continue;

                // Coluna 2: CEP
                string cep = celulas.Count > 2 ? celulas[2] : "";

                // Coluna 3: UF
                string uf = celulas.Count > 3 ? celulas[3] : "";

                // Coluna 0: Data
                string dataPostagem = celulas.Count > 0 ? celulas[0] : "";

                // Coluna 9: N.Fiscal
                string nf = celulas.Count > 9 ? celulas[9] : "";

                // Coluna 11 (ou última coluna monetária): Valor
                decimal valorCorreios = 0;
                for (int i = celulas.Count - 1; i >= 0; i--)
                {
                    string cel = celulas[i].Replace("R$", "").Trim();
                    if (decimal.TryParse(cel.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) && v > 0)
                    {
                        valorCorreios = v;
                        break;
                    }
                }

                var (atualizado, inserido, pedido) = EnvioService.AtualizarDadosColetaAvancado(
                    codigoRastreio, dest, cep, uf, nf, valorCorreios, dataPostagem
                );

                if (atualizado) atualizados++;
                if (inserido) novos++;

                itens.Add(new ColetaItemResultado
                {
                    CodigoRastreio = codigoRastreio,
                    Destinatario = dest,
                    Cep = cep,
                    Uf = uf,
                    NotaFiscal = nf,
                    ValorCorreios = valorCorreios,
                    DataPostagem = dataPostagem,
                    NumeroPedido = pedido ?? "",
                    StatusVinculo = atualizado ? $"Vinculado ao Pedido #{pedido}" : (inserido ? "Novo envio cadastrado" : "Não vinculado")
                });
            }

            // 2. Se a análise tabular não encontrou registros, executa busca textual genérica
            if (itens.Count == 0)
            {
                string textoLimpo = Regex.Replace(conteudo, @"<[^>]+>", " ");
                textoLimpo = System.Net.WebUtility.HtmlDecode(textoLimpo);
                textoLimpo = Regex.Replace(textoLimpo, @"\s+", " ");

                var matchesRastreio = Regex.Matches(textoLimpo, @"\b([A-Z]{2}\s*\d{3}\s*\d{3}\s*\d{3}\s*[A-Z]{2})\b", RegexOptions.IgnoreCase);

                foreach (Match m in matchesRastreio)
                {
                    string codigo = Regex.Replace(m.Value, @"\s+", "").ToUpperInvariant();
                    if (codigosProcessados.Contains(codigo)) continue;
                    codigosProcessados.Add(codigo);

                    int pos = m.Index;
                    int start = Math.Max(0, pos - 200);
                    int length = Math.Min(textoLimpo.Length - start, 400);
                    string contexto = textoLimpo.Substring(start, length);

                    decimal valorCorreios = 0;
                    var mVal = Regex.Match(contexto, @"R\$\s*([\d\.,]+)");
                    if (mVal.Success)
                    {
                        decimal.TryParse(mVal.Groups[1].Value.Replace(".", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out valorCorreios);
                    }

                    string pedido = "";
                    var mPed = Regex.Match(contexto, @"(?:Pedido|Nº|Ped\.?)\s*#?\s*(\d{3,8})", RegexOptions.IgnoreCase);
                    if (mPed.Success)
                    {
                        pedido = mPed.Groups[1].Value;
                    }

                    var (atualizado, inserido, pedResultado) = EnvioService.AtualizarDadosColetaAvancado(
                        codigo, null, null, null, null, valorCorreios, null
                    );

                    if (atualizado) atualizados++;
                    if (inserido) novos++;

                    itens.Add(new ColetaItemResultado
                    {
                        CodigoRastreio = codigo,
                        ValorCorreios = valorCorreios,
                        NumeroPedido = pedido,
                        StatusVinculo = atualizado ? $"Vinculado (#{pedResultado})" : (inserido ? "Novo cadastrado" : "Pendente")
                    });
                }
            }

            return (atualizados, novos, itens);
        }

        public static async Task<ResultadoColetaEmail> VerificarEmailsColetaAsync()
        {
            string host = DatabaseContext.ObterConfig("email_host", "imap.gmail.com");
            string portStr = DatabaseContext.ObterConfig("email_porta", "993");
            string usuario = DatabaseContext.ObterConfig("email_usuario", "");
            string senha = DatabaseContext.ObterConfig("email_senha", "");
            string remetenteFiltro = DatabaseContext.ObterConfig("email_remetente_filtro", "naorespondacorreios@gmail.com");

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(senha))
            {
                return new ResultadoColetaEmail
                {
                    Mensagem = "E-mail ou Senha de Aplicativo não configurados. Acesse '⚙️ Configurações' para informar sua conta Gmail e a Senha de App."
                };
            }

            return await Task.Run(async () =>
            {
                using var client = new ImapClient();
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                int porta = int.TryParse(portStr, out int p) ? p : 993;
                var sslOption = porta == 993 ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.Auto;

                try
                {
                    await client.ConnectAsync(host, porta, sslOption);
                }
                catch (Exception ex)
                {
                    return new ResultadoColetaEmail
                    {
                        Mensagem = $"Não foi possível conectar ao servidor IMAP ({host}:{porta}): {ex.Message}"
                    };
                }

                try
                {
                    string senhaLimpa = senha.Replace(" ", "").Trim();
                    await client.AuthenticateAsync(usuario.Trim(), senhaLimpa);
                }
                catch (MailKit.Security.AuthenticationException ex)
                {
                    await client.DisconnectAsync(true);
                    return new ResultadoColetaEmail
                    {
                        Mensagem = $"Falha de autenticação no Gmail ({usuario}):\n" +
                                   "1. Ative o IMAP no Gmail: mail.google.com > Configurações (⚙️) > 'Ver todas as configurações' > aba 'Encaminhamento e POP/IMAP' > marque 'Ativar IMAP' e salve.\n" +
                                   "2. Conta correta: Verifique se a Senha de App foi gerada logado na conta exata ({usuario}) e não em uma conta pessoal.\n" +
                                   $"Detalhe: {ex.Message}"
                    };
                }
                catch (Exception ex)
                {
                    await client.DisconnectAsync(true);
                    return new ResultadoColetaEmail
                    {
                        Mensagem = $"Erro na autenticação: {ex.Message}"
                    };
                }

                try
                {
                    var inbox = client.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadWrite);

                    SearchQuery query;
                    if (!string.IsNullOrWhiteSpace(remetenteFiltro))
                    {
                        query = SearchQuery.FromContains(remetenteFiltro.Trim());
                    }
                    else
                    {
                        query = SearchQuery.All;
                    }

                    var uids = await inbox.SearchAsync(query);

                    if (uids.Count == 0)
                    {
                        await client.DisconnectAsync(true);
                        return new ResultadoColetaEmail
                        {
                            Mensagem = $"Conexão realizada com sucesso! Nenhum e-mail de '{remetenteFiltro}' foi localizado na Caixa de Entrada."
                        };
                    }

                    int totalAtualizados = 0;
                    int totalNovos = 0;
                    int emailsProcessadosNestaRodada = 0;
                    var novosItensLista = new List<ColetaItemResultado>();

                    // Analisa os e-mails mais recentes (até os últimos 25)
                    var uidsRecentes = uids.Skip(Math.Max(0, uids.Count - 25)).Reverse().ToList();

                    foreach (var uid in uidsRecentes)
                    {
                        var summaryList = await inbox.FetchAsync(new[] { uid }, MessageSummaryItems.Envelope | MessageSummaryItems.Flags);
                        var summary = summaryList.FirstOrDefault();
                        string messageId = summary?.Envelope?.MessageId ?? $"UID-{uid}";

                        // Evita reprocessar e-mail já lido anteriormente
                        if (DatabaseContext.EmailJaProcessado(messageId))
                        {
                            continue;
                        }

                        var message = await inbox.GetMessageAsync(uid);
                        string htmlConteudo = "";

                        // 1. Procura anexos .html / .htm
                        foreach (var attachment in message.Attachments)
                        {
                            string nomeArquivo = attachment.ContentDisposition?.FileName ?? attachment.ContentType.Name ?? "";
                            bool isHtml = nomeArquivo.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                                       || nomeArquivo.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
                                       || attachment.ContentType.MimeType.Equals("text/html", StringComparison.OrdinalIgnoreCase);

                            if (isHtml && attachment is MimePart mimePart && mimePart.Content != null)
                            {
                                using var stream = new MemoryStream();
                                await mimePart.Content.DecodeToAsync(stream);
                                byte[] bytes = stream.ToArray();
                                htmlConteudo = DecodificarBytesHtml(bytes);
                                break;
                            }
                        }

                        // 2. Se não houver anexo HTML, verifica se o corpo do e-mail é o próprio demonstrativo
                        if (string.IsNullOrEmpty(htmlConteudo) && !string.IsNullOrEmpty(message.HtmlBody))
                        {
                            if (message.HtmlBody.Contains("DEMONSTRATIVO", StringComparison.OrdinalIgnoreCase) ||
                                message.HtmlBody.Contains("FATURAMENTO", StringComparison.OrdinalIgnoreCase))
                            {
                                htmlConteudo = message.HtmlBody;
                            }
                        }

                        if (!string.IsNullOrEmpty(htmlConteudo))
                        {
                            var (atu, nov, itensProcessados) = ProcessarConteudoHtmlOuTexto(htmlConteudo);
                            totalAtualizados += atu;
                            totalNovos += nov;
                            emailsProcessadosNestaRodada++;

                            if (itensProcessados != null)
                            {
                                novosItensLista.AddRange(itensProcessados.Where(x => x.StatusVinculo.Contains("Novo", StringComparison.OrdinalIgnoreCase)));
                            }

                            DatabaseContext.RegistrarEmailProcessado(
                                messageId,
                                message.From.ToString(),
                                message.Subject ?? "",
                                message.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                                atu + nov
                            );

                            // Marca como lido no Gmail
                            try
                            {
                                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true);
                            }
                            catch { }
                        }
                    }

                    await client.DisconnectAsync(true);

                    if (emailsProcessadosNestaRodada > 0)
                    {
                        string msgAviso = $"✓ Sucesso! {emailsProcessadosNestaRodada} e-mail(s) dos Correios processado(s): {totalAtualizados} pedido(s) atualizados com custo e rastreio, {totalNovos} novo(s) cadastrado(s).";
                        if (totalNovos > 0)
                        {
                            msgAviso += $"\n\n⚠️ ATENÇÃO: {totalNovos} envio(s) foi(ram) adicionado(s) e cobrado(s) pelos Correios, mas o PDF da Tray ainda não foi extraído!";
                        }

                        return new ResultadoColetaEmail
                        {
                            TotalAtualizados = totalAtualizados,
                            TotalNovos = totalNovos,
                            NovosItens = novosItensLista,
                            Mensagem = msgAviso
                        };
                    }
                    else
                    {
                        return new ResultadoColetaEmail
                        {
                            Mensagem = $"✓ Conexão bem-sucedida ao Gmail! Todos os e-mails de '{remetenteFiltro}' já foram processados anteriormente. Nenhum comprovante novo pendente."
                        };
                    }
                }
                catch (Exception ex)
                {
                    try { await client.DisconnectAsync(true); } catch { }
                    return new ResultadoColetaEmail
                    {
                        Mensagem = $"Erro ao ler e-mails da caixa postal: {ex.Message}"
                    };
                }
            });
        }
    }
}
