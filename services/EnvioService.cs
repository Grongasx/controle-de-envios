using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ControleEnvios.Data;
using ControleEnvios.Models;
using Microsoft.Data.Sqlite;

namespace ControleEnvios.Services
{
    public static class EnvioService
    {
        public static List<Envio> ListarEnvios(string busca = "", string statusFiltro = "TODOS", string origemFiltro = "TODAS")
        {
            var lista = new List<Envio>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            string query = @"
                SELECT 
                    e.*,
                    f.nome as fornecedor_nome,
                    f.apelido as fornecedor_apelido,
                    (SELECT GROUP_CONCAT(i.quantidade || 'x ' || i.nome_produto, '; ') 
                     FROM itens_envio i WHERE i.envio_id = e.id) as resumo_produtos,
                    (SELECT COUNT(*) FROM historico_rastreio h 
                     WHERE h.envio_id = e.id AND (
                        LOWER(h.descricao) LIKE '%não atendido%' OR 
                        LOWER(h.descricao) LIKE '%não entregue%' OR 
                        LOWER(h.descricao) LIKE '%ausente%' OR 
                        LOWER(h.detalhe) LIKE '%não atendido%' OR 
                        LOWER(h.detalhe) LIKE '%ausente%' OR 
                        LOWER(h.detalhe) LIKE '%nova tentativa%'
                     )) as tentativas_nao_atendidas
                FROM envios e
                LEFT JOIN fornecedores f ON e.fornecedor_id = f.id
                WHERE 1=1 ";

            if (!string.IsNullOrWhiteSpace(busca))
            {
                query += @" AND (
                    e.numero_pedido LIKE @b OR 
                    e.cliente LIKE @b OR 
                    e.codigo_rastreio LIKE @b OR 
                    e.nota_fiscal LIKE @b
                )";
                cmd.Parameters.AddWithValue("@b", $"%{busca.Trim()}%");
            }

            if (statusFiltro == "ARQUIVADOS")
            {
                query += " AND e.arquivado = 1";
            }
            else if (statusFiltro == "TODOS_INCLUSIVE_ARQUIVADOS")
            {
                // Sem filtro de arquivado
            }
            else
            {
                query += " AND (e.arquivado IS NULL OR e.arquivado = 0)";
            }

            if (statusFiltro != "TODOS" && statusFiltro != "ARQUIVADOS" && statusFiltro != "TODOS_INCLUSIVE_ARQUIVADOS")
            {
                if (statusFiltro == "PENDENTES_ENTREGA")
                {
                    query += " AND e.entregue = 0";
                }
                else if (statusFiltro == "ENTREGUES")
                {
                    query += " AND e.entregue = 1";
                }
                else if (statusFiltro == "COBRADOS_SEM_PDF")
                {
                    query += " AND e.valor_correios > 0 AND (e.caminho_pdf IS NULL OR e.caminho_pdf = '') AND (e.desativar_avisos IS NULL OR e.desativar_avisos = 0)";
                }
                else if (statusFiltro == "NAO_ATENDIDO")
                {
                    query += @" AND (e.entregue = 0 AND (e.desativar_avisos IS NULL OR e.desativar_avisos = 0) AND (
                        e.status_correios LIKE '%Não Atendido%' OR 
                        e.descricao_status LIKE '%não atendido%' OR 
                        e.descricao_status LIKE '%ausente%' OR 
                        (SELECT COUNT(*) FROM historico_rastreio h WHERE h.envio_id = e.id AND (
                            LOWER(h.descricao) LIKE '%não atendido%' OR 
                            LOWER(h.descricao) LIKE '%ausente%' OR 
                            LOWER(h.descricao) LIKE '%não entregue%'
                        )) > 0
                    ))";
                }
                else
                {
                    query += " AND e.status_correios = @st";
                    cmd.Parameters.AddWithValue("@st", statusFiltro);
                }
            }

            if (origemFiltro != "TODAS")
            {
                if (origemFiltro == "PROPRIO")
                {
                    query += " AND e.origem_tipo = 'PROPRIO'";
                }
                else if (origemFiltro == "DROPSHIPPING")
                {
                    query += " AND e.origem_tipo = 'DROPSHIPPING'";
                }
                else if (origemFiltro.StartsWith("FORNECEDOR_") && int.TryParse(origemFiltro.Replace("FORNECEDOR_", ""), out int fId))
                {
                    query += " AND e.fornecedor_id = @fid";
                    cmd.Parameters.AddWithValue("@fid", fId);
                }
            }

            query += " ORDER BY e.id DESC";
            cmd.CommandText = query;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var env = new Envio
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    NumeroPedido = reader.GetString(reader.GetOrdinal("numero_pedido")),
                    Cliente = reader.GetString(reader.GetOrdinal("cliente")),
                    ClienteCpf = reader.IsDBNull(reader.GetOrdinal("cliente_cpf")) ? "" : reader.GetString(reader.GetOrdinal("cliente_cpf")),
                    ClienteCelular = reader.IsDBNull(reader.GetOrdinal("cliente_celular")) ? "" : reader.GetString(reader.GetOrdinal("cliente_celular")),
                    ClienteEmail = reader.IsDBNull(reader.GetOrdinal("cliente_email")) ? "" : reader.GetString(reader.GetOrdinal("cliente_email")),
                    EnderecoEntrega = reader.IsDBNull(reader.GetOrdinal("endereco_entrega")) ? "" : reader.GetString(reader.GetOrdinal("endereco_entrega")),
                    ComplementoEntrega = reader.IsDBNull(reader.GetOrdinal("complemento_entrega")) ? "" : reader.GetString(reader.GetOrdinal("complemento_entrega")),
                    CidadeDestino = reader.IsDBNull(reader.GetOrdinal("cidade_destino")) ? "" : reader.GetString(reader.GetOrdinal("cidade_destino")),
                    UfDestino = reader.IsDBNull(reader.GetOrdinal("uf_destino")) ? "" : reader.GetString(reader.GetOrdinal("uf_destino")),
                    CepDestino = reader.IsDBNull(reader.GetOrdinal("cep_destino")) ? "" : reader.GetString(reader.GetOrdinal("cep_destino")),
                    FormaEnvioLoja = reader.IsDBNull(reader.GetOrdinal("forma_envio_loja")) ? "" : reader.GetString(reader.GetOrdinal("forma_envio_loja")),
                    PesoGramas = reader.IsDBNull(reader.GetOrdinal("peso_gramas")) ? 0 : reader.GetDouble(reader.GetOrdinal("peso_gramas")),
                    ValorPedido = reader.IsDBNull(reader.GetOrdinal("valor_pedido")) ? 0 : reader.GetDecimal(reader.GetOrdinal("valor_pedido")),
                    ValorFreteLoja = reader.IsDBNull(reader.GetOrdinal("valor_frete_loja")) ? 0 : reader.GetDecimal(reader.GetOrdinal("valor_frete_loja")),
                    OrigemTipo = reader.IsDBNull(reader.GetOrdinal("origem_tipo")) ? "PROPRIO" : reader.GetString(reader.GetOrdinal("origem_tipo")),
                    FornecedorId = reader.IsDBNull(reader.GetOrdinal("fornecedor_id")) ? null : reader.GetInt32(reader.GetOrdinal("fornecedor_id")),
                    FornecedorNome = reader.IsDBNull(reader.GetOrdinal("fornecedor_nome")) ? "" : reader.GetString(reader.GetOrdinal("fornecedor_nome")),
                    FornecedorApelido = reader.IsDBNull(reader.GetOrdinal("fornecedor_apelido")) ? "" : reader.GetString(reader.GetOrdinal("fornecedor_apelido")),
                    NotaFiscal = reader.IsDBNull(reader.GetOrdinal("nota_fiscal")) ? "" : reader.GetString(reader.GetOrdinal("nota_fiscal")),
                    CodigoRastreio = reader.IsDBNull(reader.GetOrdinal("codigo_rastreio")) ? "" : reader.GetString(reader.GetOrdinal("codigo_rastreio")),
                    TipoRastreio = reader.IsDBNull(reader.GetOrdinal("tipo_rastreio")) ? "NORMAL" : reader.GetString(reader.GetOrdinal("tipo_rastreio")),
                    DataEnvio = reader.IsDBNull(reader.GetOrdinal("data_envio")) ? "" : reader.GetString(reader.GetOrdinal("data_envio")),
                    ValorCorreios = reader.IsDBNull(reader.GetOrdinal("valor_correios")) ? 0 : reader.GetDecimal(reader.GetOrdinal("valor_correios")),
                    StatusCorreios = reader.IsDBNull(reader.GetOrdinal("status_correios")) ? "Pendente" : reader.GetString(reader.GetOrdinal("status_correios")),
                    DescricaoStatus = reader.IsDBNull(reader.GetOrdinal("descricao_status")) ? "" : reader.GetString(reader.GetOrdinal("descricao_status")),
                    UltimoEventoData = reader.IsDBNull(reader.GetOrdinal("ultimo_evento_data")) ? "" : reader.GetString(reader.GetOrdinal("ultimo_evento_data")),
                    UltimoEventoLocal = reader.IsDBNull(reader.GetOrdinal("ultimo_evento_local")) ? "" : reader.GetString(reader.GetOrdinal("ultimo_evento_local")),
                    Entregue = reader.GetInt32(reader.GetOrdinal("entregue")) == 1,
                    Observacoes = reader.IsDBNull(reader.GetOrdinal("observacoes")) ? "" : reader.GetString(reader.GetOrdinal("observacoes")),
                    CaminhoPdf = reader.IsDBNull(reader.GetOrdinal("caminho_pdf")) ? "" : reader.GetString(reader.GetOrdinal("caminho_pdf")),
                    DesativarAvisos = !reader.IsDBNull(reader.GetOrdinal("desativar_avisos")) && Convert.ToInt32(reader["desativar_avisos"]) == 1,
                    Arquivado = !reader.IsDBNull(reader.GetOrdinal("arquivado")) && Convert.ToInt32(reader["arquivado"]) == 1,
                    DataEntrega = reader.IsDBNull(reader.GetOrdinal("data_entrega")) ? "" : reader.GetString(reader.GetOrdinal("data_entrega")),
                    DataArquivamento = reader.IsDBNull(reader.GetOrdinal("data_arquivamento")) ? "" : reader.GetString(reader.GetOrdinal("data_arquivamento")),
                    ResumoProdutos = reader.IsDBNull(reader.GetOrdinal("resumo_produtos")) ? "" : reader.GetString(reader.GetOrdinal("resumo_produtos")),
                    TentativasEntregaCount = reader.IsDBNull(reader.GetOrdinal("tentativas_nao_atendidas")) ? 0 : Convert.ToInt32(reader["tentativas_nao_atendidas"])
                };

                // Fallback: se o status indica carteiro não atendido mas não contabilizou evento no histórico
                if (env.TentativasEntregaCount == 0 && !env.Entregue)
                {
                    if (env.StatusCorreios.Contains("Não Atendido", StringComparison.OrdinalIgnoreCase) ||
                        env.DescricaoStatus.Contains("não atendido", StringComparison.OrdinalIgnoreCase) ||
                        env.DescricaoStatus.Contains("ausente", StringComparison.OrdinalIgnoreCase))
                    {
                        env.TentativasEntregaCount = 1;
                    }
                }

                lista.Add(env);
            }

            return lista;
        }

        public static Envio? ObterEnvioCompleto(int envioId)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT e.*, f.nome as fornecedor_nome, f.apelido as fornecedor_apelido
                FROM envios e
                LEFT JOIN fornecedores f ON e.fornecedor_id = f.id
                WHERE e.id = @id";
            cmd.Parameters.AddWithValue("@id", envioId);

            Envio? envio = null;
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    envio = new Envio
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        NumeroPedido = reader.GetString(reader.GetOrdinal("numero_pedido")),
                        Cliente = reader.GetString(reader.GetOrdinal("cliente")),
                        ClienteCpf = reader.IsDBNull(reader.GetOrdinal("cliente_cpf")) ? "" : reader.GetString(reader.GetOrdinal("cliente_cpf")),
                        ClienteCelular = reader.IsDBNull(reader.GetOrdinal("cliente_celular")) ? "" : reader.GetString(reader.GetOrdinal("cliente_celular")),
                        ClienteEmail = reader.IsDBNull(reader.GetOrdinal("cliente_email")) ? "" : reader.GetString(reader.GetOrdinal("cliente_email")),
                        EnderecoEntrega = reader.IsDBNull(reader.GetOrdinal("endereco_entrega")) ? "" : reader.GetString(reader.GetOrdinal("endereco_entrega")),
                        ComplementoEntrega = reader.IsDBNull(reader.GetOrdinal("complemento_entrega")) ? "" : reader.GetString(reader.GetOrdinal("complemento_entrega")),
                        CidadeDestino = reader.IsDBNull(reader.GetOrdinal("cidade_destino")) ? "" : reader.GetString(reader.GetOrdinal("cidade_destino")),
                        UfDestino = reader.IsDBNull(reader.GetOrdinal("uf_destino")) ? "" : reader.GetString(reader.GetOrdinal("uf_destino")),
                        CepDestino = reader.IsDBNull(reader.GetOrdinal("cep_destino")) ? "" : reader.GetString(reader.GetOrdinal("cep_destino")),
                        FormaEnvioLoja = reader.IsDBNull(reader.GetOrdinal("forma_envio_loja")) ? "" : reader.GetString(reader.GetOrdinal("forma_envio_loja")),
                        PesoGramas = reader.IsDBNull(reader.GetOrdinal("peso_gramas")) ? 0 : reader.GetDouble(reader.GetOrdinal("peso_gramas")),
                        ValorPedido = reader.IsDBNull(reader.GetOrdinal("valor_pedido")) ? 0 : reader.GetDecimal(reader.GetOrdinal("valor_pedido")),
                        ValorFreteLoja = reader.IsDBNull(reader.GetOrdinal("valor_frete_loja")) ? 0 : reader.GetDecimal(reader.GetOrdinal("valor_frete_loja")),
                        OrigemTipo = reader.IsDBNull(reader.GetOrdinal("origem_tipo")) ? "PROPRIO" : reader.GetString(reader.GetOrdinal("origem_tipo")),
                        FornecedorId = reader.IsDBNull(reader.GetOrdinal("fornecedor_id")) ? null : reader.GetInt32(reader.GetOrdinal("fornecedor_id")),
                        FornecedorNome = reader.IsDBNull(reader.GetOrdinal("fornecedor_nome")) ? "" : reader.GetString(reader.GetOrdinal("fornecedor_nome")),
                        FornecedorApelido = reader.IsDBNull(reader.GetOrdinal("fornecedor_apelido")) ? "" : reader.GetString(reader.GetOrdinal("fornecedor_apelido")),
                        NotaFiscal = reader.IsDBNull(reader.GetOrdinal("nota_fiscal")) ? "" : reader.GetString(reader.GetOrdinal("nota_fiscal")),
                        CodigoRastreio = reader.IsDBNull(reader.GetOrdinal("codigo_rastreio")) ? "" : reader.GetString(reader.GetOrdinal("codigo_rastreio")),
                        TipoRastreio = reader.IsDBNull(reader.GetOrdinal("tipo_rastreio")) ? "NORMAL" : reader.GetString(reader.GetOrdinal("tipo_rastreio")),
                        DataEnvio = reader.IsDBNull(reader.GetOrdinal("data_envio")) ? "" : reader.GetString(reader.GetOrdinal("data_envio")),
                        ValorCorreios = reader.IsDBNull(reader.GetOrdinal("valor_correios")) ? 0 : reader.GetDecimal(reader.GetOrdinal("valor_correios")),
                        StatusCorreios = reader.IsDBNull(reader.GetOrdinal("status_correios")) ? "Pendente" : reader.GetString(reader.GetOrdinal("status_correios")),
                        DescricaoStatus = reader.IsDBNull(reader.GetOrdinal("descricao_status")) ? "" : reader.GetString(reader.GetOrdinal("descricao_status")),
                        UltimoEventoData = reader.IsDBNull(reader.GetOrdinal("ultimo_evento_data")) ? "" : reader.GetString(reader.GetOrdinal("ultimo_evento_data")),
                        UltimoEventoLocal = reader.IsDBNull(reader.GetOrdinal("ultimo_evento_local")) ? "" : reader.GetString(reader.GetOrdinal("ultimo_evento_local")),
                        Entregue = reader.GetInt32(reader.GetOrdinal("entregue")) == 1,
                        Observacoes = reader.IsDBNull(reader.GetOrdinal("observacoes")) ? "" : reader.GetString(reader.GetOrdinal("observacoes")),
                        CaminhoPdf = reader.IsDBNull(reader.GetOrdinal("caminho_pdf")) ? "" : reader.GetString(reader.GetOrdinal("caminho_pdf"))
                    };
                }
            }

            if (envio == null) return null;

            // Carrega itens
            using (var cmdItens = conn.CreateCommand())
            {
                cmdItens.CommandText = "SELECT * FROM itens_envio WHERE envio_id = @id";
                cmdItens.Parameters.AddWithValue("@id", envioId);
                using var rItens = cmdItens.ExecuteReader();
                while (rItens.Read())
                {
                    envio.Itens.Add(new ItemEnvio
                    {
                        Id = rItens.GetInt32(rItens.GetOrdinal("id")),
                        EnvioId = envioId,
                        NomeProduto = rItens.GetString(rItens.GetOrdinal("nome_produto")),
                        Referencia = rItens.IsDBNull(rItens.GetOrdinal("referencia")) ? "" : rItens.GetString(rItens.GetOrdinal("referencia")),
                        Codigo = rItens.IsDBNull(rItens.GetOrdinal("codigo")) ? "" : rItens.GetString(rItens.GetOrdinal("codigo")),
                        Tamanho = rItens.IsDBNull(rItens.GetOrdinal("tamanho")) ? "" : rItens.GetString(rItens.GetOrdinal("tamanho")),
                        Quantidade = rItens.GetInt32(rItens.GetOrdinal("quantidade")),
                        ValorUnitario = rItens.GetDecimal(rItens.GetOrdinal("valor_unitario")),
                        ValorTotal = rItens.GetDecimal(rItens.GetOrdinal("valor_total"))
                    });
                }
            }

            // Carrega histórico
            using (var cmdHist = conn.CreateCommand())
            {
                cmdHist.CommandText = "SELECT * FROM historico_rastreio WHERE envio_id = @id ORDER BY id DESC";
                cmdHist.Parameters.AddWithValue("@id", envioId);
                using var rHist = cmdHist.ExecuteReader();
                while (rHist.Read())
                {
                    envio.Historico.Add(new EventoRastreio
                    {
                        Id = rHist.GetInt32(rHist.GetOrdinal("id")),
                        EnvioId = envioId,
                        CodigoRastreio = rHist.GetString(rHist.GetOrdinal("codigo_rastreio")),
                        DataHora = rHist.GetString(rHist.GetOrdinal("data_hora")),
                        Descricao = rHist.GetString(rHist.GetOrdinal("descricao")),
                        Detalhe = rHist.IsDBNull(rHist.GetOrdinal("detalhe")) ? "" : rHist.GetString(rHist.GetOrdinal("detalhe")),
                        UnidadeLocal = rHist.IsDBNull(rHist.GetOrdinal("unidade_local")) ? "" : rHist.GetString(rHist.GetOrdinal("unidade_local")),
                        UnidadeDestino = rHist.IsDBNull(rHist.GetOrdinal("unidade_destino")) ? "" : rHist.GetString(rHist.GetOrdinal("unidade_destino")),
                        TipoEvento = rHist.IsDBNull(rHist.GetOrdinal("tipo_evento")) ? "" : rHist.GetString(rHist.GetOrdinal("tipo_evento"))
                    });
                }
            }

            // Calcula contador de tentativas não atendidas
            envio.TentativasEntregaCount = envio.Historico.Count(h =>
            {
                string d = (h.Descricao + " " + h.Detalhe).ToLowerInvariant();
                return d.Contains("não atendido") || d.Contains("não entregue") || d.Contains("ausente") || d.Contains("nova tentativa");
            });
            if (envio.TentativasEntregaCount == 0 && !envio.Entregue)
            {
                if (envio.StatusCorreios.Contains("Não Atendido", StringComparison.OrdinalIgnoreCase) ||
                    envio.DescricaoStatus.Contains("não atendido", StringComparison.OrdinalIgnoreCase) ||
                    envio.DescricaoStatus.Contains("ausente", StringComparison.OrdinalIgnoreCase))
                {
                    envio.TentativasEntregaCount = 1;
                }
            }

            return envio;
        }

        public static int SalvarEnvio(Envio e)
        {
            using var conn = DatabaseContext.GetConnection();
            using var transaction = conn.BeginTransaction();
            var agora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            int finalId;

            if (e.Id > 0)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    UPDATE envios SET
                        numero_pedido = @ped, cliente = @cli, cliente_cpf = @cpf, cliente_celular = @cel,
                        cliente_email = @email, endereco_entrega = @end, complemento_entrega = @comp, cidade_destino = @cid,
                        uf_destino = @uf, cep_destino = @cep, forma_envio_loja = @forma,
                        peso_gramas = @peso, valor_pedido = @vped, valor_frete_loja = @vfrete,
                        origem_tipo = @origem, fornecedor_id = @fornid, nota_fiscal = @nf,
                        codigo_rastreio = @rast, tipo_rastreio = @trast, data_envio = @denv,
                        valor_correios = @vcor, observacoes = @obs, caminho_pdf = @cpdf,
                        desativar_avisos = @desat, arquivado = @arq, updated_at = @upd
                    WHERE id = @id";

                cmd.Parameters.AddWithValue("@id", e.Id);
                cmd.Parameters.AddWithValue("@ped", e.NumeroPedido);
                cmd.Parameters.AddWithValue("@cli", e.Cliente);
                cmd.Parameters.AddWithValue("@cpf", e.ClienteCpf ?? "");
                cmd.Parameters.AddWithValue("@cel", e.ClienteCelular ?? "");
                cmd.Parameters.AddWithValue("@email", e.ClienteEmail ?? "");
                cmd.Parameters.AddWithValue("@end", e.EnderecoEntrega ?? "");
                cmd.Parameters.AddWithValue("@comp", e.ComplementoEntrega ?? "");
                cmd.Parameters.AddWithValue("@cid", e.CidadeDestino ?? "");
                cmd.Parameters.AddWithValue("@uf", e.UfDestino ?? "");
                cmd.Parameters.AddWithValue("@cep", e.CepDestino ?? "");
                cmd.Parameters.AddWithValue("@forma", e.FormaEnvioLoja ?? "");
                cmd.Parameters.AddWithValue("@peso", e.PesoGramas);
                cmd.Parameters.AddWithValue("@vped", e.ValorPedido);
                cmd.Parameters.AddWithValue("@vfrete", e.ValorFreteLoja);
                cmd.Parameters.AddWithValue("@origem", e.OrigemTipo);
                cmd.Parameters.AddWithValue("@fornid", e.OrigemTipo == "DROPSHIPPING" && e.FornecedorId.HasValue ? (object)e.FornecedorId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@nf", e.NotaFiscal ?? "");
                cmd.Parameters.AddWithValue("@rast", (e.CodigoRastreio ?? "").Trim().ToUpperInvariant());
                cmd.Parameters.AddWithValue("@trast", e.TipoRastreio ?? "NORMAL");
                cmd.Parameters.AddWithValue("@denv", e.DataEnvio ?? "");
                cmd.Parameters.AddWithValue("@vcor", e.ValorCorreios);
                cmd.Parameters.AddWithValue("@obs", e.Observacoes ?? "");
                cmd.Parameters.AddWithValue("@cpdf", e.CaminhoPdf ?? "");
                cmd.Parameters.AddWithValue("@desat", e.DesativarAvisos ? 1 : 0);
                cmd.Parameters.AddWithValue("@arq", e.Arquivado ? 1 : 0);
                cmd.Parameters.AddWithValue("@upd", agora);
                cmd.ExecuteNonQuery();

                finalId = e.Id;

                // Remove itens antigos para reinserir atualizados
                using var cmdDel = conn.CreateCommand();
                cmdDel.Transaction = transaction;
                cmdDel.CommandText = "DELETE FROM itens_envio WHERE envio_id = @id";
                cmdDel.Parameters.AddWithValue("@id", finalId);
                cmdDel.ExecuteNonQuery();
            }
            else
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO envios (
                        numero_pedido, cliente, cliente_cpf, cliente_celular, cliente_email,
                        endereco_entrega, complemento_entrega, cidade_destino, uf_destino, cep_destino, forma_envio_loja,
                        peso_gramas, valor_pedido, valor_frete_loja, origem_tipo, fornecedor_id,
                        nota_fiscal, codigo_rastreio, tipo_rastreio, data_envio, valor_correios,
                        status_correios, observacoes, caminho_pdf, desativar_avisos, arquivado, created_at, updated_at
                    ) VALUES (
                        @ped, @cli, @cpf, @cel, @email,
                        @end, @comp, @cid, @uf, @cep, @forma,
                        @peso, @vped, @vfrete, @origem, @fornid,
                        @nf, @rast, @trast, @denv, @vcor,
                        @st, @obs, @cpdf, @desat, @arq, @cre, @upd
                    );
                    SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("@ped", e.NumeroPedido);
                cmd.Parameters.AddWithValue("@cli", e.Cliente);
                cmd.Parameters.AddWithValue("@cpf", e.ClienteCpf ?? "");
                cmd.Parameters.AddWithValue("@cel", e.ClienteCelular ?? "");
                cmd.Parameters.AddWithValue("@email", e.ClienteEmail ?? "");
                cmd.Parameters.AddWithValue("@end", e.EnderecoEntrega ?? "");
                cmd.Parameters.AddWithValue("@comp", e.ComplementoEntrega ?? "");
                cmd.Parameters.AddWithValue("@cid", e.CidadeDestino ?? "");
                cmd.Parameters.AddWithValue("@uf", e.UfDestino ?? "");
                cmd.Parameters.AddWithValue("@cep", e.CepDestino ?? "");
                cmd.Parameters.AddWithValue("@forma", e.FormaEnvioLoja ?? "");
                cmd.Parameters.AddWithValue("@peso", e.PesoGramas);
                cmd.Parameters.AddWithValue("@vped", e.ValorPedido);
                cmd.Parameters.AddWithValue("@vfrete", e.ValorFreteLoja);
                cmd.Parameters.AddWithValue("@origem", e.OrigemTipo);
                cmd.Parameters.AddWithValue("@fornid", e.OrigemTipo == "DROPSHIPPING" && e.FornecedorId.HasValue ? (object)e.FornecedorId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@nf", e.NotaFiscal ?? "");
                cmd.Parameters.AddWithValue("@rast", (e.CodigoRastreio ?? "").Trim().ToUpperInvariant());
                cmd.Parameters.AddWithValue("@trast", e.TipoRastreio ?? "NORMAL");
                cmd.Parameters.AddWithValue("@denv", e.DataEnvio ?? "");
                cmd.Parameters.AddWithValue("@vcor", e.ValorCorreios);
                cmd.Parameters.AddWithValue("@st", e.TipoRastreio == "PRE_POSTAGEM" ? "Pré-postagem" : "Pendente");
                cmd.Parameters.AddWithValue("@obs", e.Observacoes ?? "");
                cmd.Parameters.AddWithValue("@cpdf", e.CaminhoPdf ?? "");
                cmd.Parameters.AddWithValue("@desat", e.DesativarAvisos ? 1 : 0);
                cmd.Parameters.AddWithValue("@arq", e.Arquivado ? 1 : 0);
                cmd.Parameters.AddWithValue("@cre", agora);
                cmd.Parameters.AddWithValue("@upd", agora);

                finalId = Convert.ToInt32(cmd.ExecuteScalar());
                e.Id = finalId;
            }

            // Insere itens
            foreach (var item in e.Itens)
            {
                using var cmdItem = conn.CreateCommand();
                cmdItem.Transaction = transaction;
                cmdItem.CommandText = @"
                    INSERT INTO itens_envio (
                        envio_id, nome_produto, referencia, codigo, tamanho,
                        quantidade, valor_unitario, valor_total
                    ) VALUES (
                        @eid, @nome, @ref, @cod, @tam, @qtd, @vu, @vt
                    )";
                cmdItem.Parameters.AddWithValue("@eid", finalId);
                cmdItem.Parameters.AddWithValue("@nome", item.NomeProduto);
                cmdItem.Parameters.AddWithValue("@ref", item.Referencia ?? "");
                cmdItem.Parameters.AddWithValue("@cod", item.Codigo ?? "");
                cmdItem.Parameters.AddWithValue("@tam", item.Tamanho ?? "");
                cmdItem.Parameters.AddWithValue("@qtd", item.Quantidade > 0 ? item.Quantidade : 1);
                cmdItem.Parameters.AddWithValue("@vu", item.ValorUnitario);
                cmdItem.Parameters.AddWithValue("@vt", item.ValorTotal);
                cmdItem.ExecuteNonQuery();
            }

            transaction.Commit();
            return finalId;
        }

        public static void ExcluirEnvio(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM itens_envio WHERE envio_id = @id;
                DELETE FROM historico_rastreio WHERE envio_id = @id;
                DELETE FROM envios WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static bool AtualizarRastreio(int envioId, ResultadoRastreio res)
        {
            if (!res.Sucesso) return false;

            using var conn = DatabaseContext.GetConnection();
            using var cmdCheck = conn.CreateCommand();
            cmdCheck.CommandText = "SELECT status_correios FROM envios WHERE id = @id";
            cmdCheck.Parameters.AddWithValue("@id", envioId);
            var objStatus = cmdCheck.ExecuteScalar();
            if (objStatus == null) return false;

            string statusAntigo = objStatus.ToString() ?? "";
            bool mudou = !string.Equals(statusAntigo, res.Status, StringComparison.OrdinalIgnoreCase);

            using var cmdUpd = conn.CreateCommand();
            cmdUpd.CommandText = @"
                UPDATE envios SET
                    status_correios = @st,
                    descricao_status = @desc,
                    ultimo_evento_data = @dt,
                    ultimo_evento_local = @loc,
                    entregue = @ent,
                    data_entrega = CASE 
                        WHEN @ent = 1 AND (data_entrega IS NULL OR data_entrega = '') THEN 
                            CASE WHEN @dt != '' THEN @dt ELSE @upd END
                        ELSE data_entrega 
                    END,
                    updated_at = @upd
                WHERE id = @id";

            cmdUpd.Parameters.AddWithValue("@id", envioId);
            cmdUpd.Parameters.AddWithValue("@st", res.Status);
            cmdUpd.Parameters.AddWithValue("@desc", res.Descricao ?? "");
            cmdUpd.Parameters.AddWithValue("@dt", res.UltimoEventoData ?? "");
            cmdUpd.Parameters.AddWithValue("@loc", res.UltimoEventoLocal ?? "");
            cmdUpd.Parameters.AddWithValue("@ent", res.Entregue ? 1 : 0);
            cmdUpd.Parameters.AddWithValue("@upd", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmdUpd.ExecuteNonQuery();

            if (res.Eventos != null && res.Eventos.Count > 0)
            {
                using var cmdDel = conn.CreateCommand();
                cmdDel.CommandText = "DELETE FROM historico_rastreio WHERE envio_id = @id";
                cmdDel.Parameters.AddWithValue("@id", envioId);
                cmdDel.ExecuteNonQuery();

                foreach (var ev in res.Eventos)
                {
                    using var cmdEv = conn.CreateCommand();
                    cmdEv.CommandText = @"
                        INSERT INTO historico_rastreio (
                            envio_id, codigo_rastreio, data_hora, descricao,
                            detalhe, unidade_local, unidade_destino, tipo_evento
                        ) VALUES (
                            @eid, @cod, @dt, @desc, @det, @loc, @dest, @tp
                        )";
                    cmdEv.Parameters.AddWithValue("@eid", envioId);
                    cmdEv.Parameters.AddWithValue("@cod", res.Codigo);
                    cmdEv.Parameters.AddWithValue("@dt", ev.DataHora);
                    cmdEv.Parameters.AddWithValue("@desc", ev.Descricao);
                    cmdEv.Parameters.AddWithValue("@det", ev.Detalhe ?? "");
                    cmdEv.Parameters.AddWithValue("@loc", ev.UnidadeLocal ?? "");
                    cmdEv.Parameters.AddWithValue("@dest", ev.UnidadeDestino ?? "");
                    cmdEv.Parameters.AddWithValue("@tp", ev.TipoEvento ?? "");
                    cmdEv.ExecuteNonQuery();
                }
            }

            return mudou;
        }

        public static Metricas ObterMetricas()
        {
            var m = new Metricas();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT COUNT(*) FROM envios WHERE (arquivado IS NULL OR arquivado = 0)";
            m.Total = Convert.ToInt32(cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM envios WHERE entregue = 1 AND (arquivado IS NULL OR arquivado = 0)";
            m.Entregues = Convert.ToInt32(cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM envios WHERE entregue = 0 AND status_correios = 'Em Trânsito' AND (arquivado IS NULL OR arquivado = 0)";
            m.EmTransito = Convert.ToInt32(cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM envios WHERE status_correios = 'Saiu para Entrega' AND (arquivado IS NULL OR arquivado = 0)";
            m.SaiuEntrega = Convert.ToInt32(cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM envios WHERE status_correios = 'Aguardando Retirada' AND (arquivado IS NULL OR arquivado = 0)";
            m.AguardandoRetirada = Convert.ToInt32(cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM envios WHERE status_correios = 'Alerta / Retido' AND (arquivado IS NULL OR arquivado = 0)";
            m.Alertas = Convert.ToInt32(cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM envios WHERE tipo_rastreio = 'PRE_POSTAGEM' AND status_correios IN ('Pendente', 'Pré-postagem') AND (arquivado IS NULL OR arquivado = 0)";
            m.PrePostagem = Convert.ToInt32(cmd.ExecuteScalar());

            cmd.CommandText = "SELECT COUNT(*) FROM envios WHERE arquivado = 1";
            m.Arquivados = Convert.ToInt32(cmd.ExecuteScalar());

            // 1. Total de Frete da Loja (calculado à parte do saldo / diferença de valor)
            cmd.CommandText = "SELECT COALESCE(SUM(valor_frete_loja), 0) FROM envios WHERE (arquivado IS NULL OR arquivado = 0);";
            var objTotalFrete = cmd.ExecuteScalar();
            m.TotalFrete = objTotalFrete != null && objTotalFrete != DBNull.Value ? Convert.ToDecimal(objTotalFrete) : 0;

            // 2. Total Gasto com Correios (apenas o que realmente foi cobrado com base nos e-mails / comprovantes)
            cmd.CommandText = "SELECT COALESCE(SUM(valor_correios), 0) FROM envios WHERE valor_correios > 0 AND (arquivado IS NULL OR arquivado = 0);";
            var objGasto = cmd.ExecuteScalar();
            m.TotalGastoCorreios = objGasto != null && objGasto != DBNull.Value ? Convert.ToDecimal(objGasto) : 0;

            // 3. Diferença de Valor (calculada à parte do total de frete)
            cmd.CommandText = @"
                SELECT COALESCE(SUM(valor_frete_loja - valor_correios), 0) 
                FROM envios 
                WHERE valor_correios > 0 
                  AND (arquivado IS NULL OR arquivado = 0)
                  AND UPPER(COALESCE(forma_envio_loja, '')) NOT LIKE '%GRATIS%' 
                  AND UPPER(COALESCE(forma_envio_loja, '')) NOT LIKE '%GRÁTIS%' 
                  AND UPPER(COALESCE(forma_envio_loja, '')) NOT LIKE '%RETIRAD%';";
            var objDiferenca = cmd.ExecuteScalar();
            m.DiferencaValorTotal = objDiferenca != null && objDiferenca != DBNull.Value ? Convert.ToDecimal(objDiferenca) : 0;

            // Encomendas com tentativas de entrega sem sucesso (não atendidas e com avisos ativos)
            cmd.CommandText = @"
                SELECT COUNT(DISTINCT e.id) FROM envios e 
                LEFT JOIN historico_rastreio h ON h.envio_id = e.id
                WHERE e.entregue = 0 
                  AND (e.arquivado IS NULL OR e.arquivado = 0)
                  AND (e.desativar_avisos IS NULL OR e.desativar_avisos = 0)
                  AND (
                    e.status_correios LIKE '%Não Atendido%' OR 
                    e.descricao_status LIKE '%não atendido%' OR 
                    e.descricao_status LIKE '%ausente%' OR 
                    LOWER(h.descricao) LIKE '%não atendido%' OR 
                    LOWER(h.descricao) LIKE '%ausente%' OR
                    LOWER(h.descricao) LIKE '%não entregue%'
                );";
            m.TentativasNaoAtendidas = Convert.ToInt32(cmd.ExecuteScalar());

            return m;
        }

        public static void AlternarArquivamento(int id, bool arquivar)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE envios SET
                    arquivado = @arq,
                    data_arquivamento = @dtArq,
                    updated_at = @upd
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@arq", arquivar ? 1 : 0);
            cmd.Parameters.AddWithValue("@dtArq", arquivar ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value);
            cmd.Parameters.AddWithValue("@upd", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static int VerificarEArquivarEntregues7Dias()
        {
            int arquivados = 0;
            try
            {
                using var conn = DatabaseContext.GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, status_correios, data_entrega, ultimo_evento_data, updated_at, created_at 
                    FROM envios 
                    WHERE entregue = 1 AND (arquivado IS NULL OR arquivado = 0)";

                var paraArquivar = new List<int>();
                using (var reader = cmd.ExecuteReader())
                {
                    var agora = DateTime.Now;
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string dtEntregaStr = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        string ultimoEvStr = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        string updStr = reader.IsDBNull(4) ? "" : reader.GetString(4);
                        string creStr = reader.IsDBNull(5) ? "" : reader.GetString(5);

                        DateTime dataBase = DateTime.MinValue;

                        if (!string.IsNullOrWhiteSpace(dtEntregaStr))
                        {
                            DateTime.TryParse(dtEntregaStr, out dataBase);
                        }

                        if (dataBase == DateTime.MinValue && !string.IsNullOrWhiteSpace(ultimoEvStr))
                        {
                            if (!DateTime.TryParse(ultimoEvStr, out dataBase))
                            {
                                var match = Regex.Match(ultimoEvStr, @"(\d{2})/(\d{2})/(\d{4})");
                                if (match.Success)
                                {
                                    DateTime.TryParseExact(match.Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dataBase);
                                }
                            }
                        }

                        if (dataBase == DateTime.MinValue && !string.IsNullOrWhiteSpace(updStr))
                        {
                            DateTime.TryParse(updStr, out dataBase);
                        }

                        if (dataBase == DateTime.MinValue && !string.IsNullOrWhiteSpace(creStr))
                        {
                            DateTime.TryParse(creStr, out dataBase);
                        }

                        if (dataBase != DateTime.MinValue && (agora - dataBase).TotalDays >= 7)
                        {
                            paraArquivar.Add(id);
                        }
                    }
                }

                if (paraArquivar.Count > 0)
                {
                    using var cmdArq = conn.CreateCommand();
                    cmdArq.CommandText = $@"
                        UPDATE envios SET 
                            arquivado = 1, 
                            data_arquivamento = @dtArq,
                            updated_at = @dtArq
                        WHERE id IN ({string.Join(",", paraArquivar)})";
                    cmdArq.Parameters.AddWithValue("@dtArq", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    arquivados = cmdArq.ExecuteNonQuery();
                }
            }
            catch { }
            return arquivados;
        }

        public static void CorrigirInconsistenciasStatus()
        {
            try
            {
                using var conn = DatabaseContext.GetConnection();
                using var cmd = conn.CreateCommand();
                // Corrige envios marcados erroneamente como 'Entregue' quando o último evento foi carteiro não atendido
                cmd.CommandText = @"
                    UPDATE envios SET 
                        entregue = 0, 
                        status_correios = 'Carteiro Não Atendido'
                    WHERE entregue = 1 AND (
                        LOWER(status_correios) LIKE '%não atendido%' OR 
                        LOWER(descricao_status) LIKE '%não atendido%' OR 
                        LOWER(descricao_status) LIKE '%carteiro não atendido%' OR
                        LOWER(descricao_status) LIKE '%destinatário ausente%' OR
                        id IN (
                            SELECT envio_id FROM historico_rastreio 
                            WHERE (LOWER(descricao) LIKE '%carteiro não atendido%' OR LOWER(descricao) LIKE '%não entregue%' OR LOWER(descricao) LIKE '%ausente%')
                            AND id = (SELECT MAX(id) FROM historico_rastreio h2 WHERE h2.envio_id = historico_rastreio.envio_id)
                        )
                    );";
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static bool AtualizarDadosColeta(string numeroPedido, string codigoRastreio, decimal valorCorreios)
        {
            if (string.IsNullOrWhiteSpace(numeroPedido) && string.IsNullOrWhiteSpace(codigoRastreio)) return false;

            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            if (!string.IsNullOrWhiteSpace(numeroPedido))
            {
                cmd.CommandText = @"
                    UPDATE envios SET
                        codigo_rastreio = CASE WHEN (codigo_rastreio IS NULL OR codigo_rastreio = '') AND @rast <> '' THEN @rast ELSE codigo_rastreio END,
                        valor_correios = CASE WHEN @vcor > 0 THEN @vcor ELSE valor_correios END,
                        updated_at = @upd
                    WHERE numero_pedido = @ped OR codigo_rastreio = @rast";
                cmd.Parameters.AddWithValue("@ped", numeroPedido.Trim());
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE envios SET
                        valor_correios = CASE WHEN @vcor > 0 THEN @vcor ELSE valor_correios END,
                        updated_at = @upd
                    WHERE codigo_rastreio = @rast";
            }

            cmd.Parameters.AddWithValue("@rast", (codigoRastreio ?? "").Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@vcor", valorCorreios);
            cmd.Parameters.AddWithValue("@upd", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            int rows = cmd.ExecuteNonQuery();
            return rows > 0;
        }

        public static (bool atualizado, bool inserido, string? numeroPedido) AtualizarDadosColetaAvancado(
            string? codigoRastreio,
            string? destinatario,
            string? cep,
            string? uf,
            string? notaFiscal,
            decimal valorCorreios,
            string? dataPostagem = null)
        {
            string rastreio = (codigoRastreio ?? "").Trim().ToUpperInvariant();
            string dest = (destinatario ?? "").Trim();
            string cepLimpo = Regex.Replace(cep ?? "", @"\D", "");
            string nf = (notaFiscal ?? "").Trim();
            string agora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            using var conn = DatabaseContext.GetConnection();

            // 1. Tenta correspondência exata por código de rastreio (se não vazio)
            if (!string.IsNullOrWhiteSpace(rastreio))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT id, numero_pedido FROM envios WHERE UPPER(codigo_rastreio) = @rast LIMIT 1";
                cmd.Parameters.AddWithValue("@rast", rastreio);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string ped = reader.GetString(1);
                    reader.Close();

                    using var cmdUpd = conn.CreateCommand();
                    cmdUpd.CommandText = @"
                        UPDATE envios SET
                            valor_correios = CASE WHEN @vcor > 0 THEN @vcor ELSE valor_correios END,
                            data_envio = CASE WHEN (data_envio IS NULL OR data_envio = '') AND @dt <> '' THEN @dt ELSE data_envio END,
                            updated_at = @upd
                        WHERE id = @id";
                    cmdUpd.Parameters.AddWithValue("@id", id);
                    cmdUpd.Parameters.AddWithValue("@vcor", valorCorreios);
                    cmdUpd.Parameters.AddWithValue("@dt", dataPostagem ?? "");
                    cmdUpd.Parameters.AddWithValue("@upd", agora);
                    cmdUpd.ExecuteNonQuery();

                    return (true, false, ped);
                }
            }

            // 2. Tenta correspondência por Nota Fiscal (se preenchida)
            if (!string.IsNullOrWhiteSpace(nf))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT id, numero_pedido FROM envios WHERE nota_fiscal = @nf LIMIT 1";
                cmd.Parameters.AddWithValue("@nf", nf);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string ped = reader.GetString(1);
                    reader.Close();

                    using var cmdUpd = conn.CreateCommand();
                    cmdUpd.CommandText = @"
                        UPDATE envios SET
                            codigo_rastreio = CASE WHEN (codigo_rastreio IS NULL OR codigo_rastreio = '') AND @rast <> '' THEN @rast ELSE codigo_rastreio END,
                            valor_correios = CASE WHEN @vcor > 0 THEN @vcor ELSE valor_correios END,
                            data_envio = CASE WHEN (data_envio IS NULL OR data_envio = '') AND @dt <> '' THEN @dt ELSE data_envio END,
                            updated_at = @upd
                        WHERE id = @id";
                    cmdUpd.Parameters.AddWithValue("@id", id);
                    cmdUpd.Parameters.AddWithValue("@rast", rastreio);
                    cmdUpd.Parameters.AddWithValue("@vcor", valorCorreios);
                    cmdUpd.Parameters.AddWithValue("@dt", dataPostagem ?? "");
                    cmdUpd.Parameters.AddWithValue("@upd", agora);
                    cmdUpd.ExecuteNonQuery();

                    return (true, false, ped);
                }
            }

            // 3. Tenta correspondência por CEP + Nome do Destinatário
            if (cepLimpo.Length >= 5 && !string.IsNullOrWhiteSpace(dest))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, numero_pedido, cliente
                    FROM envios
                    WHERE REPLACE(REPLACE(REPLACE(cep_destino, '-', ''), ' ', ''), '.', '') = @cep";
                cmd.Parameters.AddWithValue("@cep", cepLimpo);

                int matchedId = 0;
                string matchedPed = "";
                string destNorm = RemoverAcentos(dest).ToUpperInvariant();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string ped = reader.GetString(1);
                        string cli = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        string cliNorm = RemoverAcentos(cli).ToUpperInvariant();

                        if (cliNorm.Contains(destNorm) || destNorm.Contains(cliNorm) || NomesCombinam(cliNorm, destNorm))
                        {
                            matchedId = id;
                            matchedPed = ped;
                            break;
                        }
                    }
                }

                if (matchedId > 0)
                {
                    using var cmdUpd = conn.CreateCommand();
                    cmdUpd.CommandText = @"
                        UPDATE envios SET
                            codigo_rastreio = CASE WHEN (codigo_rastreio IS NULL OR codigo_rastreio = '') AND @rast <> '' THEN @rast ELSE codigo_rastreio END,
                            valor_correios = CASE WHEN @vcor > 0 THEN @vcor ELSE valor_correios END,
                            data_envio = CASE WHEN (data_envio IS NULL OR data_envio = '') AND @dt <> '' THEN @dt ELSE data_envio END,
                            updated_at = @upd
                        WHERE id = @id";
                    cmdUpd.Parameters.AddWithValue("@id", matchedId);
                    cmdUpd.Parameters.AddWithValue("@rast", rastreio);
                    cmdUpd.Parameters.AddWithValue("@vcor", valorCorreios);
                    cmdUpd.Parameters.AddWithValue("@dt", dataPostagem ?? "");
                    cmdUpd.Parameters.AddWithValue("@upd", agora);
                    cmdUpd.ExecuteNonQuery();

                    return (true, false, matchedPed);
                }
            }

            // 4. Tenta correspondência por Nome do Cliente (para os que estão sem valor de coleta)
            if (!string.IsNullOrWhiteSpace(dest) && dest.Length >= 4)
            {
                string destNorm = RemoverAcentos(dest).ToUpperInvariant();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, numero_pedido, cliente
                    FROM envios
                    WHERE (codigo_rastreio IS NULL OR codigo_rastreio = '' OR valor_correios = 0)
                    ORDER BY id DESC LIMIT 50";

                int matchedId = 0;
                string matchedPed = "";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string ped = reader.GetString(1);
                        string cli = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        string cliNorm = RemoverAcentos(cli).ToUpperInvariant();

                        if (cliNorm == destNorm || cliNorm.StartsWith(destNorm) || destNorm.StartsWith(cliNorm) || NomesCombinam(cliNorm, destNorm))
                        {
                            matchedId = id;
                            matchedPed = ped;
                            break;
                        }
                    }
                }

                if (matchedId > 0)
                {
                    using var cmdUpd = conn.CreateCommand();
                    cmdUpd.CommandText = @"
                        UPDATE envios SET
                            codigo_rastreio = CASE WHEN (codigo_rastreio IS NULL OR codigo_rastreio = '') AND @rast <> '' THEN @rast ELSE codigo_rastreio END,
                            valor_correios = CASE WHEN @vcor > 0 THEN @vcor ELSE valor_correios END,
                            data_envio = CASE WHEN (data_envio IS NULL OR data_envio = '') AND @dt <> '' THEN @dt ELSE data_envio END,
                            updated_at = @upd
                        WHERE id = @id";
                    cmdUpd.Parameters.AddWithValue("@id", matchedId);
                    cmdUpd.Parameters.AddWithValue("@rast", rastreio);
                    cmdUpd.Parameters.AddWithValue("@vcor", valorCorreios);
                    cmdUpd.Parameters.AddWithValue("@dt", dataPostagem ?? "");
                    cmdUpd.Parameters.AddWithValue("@upd", agora);
                    cmdUpd.ExecuteNonQuery();

                    return (true, false, matchedPed);
                }
            }

            // 5. Se não encontrou correspondência e possui rastreio válido dos Correios, cadastra como novo envio
            if (!string.IsNullOrWhiteSpace(rastreio) && Regex.IsMatch(rastreio, @"^[A-Z]{2}\d{9}[A-Z]{2}$"))
            {
                string novoPedido = $"CORR-{rastreio}";
                using var cmdIns = conn.CreateCommand();
                cmdIns.CommandText = @"
                    INSERT INTO envios (
                        numero_pedido, cliente, cep_destino, uf_destino,
                        codigo_rastreio, valor_correios, valor_frete_loja,
                        status_correios, origem_tipo, data_envio, observacoes, created_at, updated_at
                    ) VALUES (
                        @ped, @cli, @cep, @uf,
                        @rast, @vcor, 0,
                        'Pendente', 'PROPRIO', @dt, @obs, @agora, @agora
                    )";
                cmdIns.Parameters.AddWithValue("@ped", novoPedido);
                cmdIns.Parameters.AddWithValue("@cli", !string.IsNullOrWhiteSpace(dest) ? dest : "Destinatário Correios");
                cmdIns.Parameters.AddWithValue("@cep", cep ?? "");
                cmdIns.Parameters.AddWithValue("@uf", uf ?? "");
                cmdIns.Parameters.AddWithValue("@rast", rastreio);
                cmdIns.Parameters.AddWithValue("@vcor", valorCorreios);
                cmdIns.Parameters.AddWithValue("@dt", dataPostagem ?? "");
                cmdIns.Parameters.AddWithValue("@obs", $"⚠️ Cobrado nos Correios ({valorCorreios:C2}) via Gmail/Coleta. PDF da Tray NÃO extraído.");
                cmdIns.Parameters.AddWithValue("@agora", agora);
                cmdIns.ExecuteNonQuery();

                return (false, true, novoPedido);
            }

            return (false, false, null);
        }

        public static string RemoverAcentos(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            string normalizado = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool NomesCombinam(string nome1, string nome2)
        {
            var partes1 = nome1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var partes2 = nome2.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes1.Length >= 2 && partes2.Length >= 2)
            {
                return partes1[0] == partes2[0] && partes1[^1] == partes2[^1];
            }
            if (partes1.Length >= 1 && partes2.Length >= 1)
            {
                return partes1[0] == partes2[0];
            }
            return false;
        }
    }
}
