using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using ControleEnvios.Data;
using ControleEnvios.Services;
using ControleEnvios.Views;
using Application = System.Windows.Application;

namespace ControleEnvios
{
    public partial class App : Application
    {
        private static NotifyIcon? _notifyIcon;

        // Instância única na máquina (prefixo Global\ vale para todas as sessões/usuários do Windows)
        private const string NomeMutex = @"Global\ControleEnvios_InstanciaUnica_7F3A2C1E";
        private const string NomeEventoMostrar = @"Global\ControleEnvios_MostrarJanela_7F3A2C1E";
        private static Mutex? _mutexInstancia;
        private static EventWaitHandle? _eventoMostrar;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!GarantirInstanciaUnica())
            {
                // Já existe uma instância aberta: encerra imediatamente (antes de criar a janela do StartupUri)
                Environment.Exit(0);
                return;
            }

            base.OnStartup(e);

            // Inicializa Banco de Dados
            DatabaseContext.InitializeDatabase();

            // Configura o Ícone da Bandeja do Sistema (System Tray)
            ConfigurarSystemTray();

            // Inscreve-se nas notificações críticas de encomendas
            BackgroundMonitorService.Instance.NotificacaoCritica += (titulo, msg) =>
            {
                Notificar(titulo, msg);
            };
        }

        /// <summary>
        /// Retorna true se esta é a primeira instância. Caso contrário, sinaliza a instância
        /// existente para trazer a janela à frente e retorna false.
        /// </summary>
        private static bool GarantirInstanciaUnica()
        {
            bool criadoNovo;
            try
            {
                _mutexInstancia = new Mutex(true, NomeMutex, out criadoNovo);
            }
            catch (UnauthorizedAccessException)
            {
                // Mutex já existe e pertence a outro usuário do Windows
                criadoNovo = false;
            }

            if (!criadoNovo)
            {
                bool sinalizado = false;
                try
                {
                    if (EventWaitHandle.TryOpenExisting(NomeEventoMostrar, out var evento))
                    {
                        // Permite que a instância existente traga sua janela para o primeiro plano
                        AllowSetForegroundWindow(ASFW_ANY);
                        evento.Set();
                        evento.Dispose();
                        sinalizado = true;
                    }
                }
                catch { }

                if (!sinalizado)
                {
                    System.Windows.MessageBox.Show(
                        "O Controle de Envios já está aberto nesta máquina (possivelmente em outro usuário do Windows).",
                        "Controle de Envios", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return false;
            }

            // Primeira instância: escuta pedidos de outras tentativas de abertura para mostrar a janela
            try
            {
                _eventoMostrar = new EventWaitHandle(false, EventResetMode.AutoReset, NomeEventoMostrar);
                var t = new Thread(() =>
                {
                    try
                    {
                        while (_eventoMostrar != null && _eventoMostrar.WaitOne())
                        {
                            MostrarJanelaPrincipal();
                        }
                    }
                    catch { }
                })
                { IsBackground = true, Name = "InstanciaUnicaListener" };
                t.Start();
            }
            catch { }

            return true;
        }

        private const int ASFW_ANY = -1;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        private void ConfigurarSystemTray()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Controle de Envios Correios & Tray",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("📦 Abrir Controle de Envios", null, (s, ev) => MostrarJanelaPrincipal());
            contextMenu.Items.Add("🔄 Atualizar Rastreios Agora", null, async (s, ev) =>
            {
                Notificar("Correios", "Iniciando verificação de rastreios...");
                await BackgroundMonitorService.Instance.VerificarAgoraAsync();
            });
            contextMenu.Items.Add("⚙️ Configurações", null, (s, ev) =>
            {
                Current.Dispatcher.Invoke(() =>
                {
                    var dlg = new ConfigDialog();
                    dlg.ShowDialog();
                });
            });
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("❌ Sair do Aplicativo", null, (s, ev) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
                Current.Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, ev) => MostrarJanelaPrincipal();
        }

        public static void MostrarJanelaPrincipal()
        {
            Current.Dispatcher.Invoke(() =>
            {
                if (Current.MainWindow != null)
                {
                    Current.MainWindow.Show();
                    if (Current.MainWindow.WindowState == WindowState.Minimized)
                        Current.MainWindow.WindowState = WindowState.Normal;
                    Current.MainWindow.Activate();
                    Current.MainWindow.Topmost = true;
                    Current.MainWindow.Topmost = false;
                    Current.MainWindow.Focus();
                }
            });
        }

        public static void Notificar(string titulo, string mensagem)
        {
            try
            {
                bool permitirNotif = DatabaseContext.ObterConfig("notificar_desktop", "1") == "1";
                if (permitirNotif && _notifyIcon != null)
                {
                    bool isAlerta = titulo.Contains("⚠️") || titulo.Contains("Alerta") || titulo.Contains("Atenção") || titulo.Contains("Cobrado");
                    var icone = isAlerta ? ToolTipIcon.Warning : ToolTipIcon.Info;
                    _notifyIcon.ShowBalloonTip(5000, titulo, mensagem, icone);
                }
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            BackgroundMonitorService.Instance.Parar();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            try
            {
                _mutexInstancia?.ReleaseMutex();
                _mutexInstancia?.Dispose();
            }
            catch { }

            base.OnExit(e);
        }
    }
}
