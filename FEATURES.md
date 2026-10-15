# ✨ Features — Controle de Envios

Aplicativo desktop para Windows (**C# .NET 8 / WPF**) que centraliza o controle de pedidos enviados pelos **Correios**, integrando pedidos da loja **Tray**, comprovantes de coleta, fornecedores de **dropshipping** e rastreamento automático em segundo plano.

---

## 📑 Sumário

1. [Cadastro de Envios](#1--cadastro-de-envios)
2. [Importação de Pedidos da Tray (PDF)](#2--importação-de-pedidos-da-tray-pdf)
3. [Comprovantes de Coleta dos Correios](#3--comprovantes-de-coleta-dos-correios)
4. [Rastreamento via API Oficial dos Correios](#4--rastreamento-via-api-oficial-dos-correios)
5. [Monitoramento em Segundo Plano e Notificações](#5--monitoramento-em-segundo-plano-e-notificações)
6. [Dashboard, Métricas e Filtros](#6--dashboard-métricas-e-filtros)
7. [Ações Rápidas por Envio](#7--ações-rápidas-por-envio)
8. [Fornecedores de Dropshipping](#8--fornecedores-de-dropshipping)
9. [Arquivamento Automático](#9--arquivamento-automático)
10. [Excel: Importação e Exportação](#10--excel-importação-e-exportação)
11. [Nota Fiscal](#11--nota-fiscal)
12. [Configurações](#12--configurações)
13. [Segurança e Privacidade](#13--segurança-e-privacidade)
14. [Stack Técnica](#14--stack-técnica)

---

## 1. 📦 Cadastro de Envios

- Cadastro, edição e exclusão de envios.
- Dados do pedido: nº do pedido, cliente, CPF, celular, e-mail, endereço completo, cidade/UF/CEP.
- Forma de envio da loja (SEDEX, PAC…), peso em gramas, valor do pedido e do frete.
- Lista de **produtos do pedido** (nome, referência, código, tamanho, quantidade, valores).
- Origem do envio: **Estoque Próprio** ou **Fornecedor Dropshipping**.
- Tipo de rastreio: **Normal** ou **Pré-postagem**.
- Campo de observações e vínculo com o PDF original do pedido.

## 2. 📄 Importação de Pedidos da Tray (PDF)

- **Arraste e solte** o PDF do pedido da Tray na janela de envio (ou selecione o arquivo).
- Extração automática (via PdfPig) de:
  - Nº do pedido, nome, **CPF ou CNPJ** (com validação matemática e máscara dinâmica);
  - Endereço completo de entrega (logradouro, complemento, bairro, cidade, UF, CEP);
  - Itens do pedido com tamanhos, referências e quantidades;
  - Forma de envio, peso, valor do frete e total do pedido.
- Normalização de telefones e remoção de acentos para buscas consistentes.
- **Autopreenchimento de Endereço por CEP**: consulta instantânea ao ViaCEP com fallback automático para BrasilAPI e cache em memória.

## 3. 📥 Comprovantes de Coleta dos Correios

- **Importação manual** do comprovante de coleta em **HTML** (botão *Coleta Correios*).
- **Leitura automática por e-mail (IMAP)**: verifica a caixa de entrada filtrando o remetente dos Correios.
- Cruza os dados do comprovante com os pedidos cadastrados (por nº do pedido e por nome do cliente) e preenche:
  - **Código de rastreio**;
  - **Valor cobrado pelos Correios**.
- Cria novos envios quando o comprovante traz um pedido ainda não cadastrado.
- Controle de e-mails já processados, evitando duplicidade.
- Suporte a encodings legados (ex.: windows-1252).

## 4. 🔍 Rastreamento via API Oficial dos Correios

- Integração direta com a **API CWS / SRO** dos Correios (sem abrir navegador).
- Autenticação por usuário + código de acesso (Basic → token JWT) ou token direto.
- **Cache do token** com renovação automática ao expirar (retry em HTTP 401).
- Classificação unificada de status:
  - Postado · Em Trânsito · Saiu para Entrega · Aguardando Retirada · Entregue
  - Carteiro Não Atendido · Alerta / Retido · Aguardando Postagem · Não Encontrado
- **Motor de Demora Excessiva / SLA**: cálculo de dias úteis com alertas visuais para remessas paradas há 5+ dias úteis sem movimentação.
- **Linha do tempo visual** com data, hora, unidade de origem/destino e detalhes de cada evento.
- Botão *Consultar Correios Agora* na tela de detalhes.
- Banner de alerta para **tentativa de entrega não efetuada** (1/3, 2/3…), com botão para **copiar aviso pronto para WhatsApp**.

## 5. ⏱️ Monitoramento em Segundo Plano e Notificações

- Ícone na **bandeja do Windows** (System Tray); ao fechar a janela, o app continua rodando.
- Verificação automática periódica dos rastreios (intervalo configurável, padrão 60 min).
- **Notificações nativas do Windows** quando um envio muda para:
  - 🟣 Saiu para Entrega · 🟠 Aguardando Retirada · 🟢 Entregue · 🔴 Problema / Retido · ⏱️ Demora Excessiva
- Opção de **desativar avisos** por envio.
- Menu da bandeja: *Abrir Dashboard*, *Atualizar Rastreios Agora*, *Configurações*, *Sair*.

## 6. 📊 Dashboard, Métricas e Filtros

- Painel principal em **tema escuro** com cards de métricas.
- Card de **⏱️ DEMORA / PARADOS** destacando encomendas atrasadas ou estagnadas há 5+ dias úteis.
- Métricas financeiras:
  - **Total Frete** (cobrado dos clientes na loja);
  - **Total Correios** (custo real dos comprovantes de coleta);
  - **Diferença de Valor** entre frete cobrado e custo pago.
- Busca instantânea por **pedido, cliente, código de rastreio ou nota fiscal**.
- Filtros por status:
  - Ativos · 🔄 Logística Reversa / Devoluções · ⏱️ Demora Excessiva / Parados · Tentativas sem sucesso · Cobrados sem PDF da Tray · Pendentes de entrega
  - Em Trânsito · Saiu para Entrega · Aguardando Retirada · Entregues · Alerta / Retidos
  - Pré-postagens · Arquivados · Todos (inclusive arquivados)
- Filtro por origem: Todas · Estoque Próprio · Dropshipping.
- Correção automática de inconsistências de status.

## 7. ⚡ Ações Rápidas por Envio, Logística Reversa e Reclamação Oficial (PI)

Menu de contexto (botão direito) e colunas clicáveis na tabela:

- 🔄 **Logística Reversa / Devoluções com Detecção Automática via API**:
  - Aceita código de autorização reversa (e-ticket de 9 a 10 dígitos) sem travar a validação de SRO.
  - O monitor em segundo plano e a tela de consulta verificam automaticamente a API de Logística Reversa dos Correios.
  - **Auto-amarração**: Assim que o consumidor posta o pacote na agência dos Correios, a API retorna a etiqueta SRO definitiva (`<numero_etiqueta>`), preenche o rastreamento automaticamente e notifica na área de trabalho do Windows (`🔄 Devolução Postada!`).
  - Alerta de validade: se faltarem 2 dias úteis para vencer a autorização sem postagem, exibe alerta visual de expiração.
- 🔄 **Gerar Devolução / Reversa deste Pedido**: clona os dados do envio original com um clique para iniciar o processo de retorno.
- 📱 **Copiar Instruções de Devolução (WhatsApp)**: texto pré-redigido humanizado com código de autorização, data limite de postagem e passo a passo de como embalar o produto.
- 🔍 **Consultar Status da Reversa na API**: verificação instantânea se a autorização já foi postada na agência.
- ⚡ **Abrir Reclamação Oficial Correios (PI via API)**:
  - Envio automático de manifestação direta pela API oficial dos Correios.
  - Gravação do número de protocolo oficial (`PI-...`) gerado no banco de dados.
  - Em caso de recusa da API (ex: ainda dentro do prazo contratual), exibe o **motivo exato** retornado pelos Correios e a **data permitida para reenvio** (1º dia útil pós-vencimento).
  - Acesso com 1 clique para contingência manual no portal **Fale Conosco dos Correios** (copia os dados automaticamente e abre o site).
- 📋 Copiar código de postagem / nº do pedido
- 📱 Copiar mensagem de aviso ao cliente (carteiro não atendido)
- ⏱️ Copiar mensagem de atraso / demora ao cliente (WhatsApp)
- 📋 Copiar dados formatados p/ reclamação Correios (PI)
- 📦 Ver produtos do pedido
- 🏬 Ver detalhes da origem / fornecedor
- 👤 Ver detalhes do cliente / destinatário
- 📄 Abrir nota fiscal (PDF)
- 🔍 Ver rastreio detalhado
- 📦 Arquivar / desarquivar
- 🔕 Ativar / desativar avisos
- ✏️ Editar · ✕ Excluir

## 8. 🏬 Fornecedores de Dropshipping

- Cadastro completo: razão social, nome fantasia/apelido, CNPJ/CPF.
- Contatos: telefone/WhatsApp e e-mail.
- Endereço de postagem completo (CEP, rua, número, complemento, bairro, cidade, UF).
- Ativação/inativação e observações.
- Cadastro dos **dados da própria loja** como origem "Estoque Próprio".

## 9. 🗄️ Arquivamento Automático

- Envios entregues são **arquivados automaticamente após 7 dias** (configurável).
- Registro da data de entrega e da data de arquivamento.
- Arquivamento/desarquivamento manual a qualquer momento.

## 10. 📈 Excel: Importação e Exportação

- **Exportação** para `.xlsx` (ClosedXML) com colunas formatadas e cores por status.
- **Importação** de planilhas, inserindo novos envios e atualizando os existentes.

## 11. 🧾 Nota Fiscal

- Campo de nota fiscal por envio e abertura do **PDF da NF** direto da tabela.
- Serviço preparado para integração com **API de NF-e / ERP** (URL + token configuráveis), consultando a nota pelo nº do pedido.

## 12. ⚙️ Configurações

- **Correios**: usuário, código de acesso/chave API, cartão de postagem, ambiente, botão *Testar Conexão*.
- **Monitoramento**: intervalo de verificação, notificações, minimizar para bandeja.
- **E-mail de coleta (IMAP)**: servidor, porta, usuário, senha de app e remetente filtrado.
- **Nota Fiscal**: URL e token da API.
- **Arquivamento**: ativar/desativar arquivamento automático de entregues.

## 13. 🔒 Segurança e Privacidade

- **Nenhuma credencial fica no código-fonte.** Chaves da API dos Correios, token da NF-e e senha de e-mail são informados pela tela de Configurações e salvos apenas no banco local `controle_envios.db`.
- O banco local (com dados de clientes e credenciais) está no `.gitignore` e **não é versionado**.
- Banco SQLite com consultas parametrizadas.

## 14. 🛠️ Stack Técnica

| Camada | Tecnologia |
|---|---|
| Linguagem / UI | C# · .NET 8 · WPF |
| Banco de dados | SQLite (`Microsoft.Data.Sqlite`) |
| Leitura de PDF | PdfPig |
| Excel | ClosedXML |
| Rastreamento | API Correios CWS / SRO (REST + JWT) |
| E-mail | IMAP (MailKit) |
| Distribuição | Executável único (`dist/ControleEnvios.exe`) |
