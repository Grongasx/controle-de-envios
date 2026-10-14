using System;
using System.Collections.Generic;
using ControleEnvios.Data;
using ControleEnvios.Models;
using Microsoft.Data.Sqlite;

namespace ControleEnvios.Services
{
    public static class FornecedorService
    {
        public static List<Fornecedor> ListarFornecedores(bool somenteAtivos = true)
        {
            var lista = new List<Fornecedor>();
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT * FROM fornecedores WHERE 1=1 " + 
                             (somenteAtivos ? "AND ativo = 1 " : "") + 
                             "ORDER BY nome ASC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Fornecedor
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Nome = reader.GetString(reader.GetOrdinal("nome")),
                    Apelido = reader.IsDBNull(reader.GetOrdinal("apelido")) ? "" : reader.GetString(reader.GetOrdinal("apelido")),
                    CnpjCpf = reader.IsDBNull(reader.GetOrdinal("cnpj_cpf")) ? "" : reader.GetString(reader.GetOrdinal("cnpj_cpf")),
                    Cep = reader.IsDBNull(reader.GetOrdinal("cep")) ? "" : reader.GetString(reader.GetOrdinal("cep")),
                    Logradouro = reader.IsDBNull(reader.GetOrdinal("logradouro")) ? "" : reader.GetString(reader.GetOrdinal("logradouro")),
                    Numero = reader.IsDBNull(reader.GetOrdinal("numero")) ? "" : reader.GetString(reader.GetOrdinal("numero")),
                    Complemento = reader.IsDBNull(reader.GetOrdinal("complemento")) ? "" : reader.GetString(reader.GetOrdinal("complemento")),
                    Bairro = reader.IsDBNull(reader.GetOrdinal("bairro")) ? "" : reader.GetString(reader.GetOrdinal("bairro")),
                    Cidade = reader.IsDBNull(reader.GetOrdinal("cidade")) ? "" : reader.GetString(reader.GetOrdinal("cidade")),
                    Uf = reader.IsDBNull(reader.GetOrdinal("uf")) ? "" : reader.GetString(reader.GetOrdinal("uf")),
                    Telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? "" : reader.GetString(reader.GetOrdinal("telefone")),
                    Email = reader.IsDBNull(reader.GetOrdinal("email")) ? "" : reader.GetString(reader.GetOrdinal("email")),
                    Observacoes = reader.IsDBNull(reader.GetOrdinal("observacoes")) ? "" : reader.GetString(reader.GetOrdinal("observacoes")),
                    Ativo = reader.GetInt32(reader.GetOrdinal("ativo")) == 1
                });
            }

            return lista;
        }

        public static int Salvar(Fornecedor f)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();

            if (f.Id > 0)
            {
                cmd.CommandText = @"
                    UPDATE fornecedores SET
                        nome = @nome, apelido = @apelido, cnpj_cpf = @cnpj, cep = @cep,
                        logradouro = @logradouro, numero = @numero, complemento = @complemento,
                        bairro = @bairro, cidade = @cidade, uf = @uf, telefone = @tel,
                        email = @email, observacoes = @obs, ativo = @ativo
                    WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", f.Id);
            }
            else
            {
                cmd.CommandText = @"
                    INSERT INTO fornecedores (
                        nome, apelido, cnpj_cpf, cep, logradouro, numero, complemento,
                        bairro, cidade, uf, telefone, email, observacoes, ativo, created_at
                    ) VALUES (
                        @nome, @apelido, @cnpj, @cep, @logradouro, @numero, @complemento,
                        @bairro, @cidade, @uf, @tel, @email, @obs, @ativo, @created_at
                    );
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            cmd.Parameters.AddWithValue("@nome", f.Nome);
            cmd.Parameters.AddWithValue("@apelido", f.Apelido ?? "");
            cmd.Parameters.AddWithValue("@cnpj", f.CnpjCpf ?? "");
            cmd.Parameters.AddWithValue("@cep", f.Cep ?? "");
            cmd.Parameters.AddWithValue("@logradouro", f.Logradouro ?? "");
            cmd.Parameters.AddWithValue("@numero", f.Numero ?? "");
            cmd.Parameters.AddWithValue("@complemento", f.Complemento ?? "");
            cmd.Parameters.AddWithValue("@bairro", f.Bairro ?? "");
            cmd.Parameters.AddWithValue("@cidade", f.Cidade ?? "");
            cmd.Parameters.AddWithValue("@uf", f.Uf ?? "");
            cmd.Parameters.AddWithValue("@tel", f.Telefone ?? "");
            cmd.Parameters.AddWithValue("@email", f.Email ?? "");
            cmd.Parameters.AddWithValue("@obs", f.Observacoes ?? "");
            cmd.Parameters.AddWithValue("@ativo", f.Ativo ? 1 : 0);

            if (f.Id > 0)
            {
                cmd.ExecuteNonQuery();
                return f.Id;
            }
            else
            {
                var newId = Convert.ToInt32(cmd.ExecuteScalar());
                f.Id = newId;
                return newId;
            }
        }

        public static void Excluir(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE fornecedores SET ativo = 0 WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static Fornecedor? ObterPorId(int id)
        {
            using var conn = DatabaseContext.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM fornecedores WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Fornecedor
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Nome = reader.GetString(reader.GetOrdinal("nome")),
                    Apelido = reader.IsDBNull(reader.GetOrdinal("apelido")) ? "" : reader.GetString(reader.GetOrdinal("apelido")),
                    CnpjCpf = reader.IsDBNull(reader.GetOrdinal("cnpj_cpf")) ? "" : reader.GetString(reader.GetOrdinal("cnpj_cpf")),
                    Cep = reader.IsDBNull(reader.GetOrdinal("cep")) ? "" : reader.GetString(reader.GetOrdinal("cep")),
                    Logradouro = reader.IsDBNull(reader.GetOrdinal("logradouro")) ? "" : reader.GetString(reader.GetOrdinal("logradouro")),
                    Numero = reader.IsDBNull(reader.GetOrdinal("numero")) ? "" : reader.GetString(reader.GetOrdinal("numero")),
                    Complemento = reader.IsDBNull(reader.GetOrdinal("complemento")) ? "" : reader.GetString(reader.GetOrdinal("complemento")),
                    Bairro = reader.IsDBNull(reader.GetOrdinal("bairro")) ? "" : reader.GetString(reader.GetOrdinal("bairro")),
                    Cidade = reader.IsDBNull(reader.GetOrdinal("cidade")) ? "" : reader.GetString(reader.GetOrdinal("cidade")),
                    Uf = reader.IsDBNull(reader.GetOrdinal("uf")) ? "" : reader.GetString(reader.GetOrdinal("uf")),
                    Telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? "" : reader.GetString(reader.GetOrdinal("telefone")),
                    Email = reader.IsDBNull(reader.GetOrdinal("email")) ? "" : reader.GetString(reader.GetOrdinal("email")),
                    Observacoes = reader.IsDBNull(reader.GetOrdinal("observacoes")) ? "" : reader.GetString(reader.GetOrdinal("observacoes")),
                    Ativo = reader.GetInt32(reader.GetOrdinal("ativo")) == 1
                };
            }
            return null;
        }
    }
}
