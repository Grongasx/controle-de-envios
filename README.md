# 📦 Controle de Envios Correios (C# .NET 8 WPF)

Software desktop moderno para Windows desenvolvido em **C# (.NET 8 WPF)** para controle e rastreamento automático de envios com a **API oficial dos Correios (CWS / SRO)**, leitor automático de pedidos da plataforma **Tray**, gestão de **Fornecedores de Dropshipping** com endereços e execução em **Segundo Plano (System Tray)**.

---

## 🚀 Como Executar

Você pode iniciar o software de duas maneiras simples:

1. **Pelo Executável Pronto:**
   * Dê dois cliques em **`dist\ControleEnvios.exe`** ou no atalho **`iniciar.bat`**.
2. **Pelo Terminal (Desenvolvimento):**
   * Abra a pasta no terminal e execute:
     ```bash
     dotnet run
     ```

---

## 🌟 Principais Funcionalidades

### 1. 📄 Extrator Automático de Pedidos da Tray (Drag & Drop)
* Basta **arrastar e soltar o PDF do pedido da Tray** direto na janela do sistema (ou clicar na área de importação).
* O extrator lê o documento instantaneamente e pré-preenche:
  * **Nº do Pedido**
  * **Nome do Cliente, CPF, Celular e E-mail**
  * **Endereço Completo de Entrega** (Logradouro, Bairro, Cidade, UF, CEP)
  * **Lista de Produtos e Quantidades** (com tamanhos, referências e códigos)
  * **Forma de Envio da Loja** (SEDEX, PAC, etc.) e Peso em gramas
  * **Valor do Frete e Total do Pedido**

### 2. 🏬 Gestão de Fornecedores de Dropshipping
* Cadastro completo de fornecedores parceiros:
  * Razão Social e Nome Fantasia / Apelido
  * CNPJ / CPF e Contatos (Telefone, WhatsApp, E-mail)
  * **Endereço Completo de Postagem** (Rua, Número, Complemento, Bairro, Cidade, UF, CEP)
* No cadastro do envio, você seleciona com um clique se o pacote parte do **Estoque Próprio (WebRiders)** ou de um **Fornecedor Dropshipping**.

### 3. 🔍 Rastreamento Automático Correios (CWS API / SRO)
* **Zero abas no navegador:** Todas as consultas são feitas via requisição HTTP direta à API oficial dos Correios.
* Suporte nativo a:
  * **Postagem Normal:** Acompanhamento desde a postagem até a entrega.
  * **Pré-postagem:** Monitora o código e detecta automaticamente quando o pacote é postado na agência física.
* **Linha do Tempo Visual:** Clique duas vezes em qualquer envio ou no botão *"Rastreio"* para ver o histórico completo dos Correios com data, hora, local e mensagens oficiais.

### 4. ⏱️ Execução em Segundo Plano & Notificações (System Tray)
* O aplicativo roda discretamente perto do relógio do Windows com ícone na bandeja.
* Ao fechar a janela, o aplicativo **minimiza para a bandeja** e continua monitorando em segundo plano.
* **Notificações Nativas do Windows (Balloon/Toast):** Avisa na tela quando uma encomenda mudar para:
  * 🟣 **Saiu para Entrega**
  * 🟠 **Aguardando Retirada na Agência** (evita devolução ao remetente!)
  * 🟢 **Entregue com Sucesso**
  * 🔴 **Problema / Retido / Atrasado**
* Menu com botão direito no ícone do relógio: *"Abrir Dashboard"*, *"Atualizar Rastreios Agora"*, *"Configurações"* e *"Sair"*.

### 5. 📊 Exportação e Relatórios em Excel (.xlsx)
* Botão para gerar a planilha completa no formato `.xlsx` com todas as colunas formatadas, cores e dados consolidados.

---

## ⚙️ Configuração da API dos Correios

1. Abra o aplicativo e clique no botão **⚙️ Configurações**.
2. Preencha suas credenciais do portal dos Correios:
   * **Usuário:** Seu usuário do Meu Correios / IdCorreios
   * **Código de Acesso / Chave API:** Código gerado no portal CWS dos Correios
   * **Cartão de Postagem:** Número do cartão (opcional se contratual)
3. Clique em **"Testar Conexão com os Correios"** para validar o token JWT na hora.
4. Defina o intervalo de verificação automática em segundo plano (padrão: 60 minutos).

---

## 📁 Estrutura do Projeto

```
Controle Envios/
├── dist/
│   └── ControleEnvios.exe       # Executável de produção standalone
├── Models/
│   └── Models.cs                # Classes Envio, Fornecedor, ItemEnvio, EventoRastreio
├── Data/
│   └── DatabaseContext.cs       # Banco local SQLite (controle_envios.db)
├── Services/
│   ├── TrayPdfExtractor.cs      # Extrator de pedidos Tray via PdfPig
│   ├── CorreiosApiService.cs    # Cliente CWS / SRO com cache de token JWT
│   ├── EnvioService.cs          # Regras de negócio, consultas e filtros
│   ├── FornecedorService.cs     # CRUD de Fornecedores de Dropshipping
│   ├── ExcelService.cs          # Exportação ClosedXML (.xlsx)
│   └── BackgroundMonitorService.cs # Monitor em segundo plano com eventos
├── Views/
│   ├── MainWindow.xaml / .cs    # Dashboard principal Dark com métricas e filtros
│   ├── EnvioDialog.xaml / .cs   # Modal de envio com Drag & Drop de PDF Tray
│   ├── FornecedoresDialog.xaml  # Gestão de fornecedores e endereços
│   ├── RastreioDetalhesDialog.xaml # Linha do tempo visual do rastreamento
│   └── ConfigDialog.xaml        # Configuração de credenciais Correios e Tray
├── App.xaml / .cs               # Gerenciador da bandeja do Windows (NotifyIcon)
├── iniciar.bat                  # Script de início rápido
└── publicar_exe.bat             # Script para recompilar o executável
```

---

## 🚀 CI/CD & Publicação Automática (GitHub Actions)

O projeto conta com um fluxo de CI/CD automatizado no GitHub Actions ([.github/workflows/publish.yml](.github/workflows/publish.yml)):

* **Publicação Automática por Tag:**
  Sempre que uma tag de versão for enviada (ex.: `v1.2.5`), o GitHub Actions compila o projeto no ambiente Windows e cria uma **GitHub Release** com os seguintes arquivos anexados:
  * `ControleEnvios.exe` — Executável padrão (win-x64, ~25MB, requer .NET 8 Desktop Runtime).
  * `ControleEnvios-SelfContained.exe` — Executável autônomo (não necessita de .NET instalado no PC).
  * `ControleEnvios-win-x64.zip` e `ControleEnvios-SelfContained-win-x64.zip` — Pacotes compactados para download direto.
* **Validação Contínua:**
  Todo `push` ou `pull request` para a branch `main` executa a compilação completa para garantir que o código sempre permaneça íntegro.
* **Disparo Manual:**
  O workflow também pode ser disparado sob demanda pela aba *Actions* do GitHub com opção de gerar releases avulsas.

