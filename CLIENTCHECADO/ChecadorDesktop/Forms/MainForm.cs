using System.Security.Cryptography;
using System.Text;
using ChecadorDesktop.Data;
using ChecadorDesktop.Models;
using ChecadorDesktop.Services;
using Microsoft.Extensions.Configuration;

namespace ChecadorDesktop.Forms;

public class MainForm : Form
{
    private const int SessionExpiryHours = 12;
    private const string GuardProcessName = "GuardEP";
    private const string GuardExeName = "GuardEP.exe";

    private readonly bool _needPin;
    private readonly SQLiteRepository _repository;
    private readonly ApiClientService _apiClient;
    private readonly WebcamService _webcamService;
    private readonly SyncManager _syncManager;
    private readonly AlertScheduler _alertScheduler;

    private static string appVersion = string.Empty;

    private int _idTrabajadorActual;
    private string _nombreTrabajadorActual = string.Empty;
    private int _tipoRegistroActual = 1; // 1: Entrada, 2: SalidaComer, 3: EntradaComida, 4: Salida
    private bool _alertaBloqueanteActiva;
    private bool _schedulerAlertasActivo;
    private int? _schedulerTrabajadorActualId;
    private WorkerCacheItem? _trabajadorCacheActual;
    private WorkerSessionLocal? _sesionActivaActual;

    // Controles de UI
    private Label _lblEstacion = null!;
    private Label _lblSyncStatus = null!;
    private TextBox _txtIdTrabajador = null!;
    private TextBox _txtPin = null!;
    private Button _btnLogin = null!;
    private Panel _pnlMarcaje = null!;
    private Label _lblTrabajadorInfo = null!;
    private Label _lblTipoMarcajeSiguiente = null!;
    private Button _btnRegistrarAsistencia = null!;
    private Button _btnDesvincular = null!;
    private PictureBox _picCamaraPreview = null!;
    private Button _btnMinimizarTray = null!;
    private Label _lblMotivoBloqueo = null!;
    private readonly System.Windows.Forms.Timer _timerBloqueoEntrada = new() { Interval = 1000 };

    // Bandeja del Sistema
    private NotifyIcon _notifyIcon = null!;
    private ContextMenuStrip _trayMenu = null!;

    public MainForm()
    {
        _needPin = CargarNeedPinDesdeConfig();
        _repository = new SQLiteRepository();
        _apiClient = new ApiClientService();
        _webcamService = new WebcamService();
        _syncManager = new SyncManager(_repository, _apiClient);
        _alertScheduler = new AlertScheduler(_repository, _apiClient, _webcamService);

        ConstruirUI();
        ConstruirSystemTray();
        ConfigurarEventos();
    }

    private static bool CargarNeedPinDesdeConfig()
    {

        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            appVersion = config["AppVersion"] ?? string.Empty;
            return bool.TryParse(config["NeedPin"], out bool np) ? np : true;
        }
        catch
        {
            return true;
        }
    }

    private void ConstruirUI()
    {
        using var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("ChecadorDesktop.ApplicationIcon")!;
        using var applicationIcon = new Icon(iconStream);
        Icon = (Icon)applicationIcon.Clone();
        Text = "Checador de Asistencia" + " - Versión " + appVersion;
        Size = new Size(720, 580);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = true;
        BackColor = Color.FromArgb(240, 242, 245);

        // Header
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(30, 41, 59) };
        var lblTitulo = new Label { Text = "SISTEMA DE ASISTENCIA Y CONTROL LABORAL", ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(20, 12), AutoSize = true };
        _lblEstacion = new Label { Text = $"Estación: {Environment.MachineName}", ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 8.5f), Location = new Point(20, 35), AutoSize = true };
        pnlHeader.Controls.AddRange(new Control[] { lblTitulo, _lblEstacion });

        // Panel de Identificación (ID fijado y campo de PIN ágil)
        var grpLogin = new GroupBox { Text = "Identificación de Asistencia", Location = new Point(20, 75), Size = new Size(665, 95), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

        var lblId = new Label { Text = "ID Asignado:", Location = new Point(20, 35), AutoSize = true, Font = new Font("Segoe UI", 9) };
        _txtIdTrabajador = new TextBox
        {
            Location = new Point(110, 32),
            Width = 100,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ReadOnly = true,
            BackColor = Color.FromArgb(235, 238, 242),
            TabStop = false
        };

        var lblPin = new Label { Text = "Ingrese su PIN:", Location = new Point(230, 35), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        _txtPin = new TextBox
        {
            Location = new Point(335, 32),
            Width = 110,
            UseSystemPasswordChar = true,
            Font = new Font("Segoe UI", 10f),
            MaxLength = 8
        };

        _btnLogin = new Button
        {
            Text = "Continuar ➜",
            Location = new Point(460, 29),
            Width = 140,
            Height = 32,
            BackColor = Color.FromArgb(15, 118, 110),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        if (!_needPin)
        {
            lblPin.Text = "Modo: Autónomo (Sin PIN)";
            lblPin.ForeColor = Color.FromArgb(15, 118, 110);
            _txtPin.Visible = false;
            _btnLogin.Visible = false;
        }

        grpLogin.Controls.AddRange(new Control[] { lblId, _txtIdTrabajador, lblPin, _txtPin, _btnLogin });

        // Marcaje Panel
        _pnlMarcaje = new Panel { Location = new Point(20, 180), Size = new Size(665, 290), BackColor = Color.White, Visible = false };
        _lblTrabajadorInfo = new Label { Location = new Point(20, 15), Size = new Size(620, 28), Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
        _lblTipoMarcajeSiguiente = new Label
        {
            Location = new Point(20, 48),
            Size = new Size(620, 32),
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 64, 175),
            BackColor = Color.FromArgb(219, 234, 254),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        _picCamaraPreview = new PictureBox { Location = new Point(20, 85), Size = new Size(250, 180), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(15, 23, 42) };

        _btnRegistrarAsistencia = new Button { Text = "REALIZAR REGISTRO", Location = new Point(290, 95), Size = new Size(345, 52), BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12, FontStyle.Bold), Cursor = Cursors.Hand };

        _btnMinimizarTray = new Button { Text = "Ocultar a Segundo Plano (Activar Alertas)", Location = new Point(290, 160), Size = new Size(345, 38), BackColor = Color.FromArgb(241, 245, 249), ForeColor = Color.FromArgb(51, 65, 85), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Cursor = Cursors.Hand };

        _lblMotivoBloqueo = new Label
        {
            Location = new Point(290, 210),
            Size = new Size(345, 55),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(146, 64, 14),
            BackColor = Color.FromArgb(254, 243, 199),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            Visible = false
        };

        _pnlMarcaje.Controls.AddRange(new Control[] { _lblTrabajadorInfo, _lblTipoMarcajeSiguiente, _picCamaraPreview, _btnRegistrarAsistencia, _btnMinimizarTray, _lblMotivoBloqueo });

        // Footer
        _lblSyncStatus = new Label { Text = "Iniciando monitor de red...", Location = new Point(20, 495), Size = new Size(480, 25), Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(71, 85, 105) };
        _btnDesvincular = new Button { Text = "Desvincular Equipo", Location = new Point(535, 490), Width = 150, Height = 28, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(185, 28, 28), Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand };

        Controls.AddRange(new Control[] { pnlHeader, grpLogin, _pnlMarcaje, _lblSyncStatus, _btnDesvincular });

        AcceptButton = _btnLogin;
    }

    private void ConstruirSystemTray()
    {
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Abrir Checador", null, (s, e) => RestaurarDesdeTray());
        _trayMenu.Items.Add("-");
        _trayMenu.Items.Add("Salir de la Aplicación", null, (s, e) => SalirAplicacion());

        _notifyIcon = new NotifyIcon
        {
            Icon = this.Icon,
            ContextMenuStrip = _trayMenu,
            Text = "Checador de Asistencia (Activo en segundo plano)",
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => RestaurarDesdeTray();
    }

    private void ConfigurarEventos()
    {
        Load += async (s, e) => await InicializarAppAsync();
        FormClosing += (s, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                MinimizarATray();
            }
            else
            {
                _syncManager.Detener();
                _timerBloqueoEntrada.Stop();
                DetenerSchedulerAlertas("FormClosing");
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        };

        _syncManager.OnSyncStatusChanged += (s, msg) =>
        {
            if (InvokeRequired)
                BeginInvoke(new Action(() => _lblSyncStatus.Text = msg));
            else
                _lblSyncStatus.Text = msg;
        };

        // Cuando el SyncManager detecta que los horarios cambiaron en el servidor,
        // actualizar la referencia en memoria y reasignar al AlertScheduler para que
        // el proximo tick use los horarios frescos sin necesidad de reiniciarlo.
        _syncManager.OnHorariosActualizados += (s, workerActualizado) =>
        {
            Action actualizar = () =>
            {
                _trabajadorCacheActual = workerActualizado;
                _alertScheduler.AsignarTrabajador(workerActualizado);
                _repository.RegistrarLog("INFO", "MainForm",
                    $"AlertScheduler actualizado con horarios frescos del servidor. Trabajador={workerActualizado.IdTrabajador}. " +
                    $"Entrada={workerActualizado.HoraEntrada}, SalidaComida={workerActualizado.HoraSalidaComida}, " +
                    $"EntradaComida={workerActualizado.HoraEntradaComida}, Salida={workerActualizado.HoraSalida}");
            };

            if (InvokeRequired)
                BeginInvoke(actualizar);
            else
                actualizar();
        };

        _alertScheduler.OnAlertaDisparada += (s, args) => DispararAlertaBloqueante(args.TipoRegistro, args.HoraOficial);

        _btnLogin.Click += async (s, e) => await AutenticarTrabajadorAsync();
        _btnRegistrarAsistencia.Click += async (s, e) => await RegistrarAsistenciaAsync();
        _btnDesvincular.Click += async (s, e) => await DesvincularEquipoAsync();
        _btnMinimizarTray.Click += (s, e) => MinimizarATray();

        _timerBloqueoEntrada.Tick += (s, e) => RefrescarBloqueoEntrada();
        _timerBloqueoEntrada.Start();
    }

    private async Task InicializarAppAsync()
    {
        IniciarCentinelaGuard();

        _syncManager.Iniciar(15);

        // 1. Validar primero en SQLite local el vínculo de la estación (Offline-First)
        var vinculo = _repository.ObtenerVinculoLocal(Environment.MachineName);

        if (vinculo == null || !vinculo.EstaVigente)
        {
            // Solicitar vinculación
            using var bindForm = new BindingForm(_apiClient, _repository);
            if (bindForm.ShowDialog() != DialogResult.OK || !bindForm.VinculadoExitosamente)
            {
                MessageBox.Show("Esta computadora no está vinculada. La aplicación se cerrará.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Application.Exit();
                return;
            }

            // Recargar vínculo local recién creado
            vinculo = _repository.ObtenerVinculoLocal(Environment.MachineName);
        }

        // 2. Pre-cargar el ID vinculado y fijar el cursor en el PIN (Opción B)
        if (vinculo != null && vinculo.EstaVigente)
        {
            _idTrabajadorActual = vinculo.IdTrabajador;
            _txtIdTrabajador.Text = _idTrabajadorActual.ToString();
            _lblEstacion.Text = $"Estación: {Environment.MachineName} | Vinculada a ID: {_idTrabajadorActual}";

            if (!_needPin)
            {
                await InicializarModoAutonomoAsync(vinculo);
            }
            else
            {
                IntentarRecuperarSesionActiva(vinculo.IdTrabajador);
                IniciarAlertasDesdeSesionSiAplica();

                // Foco directo en el campo PIN
                BeginInvoke(new Action(() => _txtPin.Focus()));
            }
        }
    }

    private async Task InicializarModoAutonomoAsync(EstacionVinculoLocal vinculo)
    {
        // 1. Obtener de caché local si ya existe
        _trabajadorCacheActual = _repository.ObtenerWorkerCachePorTrabajador(vinculo.IdTrabajador);

        // 2. Si no tiene horarios en caché, solicitar el PIN por única ocasión para sincronizarlos
        if (_trabajadorCacheActual == null || string.IsNullOrWhiteSpace(_trabajadorCacheActual.HoraEntrada))
        {
            using var pinForm = new InitialPinSetupForm(_apiClient, _repository, vinculo.IdTrabajador, vinculo.NombreTrabajador);
            if (pinForm.ShowDialog(this) == DialogResult.OK && pinForm.TrabajadorSincronizado != null)
            {
                _trabajadorCacheActual = pinForm.TrabajadorSincronizado;
                if (vinculo.NombreTrabajador != _trabajadorCacheActual.NombreCompleto)
                {
                    vinculo.NombreTrabajador = _trabajadorCacheActual.NombreCompleto;
                    _repository.GuardarVinculoLocal(vinculo);
                }
            }
            else
            {
                _repository.RegistrarLog("WARN", "GuardStartup", "Sincronización inicial de PIN cancelada u omitida por el usuario.");
            }
        }

        if (_trabajadorCacheActual == null)
        {
            _trabajadorCacheActual = new WorkerCacheItem
            {
                IdTrabajador = vinculo.IdTrabajador,
                Pin = 0,
                NombreCompleto = vinculo.NombreTrabajador
            };
        }

        _nombreTrabajadorActual = _trabajadorCacheActual.NombreCompleto;

        _sesionActivaActual = new WorkerSessionLocal
        {
            NbEquipo = Environment.MachineName,
            IdTrabajador = vinculo.IdTrabajador,
            Pin = _trabajadorCacheActual.Pin,
            NombreCompleto = _nombreTrabajadorActual,
            HoraEntrada = _trabajadorCacheActual.HoraEntrada,
            HoraSalidaComida = _trabajadorCacheActual.HoraSalidaComida,
            HoraEntradaComida = _trabajadorCacheActual.HoraEntradaComida,
            HoraSalida = _trabajadorCacheActual.HoraSalida,
            FechaInicio = DateTime.Now,
            FechaUltimaActividad = DateTime.Now,
            TipoRegistroUltimo = 0,
            Activa = true
        };
        _repository.GuardarSesionActiva(_sesionActivaActual);

        AsegurarSchedulerAlertasActivo("ModoAutonomo");
        _repository.RegistrarLog("INFO", "GuardStartup", $"Modo autónomo (NeedPin=false) iniciado. Scheduler activo para trabajador={vinculo.IdTrabajador}. Entrada={_trabajadorCacheActual.HoraEntrada ?? "N/D"}");

        DeterminarSiguienteMarcaje();
        MostrarPanelMarcaje();

        // Minimizar a la bandeja para arranque transparente en segundo plano
        BeginInvoke(new Action(() => MinimizarATray()));
    }

    private void IniciarAlertasDesdeSesionSiAplica()
    {
        if (_trabajadorCacheActual == null || _sesionActivaActual == null)
        {
            _repository.RegistrarLog("INFO", "GuardStartup", "Sesión no disponible al iniciar. Reautenticación requerida para activar alertas.");
            return;
        }

        if (!TieneSesionActivaValida())
        {
            _repository.RegistrarLog("INFO", "GuardStartup", "Sesión inválida o expirada al iniciar. Reautenticación requerida para activar alertas.");
            return;
        }

        AsegurarSchedulerAlertasActivo("AutoInicioSesionValida");
        _repository.RegistrarLog("INFO", "GuardStartup", $"Sesión válida al iniciar. Scheduler autoactivado para trabajador={_trabajadorCacheActual.IdTrabajador}.");
    }

    private void AsegurarSchedulerAlertasActivo(string origen)
    {
        if (_trabajadorCacheActual == null)
        {
            _repository.RegistrarLog("WARN", "SchedulerState", $"No se pudo activar scheduler: trabajador nulo. Origen={origen}");
            return;
        }

        int idTrabajador = _trabajadorCacheActual.IdTrabajador;

        if (_schedulerAlertasActivo && _schedulerTrabajadorActualId == idTrabajador)
        {
            _repository.RegistrarLog("INFO", "SchedulerState", $"Activación omitida: scheduler ya activo para trabajador={idTrabajador}. Origen={origen}");
            return;
        }

        _alertScheduler.AsignarTrabajador(_trabajadorCacheActual);
        _alertScheduler.Iniciar(30);

        if (_schedulerAlertasActivo && _schedulerTrabajadorActualId != idTrabajador)
        {
            _repository.RegistrarLog("INFO", "SchedulerState", $"Scheduler reasignado a trabajador={idTrabajador}. Origen={origen}");
        }
        else
        {
            _repository.RegistrarLog("INFO", "SchedulerState", $"Scheduler activado para trabajador={idTrabajador}. Origen={origen}");
        }

        _schedulerAlertasActivo = true;
        _schedulerTrabajadorActualId = idTrabajador;
    }

    private void DetenerSchedulerAlertas(string origen)
    {
        _alertScheduler.Detener();

        if (_schedulerAlertasActivo || _schedulerTrabajadorActualId.HasValue)
        {
            _repository.RegistrarLog("INFO", "SchedulerState", $"Scheduler detenido. Origen={origen}; Trabajador={_schedulerTrabajadorActualId?.ToString() ?? "N/D"}");
        }

        _schedulerAlertasActivo = false;
        _schedulerTrabajadorActualId = null;
    }

    private void IniciarCentinelaGuard()
    {
        try
        {
            var guardProcesses = System.Diagnostics.Process.GetProcessesByName(GuardProcessName);
            if (guardProcesses.Length == 0)
            {
                string guardPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GuardExeName);
                if (System.IO.File.Exists(guardPath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = guardPath,
                        UseShellExecute = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
        }
        catch
        {
            // Ignorar si falla el lanzamiento automatico
        }
    }

    private async Task AutenticarTrabajadorAsync()
    {
        if (!_needPin)
        {
            DeterminarSiguienteMarcaje();
            MostrarPanelMarcaje();
            return;
        }

        DeterminarSiguienteMarcaje();

        // Para tipos 2/3/4 se permite continuar con sesión local válida sin solicitar PIN.
        if (_tipoRegistroActual is 2 or 3 or 4 && TieneSesionActivaValida())
        {
            if (_trabajadorCacheActual == null)
            {
                MessageBox.Show(
                    "La sesión local no está disponible para continuar. Reingrese su PIN.",
                    "Reautenticación requerida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _txtPin.Focus();
                return;
            }

            AsegurarSchedulerAlertasActivo("ContinuarSesionLocal");
            MostrarPanelMarcaje();
            return;
        }

        if (!int.TryParse(_txtPin.Text.Trim(), out int pin))
        {
            MessageBox.Show("Por favor ingrese su PIN numérico.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtPin.Focus();
            return;
        }

        _btnLogin.Enabled = false;

        // Validar con la API o la caché local
        var respApi = await _apiClient.WorkerLoginAsync(_idTrabajadorActual, pin);
        if (respApi != null && respApi.Exito == 1 && respApi.Data != null)
        {
            _nombreTrabajadorActual = respApi.Data.NombreCompleto;

            _trabajadorCacheActual = new WorkerCacheItem
            {
                IdTrabajador = _idTrabajadorActual,
                Pin = pin,
                NombreCompleto = _nombreTrabajadorActual,
                HoraEntrada = respApi.Data.HoraEntrada,
                HoraSalidaComida = respApi.Data.HoraSalidaComida,
                HoraEntradaComida = respApi.Data.HoraEntradaComida,
                HoraSalida = respApi.Data.HoraSalida
            };

            // La sesión operativa se establece al autenticar Entrada,
            // y para reautenticación a mitad de jornada se mantiene activa.
            _sesionActivaActual = new WorkerSessionLocal
            {
                NbEquipo = Environment.MachineName,
                IdTrabajador = _trabajadorCacheActual.IdTrabajador,
                Pin = _trabajadorCacheActual.Pin,
                NombreCompleto = _trabajadorCacheActual.NombreCompleto,
                HoraEntrada = _trabajadorCacheActual.HoraEntrada,
                HoraSalidaComida = _trabajadorCacheActual.HoraSalidaComida,
                HoraEntradaComida = _trabajadorCacheActual.HoraEntradaComida,
                HoraSalida = _trabajadorCacheActual.HoraSalida,
                FechaInicio = DateTime.Now,
                FechaUltimaActividad = DateTime.Now,
                TipoRegistroUltimo = 0,
                Activa = true
            };

            _repository.GuardarWorkerCache(_trabajadorCacheActual);
            _repository.GuardarSesionActiva(_sesionActivaActual);
            _repository.RegistrarLog("INFO", "Sesion", $"Sesión activa creada/renovada. Trabajador={_trabajadorCacheActual.IdTrabajador}");

            // Actualizar nombre en vínculo local si aún decía provisional
            var vinc = _repository.ObtenerVinculoLocal(Environment.MachineName);
            if (vinc != null && vinc.NombreTrabajador != _nombreTrabajadorActual)
            {
                vinc.NombreTrabajador = _nombreTrabajadorActual;
                _repository.GuardarVinculoLocal(vinc);
            }
        }
        else
        {
            // Búsqueda en caché SQLite
            var cache = _repository.ObtenerWorkerCache(_idTrabajadorActual, pin);
            if (cache != null)
            {
                _nombreTrabajadorActual = cache.NombreCompleto;
                _trabajadorCacheActual = cache;
                _sesionActivaActual = new WorkerSessionLocal
                {
                    NbEquipo = Environment.MachineName,
                    IdTrabajador = cache.IdTrabajador,
                    Pin = cache.Pin,
                    NombreCompleto = cache.NombreCompleto,
                    HoraEntrada = cache.HoraEntrada,
                    HoraSalidaComida = cache.HoraSalidaComida,
                    HoraEntradaComida = cache.HoraEntradaComida,
                    HoraSalida = cache.HoraSalida,
                    FechaInicio = DateTime.Now,
                    FechaUltimaActividad = DateTime.Now,
                    TipoRegistroUltimo = 0,
                    Activa = true
                };
                _repository.GuardarSesionActiva(_sesionActivaActual);
                _repository.RegistrarLog("INFO", "Sesion", $"Sesión activa creada desde caché local. Trabajador={cache.IdTrabajador}");
                MessageBox.Show("PIN validado fuera de línea (Offline Cache).", "Modo Offline", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(respApi?.Mensaje ?? "PIN incorrecto o usuario no registrado.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnLogin.Enabled = true;
                _txtPin.SelectAll();
                _txtPin.Focus();
                return;
            }
        }

        // Activar programador de alertas LFT
        if (_trabajadorCacheActual == null)
        {
            _btnLogin.Enabled = true;
            MessageBox.Show("No fue posible establecer el trabajador autenticado.", "Error de sesión", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        AsegurarSchedulerAlertasActivo("AutenticacionExitosa");

        DeterminarSiguienteMarcaje();
        MostrarPanelMarcaje();
        _btnLogin.Enabled = true;
    }

    private void DispararAlertaBloqueante(int tipoRegistro, string horaOficial)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => DispararAlertaBloqueante(tipoRegistro, horaOficial)));
            return;
        }

        if (!AlertScheduler.EsDiaLaboral(DateTime.Now))
        {
            _repository.RegistrarLog("INFO", "Alerta", $"Disparo de alerta tipo {tipoRegistro} ignorado en fin de semana.");
            return;
        }

        if (_alertaBloqueanteActiva)
        {
            _repository.RegistrarLog("INFO", "Alerta", $"Disparo ignorado: ya existe alerta activa. TipoSolicitado={tipoRegistro}");
            return;
        }

        if (_trabajadorCacheActual == null) return;

        // Revalidar estado justo antes de abrir para descartar disparos encolados u obsoletos.
        DeterminarSiguienteMarcaje();
        if (_tipoRegistroActual != tipoRegistro)
        {
            _repository.RegistrarLog("INFO", "Alerta", $"Disparo ignorado por estado actualizado. TipoSolicitado={tipoRegistro}; TipoEsperado={_tipoRegistroActual}");
            return;
        }

        _alertaBloqueanteActiva = true;
        var cortinasSecundarias = new List<OverlayBackgroundForm>();
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var pantallaPrincipal = Screen.AllScreens.FirstOrDefault(p => p.Primary) ?? Screen.AllScreens[0];
        bool restauradaParaAlerta = false;

        try
        {
            restauradaParaAlerta = RestaurarParaAlerta(pantallaPrincipal.WorkingArea);

            // 1. Identificar y bloquear todos los monitores secundarios activos.
            foreach (var pantalla in Screen.AllScreens)
            {
                if (pantalla.Equals(pantallaPrincipal))
                {
                    continue;
                }

                var cortina = new OverlayBackgroundForm(pantalla);
                cortinasSecundarias.Add(cortina);
                cortina.Show();
            }

            if (cortinasSecundarias.Count > 0)
            {
                _repository.RegistrarLog("INFO", "Alerta", $"Pantallas cortina activadas en monitores secundarios: {cortinasSecundarias.Count}");
            }

            // 2. Desplegar la alerta interactiva bloqueante en el monitor principal.
            _repository.RegistrarLog(
                "INFO",
                "AlertaDiag",
                $"Tipo={tipoRegistro}; MainRestaurada={restauradaParaAlerta}; MainBounds={Bounds}; MainDpi={DeviceDpi}; PreviaAlertaMs={cronometro.ElapsedMilliseconds}; " +
                $"Pantallas=[{string.Join(" | ", Screen.AllScreens.Select(p => $"{p.DeviceName} Principal={p.Primary} Bounds={p.Bounds}"))}]");

            using var formAlerta = new AlertNotificationForm(
                tipoRegistro,
                horaOficial,
                _trabajadorCacheActual,
                _repository,
                _apiClient,
                _webcamService,
                pantallaPrincipal.WorkingArea);

            var resultado = formAlerta.ShowDialog(this);

            if (formAlerta.AlertaVencida || formAlerta.AlertaEnDiaNoLaborable)
            {
                _repository.RegistrarLog(
                    "INFO",
                    "Alerta",
                    $"Alerta tipo {tipoRegistro} cerrada por vencimiento o día no laborable. Se continuará con el ciclo permitido.");
                _notifyIcon.ShowBalloonTip(
                    5000,
                    "Alerta vencida",
                    "La alerta ya no está vigente. Los marcajes solo están permitidos de lunes a viernes.",
                    ToolTipIcon.Info);
                _pnlMarcaje.Visible = false;
                MinimizarATray();
            }
            else if (formAlerta.AlertaPospuesta)
            {
                _alertScheduler.PosponerAlerta(tipoRegistro, 10);
                _notifyIcon.ShowBalloonTip(3000, "Alerta Pospuesta", "La alerta de comida se volverá a mostrar en 10 minutos.", ToolTipIcon.Info);
                MinimizarATray();
            }
            else if (resultado == DialogResult.OK)
            {
                DeterminarSiguienteMarcaje();
                _pnlMarcaje.Visible = false;

                // Tras confirmar una alerta modal, regresar a segundo plano evita
                // que la pantalla principal quede visible solicitando otro registro.
                MinimizarATray();
            }
        }
        finally
        {
            // 3. Liberar y destruir todas las cortinas de monitores secundarios.
            foreach (var cortina in cortinasSecundarias)
            {
                try
                {
                    if (!cortina.IsDisposed)
                    {
                        cortina.CerrarForzado();
                    }
                }
                catch
                {
                    // No interrumpir el cierre de la alerta principal por fallos visuales.
                }
                finally
                {
                    cortina.Dispose();
                }
            }

            if (restauradaParaAlerta && Visible)
            {
                Hide();
            }

            _alertaBloqueanteActiva = false;
        }
    }

    // A diferencia de RestaurarDesdeTray, no recalcula el panel ni captura foto.
    private bool RestaurarParaAlerta(Rectangle areaTrabajo)
    {
        if (Visible && WindowState != FormWindowState.Minimized)
        {
            return false;
        }

        _pnlMarcaje.Visible = false;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Normal;
        Location = new Point(
            areaTrabajo.Left + (areaTrabajo.Width - Width) / 2,
            areaTrabajo.Top + (areaTrabajo.Height - Height) / 2);
        Show();
        return true;
    }

    private void DeterminarSiguienteMarcaje()
    {
        var ultimo = _repository.ObtenerUltimoRegistroDelDia(_idTrabajadorActual, DateTime.Now);

        if (ultimo == null)
        {
            _tipoRegistroActual = 1; // Entrada
        }
        else
        {
            if ((DateTime.Now - ultimo.FechaHoraCaptura).TotalMinutes < 5)
            {
                _tipoRegistroActual = -1; // Espera de 5 minutos
            }
            else
            {
                _tipoRegistroActual = ultimo.TipoRegistro switch
                {
                    1 => 2, // Salida comida
                    2 => 3, // Regreso comida
                    3 => 4, // Salida final
                    _ => 0  // Jornada completa
                };
            }
        }
    }

    private void MostrarPanelMarcaje()
    {
        _lblTrabajadorInfo.Text = $"Colaborador: {_nombreTrabajadorActual} (ID: {_idTrabajadorActual})";
        _lblMotivoBloqueo.Visible = false;

        if (!AlertScheduler.EsDiaLaboral(DateTime.Now))
        {
            _lblTipoMarcajeSiguiente.Text = "No se permiten marcajes los sábados ni domingos.";
            _btnRegistrarAsistencia.Enabled = false;
            _pnlMarcaje.Visible = true;
            return;
        }

        switch (_tipoRegistroActual)
        {
            case 1:
                _lblTipoMarcajeSiguiente.Text = "Evento actual: Entrada";
                _btnRegistrarAsistencia.Enabled = true;
                break;
            case 2:
                _lblTipoMarcajeSiguiente.Text = "Evento actual: Salida a comer";
                _btnRegistrarAsistencia.Enabled = true;
                break;
            case 3:
                _lblTipoMarcajeSiguiente.Text = "Evento actual: Entrada de comer";
                _btnRegistrarAsistencia.Enabled = true;
                break;
            case 4:
                _lblTipoMarcajeSiguiente.Text = "Evento actual: Salida";
                _btnRegistrarAsistencia.Enabled = true;
                break;
            case -1:
                _lblTipoMarcajeSiguiente.Text = "Debe esperar al menos 5 minutos entre registros consecutivos.";
                _btnRegistrarAsistencia.Enabled = false;
                break;
            case 0:
            default:
                _lblTipoMarcajeSiguiente.Text = "Jornada completa. Los 4 registros del día ya fueron realizados.";
                _btnRegistrarAsistencia.Enabled = false;
                break;
        }

        AplicarBloqueoEntradaAnticipada();

        var preview = _webcamService.CapturarFoto(_idTrabajadorActual, Environment.MachineName);
        if (preview.FotoJpeg.Length > 0)
        {
            using var ms = new MemoryStream(preview.FotoJpeg);
            _picCamaraPreview.Image = Image.FromStream(ms);
        }

        if (!preview.IsSuccess)
        {
            _lblSyncStatus.Text = $"Cámara no disponible: {preview.Motivo}";
            RegistrarDiagnosticoCaptura("PreviewMain", preview, "WARN");
        }
        else if (preview.EsFallback)
        {
            _lblSyncStatus.Text = $"Evidencia en modo fallback: {preview.ToUiSummary()}";
            RegistrarDiagnosticoCaptura("PreviewMain", preview, "WARN");
        }
        else
        {
            _lblSyncStatus.Text = $"Cámara lista: {preview.ToUiSummary()}";
            if (_webcamService.DebugCaptureEnabled)
            {
                RegistrarDiagnosticoCaptura("PreviewMain", preview, "INFO");
            }
        }

        _pnlMarcaje.Visible = true;
        _btnRegistrarAsistencia.Focus();
    }

    private async Task RegistrarAsistenciaAsync()
    {
        if (!AlertScheduler.EsDiaLaboral(DateTime.Now))
        {
            MostrarPanelMarcaje();
            MessageBox.Show(
                "No se permiten registrar marcajes los sábados ni domingos.",
                "Día no laborable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (_tipoRegistroActual <= 0) return;

        DeterminarSiguienteMarcaje();
        if (_tipoRegistroActual <= 0)
        {
            MostrarPanelMarcaje();
            return;
        }

        if (_needPin && _tipoRegistroActual is 2 or 3 or 4 && !TieneSesionActivaValida())
        {
            MessageBox.Show(
                "No hay sesión activa válida para registrar este tipo de marcaje.\nReingrese su PIN para reautenticar.",
                "Reautenticación requerida",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _txtPin.Focus();
            return;
        }

        if (DebeBloquearEntradaAnticipada(_tipoRegistroActual, DateTime.Now, out var mensajeAnticipado))
        {
            _btnRegistrarAsistencia.Enabled = true;
            MostrarPanelMarcaje();
            MessageBox.Show(
                mensajeAnticipado,
                "Marcaje no disponible",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _btnRegistrarAsistencia.Enabled = false;

        var captura = _webcamService.CapturarFoto(_idTrabajadorActual, Environment.MachineName);
        DateTime ahora = DateTime.Now;
        if (!AlertScheduler.EsDiaLaboral(ahora))
        {
            MostrarPanelMarcaje();
            MessageBox.Show(
                "El día laboral terminó durante la captura. No se guardó ningún marcaje.",
                "Día no laborable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!captura.IsSuccess)
        {
            RegistrarDiagnosticoCaptura("RegistroManual", captura, "WARN");
            MessageBox.Show(
                $"No se pudo obtener la foto de evidencia.\n\nDetalle: {captura.Motivo}\nEstado: {captura.Status}\nDispositivo: {captura.DeviceId ?? "N/D"}",
                "Captura de cámara no disponible",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _btnRegistrarAsistencia.Enabled = true;
            return;
        }

        byte[] foto = captura.FotoJpeg;
        if (_webcamService.DebugCaptureEnabled || captura.EsFallback)
        {
            RegistrarDiagnosticoCaptura("RegistroManual", captura, captura.EsFallback ? "WARN" : "INFO");
        }

        int pinOperacion = ObtenerPinParaTipoRegistro(_tipoRegistroActual);
        if (_needPin && pinOperacion <= 0)
        {
            _btnRegistrarAsistencia.Enabled = true;
            MessageBox.Show(
                "No fue posible resolver el PIN de operación para este marcaje.",
                "Validación",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _txtPin.Focus();
            return;
        }

        var regLocal = new RegistroAsistenciaLocal
        {
            GuidRegistro = Guid.NewGuid().ToString(),
            IDTrabajador = _idTrabajadorActual,
            PIN = pinOperacion,
            FechaHoraCaptura = ahora,
            TipoRegistro = _tipoRegistroActual,
            IPRegistro = "127.0.0.1",
            NBEquipo = Environment.MachineName,
            Foto = foto,
            EsFotoReal = !captura.EsFallback,
            MotivoFallback = captura.EsFallback ? captura.Motivo : null,
            CameraStatus = captura.Status.ToString(),
            CameraDeviceId = captura.DeviceId,
            EstadoSync = 0
        };

        ahora = DateTime.Now;
        if (!AlertScheduler.EsDiaLaboral(ahora))
        {
            MostrarPanelMarcaje();
            MessageBox.Show(
                "El día laboral terminó antes de guardar el marcaje. No se guardó ningún registro.",
                "Día no laborable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        regLocal.FechaHoraCaptura = ahora;
        regLocal.HashLocal = GenerarHashSha256($"{_idTrabajadorActual}|{ahora:o}|{_tipoRegistroActual}");
        regLocal.IdLocal = _repository.InsertarRegistroLocal(regLocal);
        _repository.ActualizarSesionActividad(Environment.MachineName, _tipoRegistroActual);

        var reqDto = new MarcajeRequestDto
        {
            GuidRegistro = regLocal.GuidRegistro,
            IdTrabajador = regLocal.IDTrabajador,
            Pin = regLocal.PIN,
            FechaHoraCaptura = regLocal.FechaHoraCaptura,
            TipoRegistro = regLocal.TipoRegistro,
            IpRegistro = regLocal.IPRegistro,
            NbEquipo = regLocal.NBEquipo,
            FotoBase64 = foto.Length > 0 ? Convert.ToBase64String(foto) : null
        };

        var respOnline = await _apiClient.RegistrarMarcajeOnlineAsync(reqDto);
        if (respOnline != null && respOnline.Exito == 1)
        {
            _repository.ActualizarEstadoSync(regLocal.IdLocal, 2);
            MessageBox.Show(
                $"¡Asistencia registrada y sincronizada en el servidor!\nTipo: {_lblTipoMarcajeSiguiente.Text}\nEvidencia: {captura.ToUiSummary()}",
                "Éxito",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else if (respOnline != null && respOnline.Exito == 2)
        {
            _repository.ActualizarEstadoSync(regLocal.IdLocal, 3, respOnline.Mensaje);
            MessageBox.Show(
                $"La API rechazó el registro por regla de negocio:\n\n{respOnline.Mensaje}",
                "Validación de Negocio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        else
        {
            MessageBox.Show(
                $"Marcaje guardado de forma local (Offline).\nSe sincronizará en segundo plano al recuperar la conexión.\nTipo: {_lblTipoMarcajeSiguiente.Text}\nEvidencia: {captura.ToUiSummary()}",
                "Guardado Local",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        if (_tipoRegistroActual == 4)
        {
            if (_needPin)
            {
                _repository.CerrarSesionActiva(Environment.MachineName);
                _sesionActivaActual = null;
                _trabajadorCacheActual = null;
                _repository.RegistrarLog("INFO", "Sesion", $"Sesión cerrada por salida final. Trabajador={_idTrabajadorActual}");
            }
            else
            {
                _repository.ActualizarSesionActividad(Environment.MachineName, 4);
                _repository.RegistrarLog("INFO", "Sesion", $"Salida final registrada en modo autónomo. Trabajador={_idTrabajadorActual}");
            }
        }

        _pnlMarcaje.Visible = false;
        if (_needPin)
        {
            _txtPin.Clear();
            _txtPin.Focus();
        }
        else
        {
            DeterminarSiguienteMarcaje();
            MostrarPanelMarcaje();
            MinimizarATray();
        }
        _btnRegistrarAsistencia.Enabled = true;
    }

    private void MinimizarATray()
    {
        Hide();
        ShowInTaskbar = false;
        _notifyIcon.ShowBalloonTip(2000, "Checador Activo", "La aplicación continúa en segundo plano para notificar sus horarios de asistencia.", ToolTipIcon.Info);
    }

    // Lo invoca el hilo que escucha el intento de abrir una segunda instancia.
    public void MostrarDesdeOtraInstancia()
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (_alertaBloqueanteActiva)
            {
                return;
            }

            RestaurarDesdeTray();
            Activate();
        }));
    }

    private void RestaurarDesdeTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        BringToFront();
        DeterminarSiguienteMarcaje();
        if (_trabajadorCacheActual != null && (!_needPin || TieneSesionActivaValida()))
        {
            MostrarPanelMarcaje();
        }
        else
        {
            _pnlMarcaje.Visible = false;
            if (_needPin)
            {
                _txtPin.Focus();
            }
        }
    }

    private void IntentarRecuperarSesionActiva(int idTrabajadorVinculado)
    {
        var sesion = _repository.ObtenerSesionActiva(Environment.MachineName);
        if (sesion == null)
        {
            return;
        }

        if (sesion.IdTrabajador != idTrabajadorVinculado)
        {
            _repository.InvalidarSesionActiva(Environment.MachineName, "Trabajador vinculado distinto al de sesión.");
            return;
        }

        if ((DateTime.Now - sesion.FechaUltimaActividad).TotalHours > SessionExpiryHours)
        {
            _repository.InvalidarSesionActiva(Environment.MachineName, $"Sesión expirada por inactividad > {SessionExpiryHours} horas.");
            return;
        }

        _sesionActivaActual = sesion;
        _trabajadorCacheActual = new WorkerCacheItem
        {
            IdTrabajador = sesion.IdTrabajador,
            Pin = sesion.Pin,
            NombreCompleto = sesion.NombreCompleto,
            HoraEntrada = sesion.HoraEntrada,
            HoraSalidaComida = sesion.HoraSalidaComida,
            HoraEntradaComida = sesion.HoraEntradaComida,
            HoraSalida = sesion.HoraSalida
        };
        _nombreTrabajadorActual = sesion.NombreCompleto;
        _repository.RegistrarLog("INFO", "Sesion", $"Sesión activa recuperada tras reinicio. Trabajador={sesion.IdTrabajador}");
    }

    private bool TieneSesionActivaValida()
    {
        if (_sesionActivaActual == null || !_sesionActivaActual.Activa)
        {
            return false;
        }

        if (_sesionActivaActual.IdTrabajador != _idTrabajadorActual)
        {
            return false;
        }

        return (DateTime.Now - _sesionActivaActual.FechaUltimaActividad).TotalHours <= SessionExpiryHours;
    }

    private int ObtenerPinParaTipoRegistro(int tipoRegistro)
    {
        if (!_needPin)
        {
            return 0;
        }

        if (tipoRegistro == 1)
        {
            return int.TryParse(_txtPin.Text.Trim(), out int pinEntrada) ? pinEntrada : 0;
        }

        if (TieneSesionActivaValida() && _sesionActivaActual != null)
        {
            return _sesionActivaActual.Pin;
        }

        return 0;
    }

    private bool DebeBloquearEntradaAnticipada(int tipoRegistro, DateTime ahora, out string mensaje)
    {
        mensaje = string.Empty;

        if (tipoRegistro is not (1 or 3))
        {
            return false;
        }

        if (_trabajadorCacheActual == null)
        {
            return false;
        }

        string? horarioProgramado = tipoRegistro == 1
            ? _trabajadorCacheActual.HoraEntrada
            : _trabajadorCacheActual.HoraEntradaComida;

        if (string.IsNullOrWhiteSpace(horarioProgramado) || !TimeSpan.TryParse(horarioProgramado, out var horaObjetivo))
        {
            return false;
        }

        if (ahora.TimeOfDay >= horaObjetivo)
        {
            return false;
        }

        string evento = ObtenerNombreEvento(tipoRegistro);
        mensaje = $"Aún no es momento de registrar {evento}. Horario programado: {horaObjetivo:hh\\:mm}.";
        return true;
    }

    private void AplicarBloqueoEntradaAnticipada()
    {
        if (DebeBloquearEntradaAnticipada(_tipoRegistroActual, DateTime.Now, out var mensaje))
        {
            _btnRegistrarAsistencia.Enabled = false;
            _lblMotivoBloqueo.Text = mensaje;
            _lblMotivoBloqueo.Visible = true;
            return;
        }

        _lblMotivoBloqueo.Visible = false;
    }

    // Solo rehabilita el botón si el único impedimento era la hora anticipada.
    private void RefrescarBloqueoEntrada()
    {
        if (!_lblMotivoBloqueo.Visible || !_pnlMarcaje.Visible)
        {
            return;
        }

        AplicarBloqueoEntradaAnticipada();
        if (!_lblMotivoBloqueo.Visible)
        {
            _btnRegistrarAsistencia.Enabled = true;
        }
    }

    private static string ObtenerNombreEvento(int tipoRegistro)
    {
        return tipoRegistro switch
        {
            1 => "Entrada",
            2 => "Salida a comer",
            3 => "Entrada de comer",
            4 => "Salida",
            _ => "registro"
        };
    }

    private bool SolicitarAutorizacionAdministradorParaSalida()
    {
        using var adminForm = new AdminLoginForm(_apiClient);
        if (adminForm.ShowDialog(this) == DialogResult.OK)
        {
            return true;
        }

        MessageBox.Show(
            "Se requiere autorización de administrador para cerrar la aplicación desde la bandeja.",
            "Autorización Requerida",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);

        return false;
    }

    private void SalirAplicacion(bool requiereAutorizacionAdmin = true)
    {
        if (requiereAutorizacionAdmin && !SolicitarAutorizacionAdministradorParaSalida())
        {
            return;
        }

        var confirm = MessageBox.Show("¿Está seguro de que desea salir del Checador de Asistencia?\nLas notificaciones programadas se suspenderán.", "Confirmar Salida", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            DetenerCentinelaGuard();
            _syncManager.Detener();
            DetenerSchedulerAlertas("SalidaAplicacion");
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Exit();
        }
    }

    private void DetenerCentinelaGuard()
    {
        try
        {
            var guardProcesses = System.Diagnostics.Process.GetProcessesByName(GuardProcessName);
            foreach (var process in guardProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch
                {
                    // Continuar con los demas procesos para no bloquear el cierre autorizado.
                }
            }

            _repository.RegistrarLog("INFO", "GuardStartup", $"{GuardProcessName} detenido de forma autorizada. ProcesosFinalizados={guardProcesses.Length}");
        }
        catch (Exception ex)
        {
            _repository.RegistrarLog("WARN", "GuardStartup", $"No fue posible detener {GuardProcessName} durante salida autorizada. Detalle={ex.Message}");
        }
    }

    private async Task DesvincularEquipoAsync()
    {
        using var adminForm = new AdminLoginForm(_apiClient);
        if (adminForm.ShowDialog() != DialogResult.OK)
            return;

        var confirm = MessageBox.Show($"¿Desea desvincular este equipo ({Environment.MachineName})?", "Confirmar Desvinculación", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        var res = await _apiClient.DeleteBindingAsync(Environment.MachineName, adminForm.UsuarioAutenticado);
        if (res != null && res.Exito == 1)
        {
            // Desactivar vínculo en SQLite local
            _repository.DesvincularLocal(Environment.MachineName);

            MessageBox.Show("Equipo desvinculado con éxito en el servidor y de forma local.\nLa aplicación se cerrará.", "Desvinculación Completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SalirAplicacion(requiereAutorizacionAdmin: false);
        }
        else
        {
            MessageBox.Show(res?.Mensaje ?? "No fue posible desvincular el equipo en el servidor.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GenerarHashSha256(string entrada)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(entrada));
        return Convert.ToHexString(bytes).ToLower();
    }

    private void RegistrarDiagnosticoCaptura(string contexto, PhotoCaptureResult captura, string nivel)
    {
        _repository.RegistrarLog(
            nivel,
            "Camara",
            $"Contexto={contexto}; Estado={captura.Status}; Provider={captura.Provider}; Backend={captura.Backend}; Device={captura.DeviceId ?? "N/D"}; DuracionMs={captura.CaptureDurationMs}; Fallback={captura.EsFallback}; Motivo={captura.Motivo}");
    }

}
