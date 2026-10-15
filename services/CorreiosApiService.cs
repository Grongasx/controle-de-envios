using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ControleEnvios.Data;
using ControleEnvios.Models;

namespace ControleEnvios.Services
{
    public class ResultadoRastreio
    {
        public bool Sucesso { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Status { get; set; } = "Pendente";
        public string Descricao { get; set; } = string.Empty;
        public string Detalhe { get; set; } = string.Empty;
        public string UltimoEventoData { get; set; } = string.Empty;
        public string UltimoEventoLocal { get; set; } = string.Empty;
        public bool Entregue { get; set; }
        public string Erro { get; set; } = string.Empty;
        public List<EventoRastreio> Eventos { get; set; } = new();
    }

    public class ResultadoManifestacao
    {
        public bool Sucesso { get; set; }
        public string Protocolo { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;
        public string MotivoRecusa { get; set; } = string.Empty;
        public DateTime? DataPermitidaReenvio { get; set; }
        public string DataPermitidaReenvioFormatada => DataPermitidaReenvio.HasValue ? DataPermitidaReenvio.Value.ToString("dd/MM/yyyy") : "";
        public string DetalheCompleto { get; set; } = string.Empty;
        public bool PodeReenviarManualmente => true;
    }

    public class ResultadoConsultaReversa
    {
        public bool Sucesso { get; set; }
        public bool Postado { get; set; }
        public string CodigoAutorizacao { get; set; } = string.Empty;
        public string CodigoObjetoGerado { get; set; } = string.Empty;
        public string Status { get; set; } = "Pendente";
        public string Detalhe { get; set; } = string.Empty;
        public DateTime? DataPostagem { get; set; }
        public DateTime? DataValidade { get; set; }
        public string Erro { get; set; } = string.Empty;
    }

    public static class CorreiosApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.correios.com.br"),
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static string? _tokenCache;
        private static DateTime? _tokenExpiraEm;

        public static async Task<string> ObterTokenAsync(bool forcarRenovacao = false)
        {
            var agora = DateTime.Now;
            if (!forcarRenovacao && !string.IsNullOrEmpty(_tokenCache) && _tokenExpiraEm.HasValue && agora < (_tokenExpiraEm.Value - TimeSpan.FromMinutes(5)))
            {
                return _tokenCache;
            }

            string usuario = DatabaseContext.ObterConfig("correios_usuario").Trim();
            string codigoAcesso = DatabaseContext.ObterConfig("correios_codigo_acesso").Trim();

            if (string.IsNullOrWhiteSpace(codigoAcesso))
            {
                throw new InvalidOperationException("Chave de Acesso da API dos Correios não configurada. Acesse Configurações.");
            }

            // Se for Bearer Token direto
            if (codigoAcesso.StartsWith("eyJ", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(usuario))
            {
                _tokenCache = codigoAcesso;
                _tokenExpiraEm = agora.AddHours(24);
                return _tokenCache;
            }

            string rawAuth = $"{usuario}:{codigoAcesso}";
            string base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawAuth));

            using var request = new HttpRequestMessage(HttpMethod.Post, "/token/v1/autentica");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Falha ao autenticar na API dos Correios (HTTP {(int)response.StatusCode}): {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("token", out var propToken))
            {
                throw new Exception("Token não retornado pela API dos Correios.");
            }

            string token = propToken.GetString() ?? "";
            _tokenCache = token;

            if (root.TryGetProperty("expiraEm", out var propExpira) && DateTime.TryParse(propExpira.GetString(), out var expiraDt))
            {
                _tokenExpiraEm = expiraDt;
            }
            else
            {
                _tokenExpiraEm = agora.AddHours(6);
            }

            return token;
        }

        public static async Task<(bool sucesso, string token, string mensagem)> TestarAutenticacaoAsync(string usuario, string codigoAcesso)
        {
            try
            {
                string chave = (codigoAcesso ?? "").Trim();
                string user = (usuario ?? "").Trim();

                if (string.IsNullOrWhiteSpace(chave))
                {
                    return (false, "", "Informe a Chave de Acesso da API dos Correios.");
                }

                if (chave.StartsWith("eyJ", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(user))
                {
                    return (true, chave, "✓ Chave/Token configurado com sucesso.");
                }

                string rawAuth = $"{user}:{chave}";
                string base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawAuth));

                using var request = new HttpRequestMessage(HttpMethod.Post, "/token/v1/autentica");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, "", $"HTTP {(int)response.StatusCode}: {json}");
                }

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("token", out var pTok))
                {
                    return (true, pTok.GetString() ?? "", "✓ Chave de API validada com sucesso! Conexão ativa com os Correios.");
                }
                return (false, "", "Token não retornado pela API dos Correios.");
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        public static async Task<ResultadoRastreio> RastrearObjetoAsync(string codigo)
        {
            string codLimpo = (codigo ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(codLimpo))
            {
                return new ResultadoRastreio { Sucesso = false, Erro = "Código de rastreio vazio." };
            }

            // Se for código exclusivamente numérico (8 a 11 dígitos), é Autorização de Logística Reversa
            if (Regex.IsMatch(codLimpo, @"^\d{8,11}$"))
            {
                var revRes = await ConsultarAutorizacaoReversaAsync(codLimpo);
                if (revRes.Postado && !string.IsNullOrWhiteSpace(revRes.CodigoObjetoGerado))
                {
                    var sroRes = await RastrearObjetoAsync(revRes.CodigoObjetoGerado);
                    sroRes.Descricao = $"[Logística Reversa] {sroRes.Descricao}";
                    return sroRes;
                }

                return new ResultadoRastreio
                {
                    Sucesso = true,
                    Codigo = codLimpo,
                    Status = "Aguardando Postagem",
                    Descricao = !string.IsNullOrWhiteSpace(revRes.Detalhe) ? revRes.Detalhe : "Autorização de Postagem Reversa ativa. Aguardando entrega pelo cliente em agência dos Correios.",
                    Detalhe = revRes.DataValidade.HasValue ? $"Validade: {revRes.DataValidade.Value:dd/MM/yyyy}" : "",
                    Entregue = false
                };
            }

            try
            {
                string token = await ObterTokenAsync();

                using var request = new HttpRequestMessage(HttpMethod.Get, $"/srorastro/v1/objetos/{codLimpo}?resultado=T");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);

                // Se token expirou (401), tenta renovar 1 vez
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    token = await ObterTokenAsync(forcarRenovacao: true);
                    using var retryRequest = new HttpRequestMessage(HttpMethod.Get, $"/srorastro/v1/objetos/{codLimpo}?resultado=T");
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    retryRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    response = await _httpClient.SendAsync(retryRequest);
                }

                string content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return new ResultadoRastreio
                    {
                        Sucesso = false,
                        Codigo = codLimpo,
                        Erro = $"Erro HTTP {(int)response.StatusCode}: {content}"
                    };
                }

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (!root.TryGetProperty("objetos", out var objetos) || objetos.GetArrayLength() == 0)
                {
                    return new ResultadoRastreio
                    {
                        Sucesso = true,
                        Codigo = codLimpo,
                        Status = "Não Encontrado",
                        Descricao = "Objeto não consta na base dos Correios",
                        Entregue = false
                    };
                }

                var obj = objetos[0];
                if (!obj.TryGetProperty("eventos", out var eventos) || eventos.GetArrayLength() == 0)
                {
                    string msg = obj.TryGetProperty("mensagem", out var m) ? m.GetString() ?? "" : "Sem eventos";
                    return new ResultadoRastreio
                    {
                        Sucesso = true,
                        Codigo = codLimpo,
                        Status = "Aguardando Postagem",
                        Descricao = msg,
                        Entregue = false
                    };
                }

                var resultado = new ResultadoRastreio
                {
                    Sucesso = true,
                    Codigo = codLimpo
                };

                // O primeiro evento é o mais recente
                var primeiroEvento = eventos[0];
                string codEvento = primeiroEvento.TryGetProperty("codigo", out var c) ? c.GetString() ?? "" : "";
                string descEvento = primeiroEvento.TryGetProperty("descricao", out var d) ? d.GetString() ?? "" : "";
                string dtHr = primeiroEvento.TryGetProperty("dtHrCriado", out var dt) ? dt.GetString() ?? "" : "";
                string detalhe = primeiroEvento.TryGetProperty("detalhe", out var det) ? det.GetString() ?? "" : "";

                string local = "";
                if (primeiroEvento.TryGetProperty("unidade", out var un))
                {
                    if (un.TryGetProperty("endereco", out var end))
                    {
                        string cid = end.TryGetProperty("cidade", out var ci) ? ci.GetString() ?? "" : "";
                        string uf = end.TryGetProperty("uf", out var u) ? u.GetString() ?? "" : "";
                        local = $"{cid}/{uf}".Trim('/');
                    }
                    if (string.IsNullOrEmpty(local) && un.TryGetProperty("tipo", out var tp))
                    {
                        local = tp.GetString() ?? "";
                    }
                }

                resultado.Descricao = descEvento;
                resultado.Detalhe = detalhe;
                resultado.UltimoEventoData = dtHr;
                resultado.UltimoEventoLocal = local;

                // Classificação unificada de status
                string descLower = (descEvento + " " + detalhe).ToLowerInvariant();
                if (descLower.Contains("não entregue") || descLower.Contains("não atendido") || descLower.Contains("destinatário ausente") || descLower.Contains("não pôde ser efetuada") || descLower.Contains("não pode ser efetuada") || descLower.Contains("nova tentativa"))
                {
                    resultado.Status = "Carteiro Não Atendido";
                    resultado.Entregue = false;
                }
                else if (codEvento is "BDE" or "BDI" or "BDR" || descLower.Contains("entregue ao destinatário") || (descLower.Contains("entregue") && !descLower.Contains("não")))
                {
                    resultado.Status = "Entregue";
                    resultado.Entregue = true;
                }
                else if (codEvento == "OEC" || descLower.Contains("saiu para entrega"))
                {
                    resultado.Status = "Saiu para Entrega";
                }
                else if (codEvento == "LDI" || descLower.Contains("aguardando retirada"))
                {
                    resultado.Status = "Aguardando Retirada";
                }
                else if (codEvento is "RO" or "DO" || descLower.Contains("em trânsito") || descLower.Contains("transito") || descLower.Contains("encaminhado"))
                {
                    resultado.Status = "Em Trânsito";
                }
                else if (codEvento == "PO" || descLower.Contains("postado"))
                {
                    resultado.Status = "Postado";
                }
                else if (descLower.Contains("devolvido") || descLower.Contains("extraviado") || descLower.Contains("problema") || descLower.Contains("retido"))
                {
                    resultado.Status = "Alerta / Retido";
                }
                else
                {
                    resultado.Status = descEvento;
                }

                // Processa lista completa de eventos para histórico
                foreach (var ev in eventos.EnumerateArray())
                {
                    string evDt = ev.TryGetProperty("dtHrCriado", out var eDt) ? eDt.GetString() ?? "" : "";
                    string evDesc = ev.TryGetProperty("descricao", out var eDc) ? eDc.GetString() ?? "" : "";
                    string evDet = ev.TryGetProperty("detalhe", out var eDt2) ? eDt2.GetString() ?? "" : "";
                    string evCod = ev.TryGetProperty("codigo", out var eC) ? eC.GetString() ?? "" : "";

                    string evLocal = "";
                    if (ev.TryGetProperty("unidade", out var uObj))
                    {
                        if (uObj.TryGetProperty("endereco", out var uEnd))
                        {
                            string cidVal = uEnd.TryGetProperty("cidade", out var ci) ? ci.GetString() ?? "" : "";
                            string f = uEnd.TryGetProperty("uf", out var uf) ? uf.GetString() ?? "" : "";
                            evLocal = $"{cidVal}/{f}".Trim('/');
                        }
                    }

                    string evDestino = "";
                    if (ev.TryGetProperty("unidadeDestino", out var dObj))
                    {
                        if (dObj.TryGetProperty("endereco", out var dEnd))
                        {
                            string cidDest = dEnd.TryGetProperty("cidade", out var ci2) ? ci2.GetString() ?? "" : "";
                            string f = dEnd.TryGetProperty("uf", out var uf2) ? uf2.GetString() ?? "" : "";
                            evDestino = $"{cidDest}/{f}".Trim('/');
                        }
                    }

                    resultado.Eventos.Add(new EventoRastreio
                    {
                        CodigoRastreio = codLimpo,
                        DataHora = evDt,
                        Descricao = evDesc,
                        Detalhe = evDet,
                        UnidadeLocal = evLocal,
                        UnidadeDestino = evDestino,
                        TipoEvento = evCod
                    });
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return new ResultadoRastreio
                {
                    Sucesso = false,
                    Codigo = codLimpo,
                    Erro = ex.Message
                };
            }
        }

        public static async Task<ResultadoManifestacao> AbrirManifestacaoAsync(Envio envio)
        {
            if (envio == null || string.IsNullOrWhiteSpace(envio.CodigoRastreio))
            {
                return new ResultadoManifestacao
                {
                    Sucesso = false,
                    MotivoRecusa = "O envio não possui código de rastreio dos Correios.",
                    Mensagem = "Não é possível registrar manifestação sem código de rastreio."
                };
            }

            string token;
            try
            {
                token = await ObterTokenAsync();
            }
            catch (Exception ex)
            {
                return new ResultadoManifestacao
                {
                    Sucesso = false,
                    MotivoRecusa = "Credenciais dos Correios não configuradas ou token inválido.",
                    Mensagem = $"Falha na autenticação: {ex.Message}",
                    DataPermitidaReenvio = DateTime.Today.AddDays(1)
                };
            }

            // Monta payload oficial da API de Manifestações / CWS Correios
            var payload = new
            {
                codigoObjeto = envio.CodigoRastreio.Trim().ToUpperInvariant(),
                tipoManifestante = "R", // R = Remetente
                codigoMotivo = "1", // 1 = Atraso na entrega
                descricaoManifestacao = string.IsNullOrWhiteSpace(envio.MotivoDemora)
                    ? $"Objeto em atraso sem atualização recente no rastreio para o destinatário {envio.Cliente}."
                    : $"{envio.MotivoDemora}. Solicitamos providências e localização do pacote.",
                remetente = new
                {
                    nome = envio.OrigemTipo == "DROPSHIPPING" && !string.IsNullOrWhiteSpace(envio.FornecedorNome)
                        ? envio.FornecedorNome
                        : "WebRiders",
                    tipoPessoa = "J",
                    cnpjCpf = DatabaseContext.ObterConfig("correios_usuario", "")
                },
                destinatario = new
                {
                    nome = envio.Cliente,
                    cpfCnpj = Regex.Replace(envio.ClienteCpf ?? "", @"[^\d]", ""),
                    email = envio.ClienteEmail,
                    telefone = Regex.Replace(envio.ClienteCelular ?? "", @"[^\d]", ""),
                    logradouro = envio.EnderecoEntrega,
                    cidade = envio.CidadeDestino,
                    uf = envio.UfDestino,
                    cep = Regex.Replace(envio.CepDestino ?? "", @"[^\d]", "")
                },
                dadosEncomenda = new
                {
                    numeroNotaFiscal = envio.NotaFiscal,
                    valorDeclarado = envio.ValorPedido,
                    peso = envio.PesoGramas
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/manifestacoes/v1/manifestacoes")
                {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                // 1. Sucesso (HTTP 200, 201, 202)
                if (response.IsSuccessStatusCode)
                {
                    string protocolo = "";
                    string msgSucesso = "Manifestação registrada com sucesso nos Correios.";

                    try
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("numeroManifestacao", out var p1)) protocolo = p1.GetString() ?? "";
                        else if (root.TryGetProperty("protocolo", out var p2)) protocolo = p2.GetString() ?? "";
                        else if (root.TryGetProperty("numero", out var p3)) protocolo = p3.GetString() ?? "";
                        else if (root.TryGetProperty("id", out var p4)) protocolo = p4.GetString() ?? "";
                        else if (root.TryGetProperty("codigoProtocolo", out var p5)) protocolo = p5.GetString() ?? "";

                        if (root.TryGetProperty("mensagem", out var pMsg)) msgSucesso = pMsg.GetString() ?? msgSucesso;
                    }
                    catch { }

                    if (string.IsNullOrWhiteSpace(protocolo))
                    {
                        protocolo = $"PI-{DateTime.Now:yyyyMMddHHmm}";
                    }

                    return new ResultadoManifestacao
                    {
                        Sucesso = true,
                        Protocolo = protocolo,
                        Mensagem = msgSucesso,
                        DetalheCompleto = responseBody
                    };
                }

                // 2. Resposta Negativa / Recusa (ex: Dentro do prazo, serviço não habilitado, já existente)
                string motivoRecusa = "";
                DateTime? dataPermitida = null;

                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("mensagem", out var pMsg))
                        motivoRecusa = pMsg.GetString() ?? "";
                    else if (root.TryGetProperty("descricao", out var pDesc))
                        motivoRecusa = pDesc.GetString() ?? "";
                    else if (root.TryGetProperty("msgs", out var pMsgs) && pMsgs.ValueKind == JsonValueKind.Array && pMsgs.GetArrayLength() > 0)
                        motivoRecusa = pMsgs[0].GetString() ?? "";
                    else if (root.TryGetProperty("erros", out var pErros) && pErros.ValueKind == JsonValueKind.Array && pErros.GetArrayLength() > 0)
                    {
                        var e0 = pErros[0];
                        motivoRecusa = e0.TryGetProperty("mensagem", out var em) ? em.GetString() ?? "" : e0.ToString();
                    }
                }
                catch
                {
                    motivoRecusa = responseBody;
                }

                if (string.IsNullOrWhiteSpace(motivoRecusa))
                {
                    motivoRecusa = $"Os Correios recusaram a abertura da manifestação (HTTP {(int)response.StatusCode}).";
                }

                // Extração inteligente de data do motivo (ex: "prazo previsto até 18/10/2026")
                var mData = Regex.Match(motivoRecusa, @"\b(\d{2}/\d{2}/\d{4})\b");
                if (mData.Success && DateTime.TryParseExact(mData.Groups[1].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtLimite))
                {
                    // Próximo dia útil após a data limite
                    var dtNext = dtLimite.AddDays(1);
                    while (dtNext.DayOfWeek == DayOfWeek.Saturday || dtNext.DayOfWeek == DayOfWeek.Sunday)
                        dtNext = dtNext.AddDays(1);
                    dataPermitida = dtNext;
                }
                else
                {
                    // Cálculo estimado com base na modalidade e data de envio
                    int diasLimite = (envio.FormaEnvioLoja ?? "").ToUpperInvariant().Contains("SEDEX") ? 5 : 10;
                    DateTime dtBase = DateTime.TryParse(envio.DataEnvio, out var dEnv) ? dEnv : envio.CreatedAt;
                    dataPermitida = dtBase.AddDays(diasLimite + 1);
                    while (dataPermitida.Value.DayOfWeek == DayOfWeek.Saturday || dataPermitida.Value.DayOfWeek == DayOfWeek.Sunday)
                        dataPermitida = dataPermitida.Value.AddDays(1);

                    if (dataPermitida < DateTime.Today)
                    {
                        dataPermitida = DateTime.Today;
                    }
                }

                return new ResultadoManifestacao
                {
                    Sucesso = false,
                    MotivoRecusa = motivoRecusa,
                    DataPermitidaReenvio = dataPermitida,
                    Mensagem = $"Abertura automática negada pelos Correios: {motivoRecusa}",
                    DetalheCompleto = responseBody
                };
            }
            catch (Exception ex)
            {
                return new ResultadoManifestacao
                {
                    Sucesso = false,
                    MotivoRecusa = $"Falha de conexão com a API dos Correios: {ex.Message}",
                    DataPermitidaReenvio = DateTime.Today.AddDays(1),
                    Mensagem = ex.Message
                };
            }
        }

        public static async Task<ResultadoConsultaReversa> ConsultarAutorizacaoReversaAsync(string codigoAutorizacao)
        {
            string codLimpo = Regex.Replace(codigoAutorizacao ?? "", @"[^\d]", "").Trim();
            if (string.IsNullOrWhiteSpace(codLimpo))
            {
                return new ResultadoConsultaReversa { Sucesso = false, Erro = "Código de autorização inválido ou vazio." };
            }

            try
            {
                string usuario = DatabaseContext.ObterConfig("correios_usuario", "").Trim();
                string codigoAcesso = DatabaseContext.ObterConfig("correios_codigo_acesso", "").Trim();
                string cartao = DatabaseContext.ObterConfig("correios_cartao_postagem", "").Trim();
                string ambiente = DatabaseContext.ObterConfig("correios_ambiente", "PRODUCAO").Trim().ToUpperInvariant();

                string codAdmin = !string.IsNullOrWhiteSpace(cartao) ? cartao : usuario;
                if (string.IsNullOrWhiteSpace(codAdmin))
                {
                    codAdmin = "00000000";
                }

                string url = ambiente == "HOMOLOGACAO"
                    ? "https://apphom.correios.com.br/logisticaReversaWS/logisticaReversaService/logisticaReversaWS"
                    : "https://apps.correios.com.br/logisticaReversaWS/logisticaReversaService/logisticaReversaWS";

                string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ser=""http://service.sro.correios.com.br/"">
    <soapenv:Header/>
    <soapenv:Body>
        <ser:acompanharPedido>
            <codAdministrativo>{System.Security.SecurityElement.Escape(codAdmin)}</codAdministrativo>
            <tipoBusca>H</tipoBusca>
            <tipoSolicitacao>A</tipoSolicitacao>
            <numeroPedido>{System.Security.SecurityElement.Escape(codLimpo)}</numeroPedido>
        </ser:acompanharPedido>
    </soapenv:Body>
</soapenv:Envelope>";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                request.Headers.Add("SOAPAction", "");

                if (!string.IsNullOrWhiteSpace(usuario) && !string.IsNullOrWhiteSpace(codigoAcesso))
                {
                    string rawAuth = $"{usuario}:{codigoAcesso}";
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(rawAuth)));
                }

                var response = await _httpClient.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                var resultado = new ResultadoConsultaReversa
                {
                    Sucesso = response.IsSuccessStatusCode,
                    CodigoAutorizacao = codLimpo
                };

                // Extrai número do objeto SRO gerado (<numero_etiqueta> ou <numero_objeto>)
                var mEtiqueta = Regex.Match(responseBody, @"<(?:numero_etiqueta|numero_objeto|codigo_objeto|objeto)>([A-Z]{2}\d{9}[A-Z]{2})<\/(?:numero_etiqueta|numero_objeto|codigo_objeto|objeto)>", RegexOptions.IgnoreCase);
                if (mEtiqueta.Success)
                {
                    resultado.Postado = true;
                    resultado.CodigoObjetoGerado = mEtiqueta.Groups[1].Value.ToUpperInvariant();
                    resultado.Status = "Postado";
                    resultado.Detalhe = $"Objeto de logística reversa postado na agência dos Correios com código {resultado.CodigoObjetoGerado}.";
                }
                else
                {
                    // Verifica status da solicitação
                    var mStatus = Regex.Match(responseBody, @"<(?:status_pedido|descricao_status|situacao)>([^<]+)<\/", RegexOptions.IgnoreCase);
                    string st = mStatus.Success ? mStatus.Groups[1].Value.Trim() : "Pendente";

                    resultado.Status = st;
                    resultado.Postado = st.Contains("POSTAD", StringComparison.OrdinalIgnoreCase);

                    if (resultado.Postado)
                    {
                        var mEt2 = Regex.Match(responseBody, @"\b([A-Z]{2}\d{9}[A-Z]{2})\b");
                        if (mEt2.Success)
                        {
                            resultado.CodigoObjetoGerado = mEt2.Groups[1].Value.ToUpperInvariant();
                        }
                        resultado.Detalhe = "Objeto postado pelo cliente na agência dos Correios.";
                    }
                    else
                    {
                        resultado.Detalhe = "Autorização de postagem ativa. Aguardando entrega pelo cliente em agência dos Correios.";
                    }
                }

                // Extrai prazo de validade se houver
                var mValidade = Regex.Match(responseBody, @"<(?:prazo|data_limite|data_validade)>(\d{2}[\/\-]\d{2}[\/\-]\d{4})<\/", RegexOptions.IgnoreCase);
                if (mValidade.Success && DateTime.TryParse(mValidade.Groups[1].Value.Replace('-', '/'), out var dtVal))
                {
                    resultado.DataValidade = dtVal;
                }

                // Extrai data de postagem se houver
                var mDtPost = Regex.Match(responseBody, @"<(?:data_atualizacao|data_postagem)>(\d{2}[\/\-]\d{2}[\/\-]\d{4})<\/", RegexOptions.IgnoreCase);
                if (mDtPost.Success && DateTime.TryParse(mDtPost.Groups[1].Value.Replace('-', '/'), out var dtPost))
                {
                    resultado.DataPostagem = dtPost;
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return new ResultadoConsultaReversa
                {
                    Sucesso = false,
                    CodigoAutorizacao = codLimpo,
                    Status = "Pendente",
                    Detalhe = "Aguardando postagem pelo cliente.",
                    Erro = ex.Message
                };
            }
        }
    }
}
