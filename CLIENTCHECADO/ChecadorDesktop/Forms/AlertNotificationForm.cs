using System.Security.Cryptography;
using System.Text;
using ChecadorDesktop.Data;
using ChecadorDesktop.Models;
using ChecadorDesktop.Services;
using Microsoft.Extensions.Configuration;

namespace ChecadorDesktop.Forms;

public class AlertNotificationForm : Form
{
    private const int SessionExpiryHours = 12;

    private readonly int _tipoRegistro;
    private readonly string _horaProgramada;
    private readonly WorkerCacheItem _trabajador;
    private readonly SQLiteRepository _repository;
    private readonly ApiClientService _apiClient;
    private readonly WebcamService _webcamService;
    private readonly bool _permitirPosponer;
    private readonly bool _needPin;
    private readonly DateTime _fechaAlerta;
    private readonly System.Windows.Forms.Timer _timerCaducidad = new() { Interval = 1000 };

    private bool _cierreAutorizado = false;
    private long _duracionPreviewMs;
    private readonly System.Diagnostics.Stopwatch _cronometroApertura = System.Diagnostics.Stopwatch.StartNew();
    public bool AlertaPospuesta { get; private set; } = false;
    public bool AlertaVencida { get; private set; }
    public bool AlertaEnDiaNoLaborable { get; private set; }

    // Controles UI
    private PictureBox _picPreview = null!;
    private Button _btnRegistrar = null!;
    private Button _btnPosponer = null!;
    private Label _lblStatus = null!;

    public AlertNotificationForm(
        int tipoRegistro,
        string horaProgramada,
        WorkerCacheItem trabajador,
        SQLiteRepository repository,
        ApiClientService apiClient,
        WebcamService webcamService,
        Rectangle areaTrabajo)
    {
        _tipoRegistro = tipoRegistro;
        _horaProgramada = horaProgramada;
        _trabajador = trabajador;
        _repository = repository;
        _apiClient = apiClient;
        _webcamService = webcamService;
        _fechaAlerta = DateTime.Now.Date;
        _permitirPosponer = CargarPermitirPosponerDesdeConfig();
        _needPin = CargarNeedPinDesdeConfig();

        _timerCaducidad.Tick += (s, e) => ValidarVigencia(DateTime.Now);
        ConstruirUI(areaTrabajo);
    }

    private void ConstruirUI(Rectangle areaTrabajo)
    {
        // 1. Configuración de dimensiones: 60% de la pantalla principal
        Width = (int)(areaTrabajo.Width * 0.60);
        Height = (int)(areaTrabajo.Height * 0.60);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(
            areaTrabajo.Left + (areaTrabajo.Width - Width) / 2,
            areaTrabajo.Top + (areaTrabajo.Height - Height) / 2);

        // 2. Eliminación de bordes y bloqueo
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(248, 250, 252);

        // Header llamativo según criticidad
        bool esCritico = (_tipoRegistro == 1 || _tipoRegistro == 4);
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = esCritico ? Color.FromArgb(185, 28, 28) : Color.FromArgb(2, 132, 199)
        };

        string nombreTipo = _tipoRegistro switch
        {
            1 => "REGISTRO OBLIGATORIO DE ENTRADA",
            2 => "HORARIO DE SALIDA A COMER",
            3 => "HORARIO DE REGRESO DE COMIDA",
            4 => "REGISTRO OBLIGATORIO DE SALIDA",
            _ => "REGISTRO DE ASISTENCIA"
        };

        string tipoCorto = ObtenerTipoRegistroCorto(_tipoRegistro);

        var lblTitulo = new Label
        {
            Text = $"⚠️ ATENCIÓN: {nombreTipo}",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(30, 15),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = esCritico
                ? "Cumplimiento legal Art. 132 LFT: Es obligatorio acreditar el horario laborado."
                : "Favor de checar su horario de alimentos o posponer temporalmente.",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(241, 245, 249),
            Location = new Point(30, 48),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange(new Control[] { lblTitulo, lblSub });

        // Información del colaborador
        var pnlInfo = new Panel { Location = new Point(30, 100), Size = new Size(Width - 60, 75), BackColor = Color.White };
        var lblEmpleado = new Label
        {
            Text = $"Colaborador: {_trabajador.NombreCompleto} (ID: {_trabajador.IdTrabajador})",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(15, 12),
            AutoSize = true
        };
        var lblHorario = new Label
        {
            Text = $"Horario oficial programado: {_horaProgramada}  |  Hora actual: {DateTime.Now:HH:mm:ss}",
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(15, 42),
            AutoSize = true
        };
        pnlInfo.Controls.AddRange(new Control[] { lblEmpleado, lblHorario });

        var lblTipoChequeo = new Label
        {
            Text = $"Tipo de checado a registrar: {tipoCorto}",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(30, 172),
            AutoSize = true
        };

        // Foto preview y captura
        int camWidth = 260;
        int camHeight = 185;
        _picPreview = new PictureBox
        {
            Location = new Point(30, 190),
            Size = new Size(camWidth, camHeight),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(15, 23, 42)
        };

        // Generar fotografía previa de comprobación
        var cronometroPreview = System.Diagnostics.Stopwatch.StartNew();
        var preview = _webcamService.CapturarFoto(_trabajador.IdTrabajador, Environment.MachineName);
        _duracionPreviewMs = cronometroPreview.ElapsedMilliseconds;
        if (preview.FotoJpeg.Length > 0)
        {
            using var ms = new MemoryStream(preview.FotoJpeg);
            _picPreview.Image = Image.FromStream(ms);
        }

        // Botón principal de registro
        _btnRegistrar = new Button
        {
            Text = $"📸 REGISTRAR {tipoCorto} AHORA",
            Location = new Point(315, 190),
            Size = new Size(Width - 345, 60),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnRegistrar.FlatAppearance.BorderSize = 0;
        _btnRegistrar.Click += async (s, e) => await EjecutarMarcajeDesdeAlertaAsync();

        bool mostrarPosponer = PuedePosponerAlerta();

        // Botón de posponer (solo para comida y habilitado por configuración)
        _btnPosponer = new Button
        {
            Text = "⏱️ Posponer alerta (10 minutos)",
            Location = new Point(315, 260),
            Size = new Size(Width - 345, 42),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Visible = mostrarPosponer,
            Enabled = mostrarPosponer,
            Cursor = Cursors.Hand
        };
        _btnPosponer.FlatAppearance.BorderSize = 0;
        _btnPosponer.Click += (s, e) =>
        {
            AlertaPospuesta = true;
            _cierreAutorizado = true;
            DialogResult = DialogResult.Cancel;
            _repository.RegistrarLog("INFO", "Alerta", $"Alerta tipo {_tipoRegistro} pospuesta por usuario.");
            Close();
        };

        _lblStatus = new Label
        {
            Location = new Point(315, 315),
            Size = new Size(Width - 345, 45),
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(100, 116, 139)
        };

        string horariosTexto = ConstruirResumenHorarios();
        var horariosFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        int horariosWidth = Width - 60;
        int alturaTextoHorarios = TextRenderer.MeasureText(
            horariosTexto,
            horariosFont,
            new Size(horariosWidth - 24, 0),
            TextFormatFlags.WordBreak).Height;
        int alturaHorarios = Math.Max(100, alturaTextoHorarios + 18);

        var lblHorariosInfo = new Label
        {
            Location = new Point(30, Height - alturaHorarios - 40),
            Size = new Size(horariosWidth, alturaHorarios),
            Font = horariosFont,
            ForeColor = Color.FromArgb(71, 85, 105),
            BackColor = Color.FromArgb(241, 245, 249),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 8, 12, 8),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Text = horariosTexto
        };

        if (!preview.IsSuccess)
        {
            _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
            _lblStatus.Text = $"⚠ Cámara no disponible: {preview.Motivo}";
            RegistrarDiagnosticoCaptura("PreviewAlerta", preview, "WARN");
        }
        else if (preview.EsFallback)
        {
            _lblStatus.ForeColor = Color.FromArgb(180, 83, 9);
            _lblStatus.Text = $"⚠ Vista previa en modo respaldo: {preview.ToUiSummary()}";
            RegistrarDiagnosticoCaptura("PreviewAlerta", preview, "WARN");
        }
        else
        {
            _lblStatus.ForeColor = Color.FromArgb(51, 65, 85);
            _lblStatus.Text = $"Cámara lista: {preview.ToUiSummary()}";
            if (_webcamService.DebugCaptureEnabled)
            {
                RegistrarDiagnosticoCaptura("PreviewAlerta", preview, "INFO");
            }
        }

        // Borde exterior decorativo
        var pnlBorde = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };

        Controls.AddRange(new Control[] { pnlHeader, pnlInfo, lblTipoChequeo, _picPreview, _btnRegistrar, _btnPosponer, _lblStatus, lblHorariosInfo, pnlBorde });
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Cancelar cualquier intento de cierre mediante Alt + F4 o teclado
        if (!_cierreAutorizado && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _lblStatus.ForeColor = Color.Red;
            _lblStatus.Text = "⚠️ Debe registrar su asistencia para continuar utilizando el equipo.";
        }
        base.OnFormClosing(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _timerCaducidad.Start();
        ValidarVigencia(DateTime.Now);

        Activate();
        var pantalla = Screen.FromControl(this);
        _repository.RegistrarLog(
            "INFO",
            "AlertaDiag",
            $"Tipo={_tipoRegistro}; Bounds={Bounds}; Pantalla={pantalla.DeviceName}; EnPrincipal={pantalla.Primary}; " +
            $"AreaPrincipal={Screen.PrimaryScreen?.WorkingArea}; DeviceDpi={DeviceDpi}; ActivaTrasMostrar={ActiveForm == this}; " +
            $"PreviewMs={_duracionPreviewMs}; DesdeCreacionMs={_cronometroApertura.ElapsedMilliseconds}");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timerCaducidad.Stop();
        _timerCaducidad.Dispose();
        base.OnFormClosed(e);
    }

    private async Task EjecutarMarcajeDesdeAlertaAsync()
    {
        if (!ValidarVigencia(DateTime.Now))
        {
            return;
        }

        _btnRegistrar.Enabled = false;
        _btnPosponer.Enabled = false;
        _lblStatus.ForeColor = Color.FromArgb(2, 132, 199);
        _lblStatus.Text = "Capturando evidencia y registrando asistencia...";

        if (_needPin && _tipoRegistro is 2 or 3 or 4 && !SesionActivaValidaParaTrabajador())
        {
            _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
            _lblStatus.Text = "Sesión expirada o no disponible. Reautentique en pantalla principal.";
            MostrarMensajePrioritario(
                "No hay una sesión activa válida para registrar este tipo de marcaje.\nDebe reautenticarse desde la pantalla principal.",
                "Reautenticación requerida",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _btnRegistrar.Enabled = true;
            _btnPosponer.Enabled = PuedePosponerAlerta();
            return;
        }

        DateTime ahora = DateTime.Now;
        if (!ValidarVigencia(ahora) || BloquearMarcajeDuplicado(ahora))
        {
            return;
        }

        if (DebeBloquearEntradaAnticipada(ahora))
        {
            _btnRegistrar.Enabled = true;
            _btnPosponer.Enabled = PuedePosponerAlerta();
            return;
        }

        var captura = _webcamService.CapturarFoto(_trabajador.IdTrabajador, Environment.MachineName);
        ahora = DateTime.Now;
        if (!ValidarVigencia(ahora))
        {
            return;
        }

        if (!captura.IsSuccess)
        {
            RegistrarDiagnosticoCaptura("RegistroAlerta", captura, "WARN");
            _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
            _lblStatus.Text = $"No se pudo capturar evidencia: {captura.Motivo}";
            _btnRegistrar.Enabled = true;
            _btnPosponer.Enabled = PuedePosponerAlerta();
            MostrarMensajePrioritario(
                $"No se pudo obtener la foto de evidencia para este marcaje.\n\nDetalle: {captura.Motivo}\nEstado: {captura.Status}\nDispositivo: {captura.DeviceId ?? "N/D"}",
                "Captura de cámara no disponible",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (BloquearMarcajeDuplicado(ahora))
        {
            return;
        }

        byte[] foto = captura.FotoJpeg;
        if (_webcamService.DebugCaptureEnabled || captura.EsFallback)
        {
            RegistrarDiagnosticoCaptura("RegistroAlerta", captura, captura.EsFallback ? "WARN" : "INFO");
        }

        int pinMarcaje = _needPin ? _trabajador.Pin : 0;

        var regLocal = new RegistroAsistenciaLocal
        {
            GuidRegistro = Guid.NewGuid().ToString(),
            IDTrabajador = _trabajador.IdTrabajador,
            PIN = pinMarcaje,
            FechaHoraCaptura = ahora,
            TipoRegistro = _tipoRegistro,
            IPRegistro = "127.0.0.1",
            NBEquipo = Environment.MachineName,
            Foto = foto,
            EsFotoReal = !captura.EsFallback,
            MotivoFallback = captura.EsFallback ? captura.Motivo : null,
            CameraStatus = captura.Status.ToString(),
            CameraDeviceId = captura.DeviceId,
            EstadoSync = 0
        };

        // 1. Persistencia Offline-First
        // Validar justo antes de persistir y fijar aquí la hora definitiva del marcaje.
        ahora = DateTime.Now;
        if (!ValidarVigencia(ahora))
        {
            return;
        }

        regLocal.FechaHoraCaptura = ahora;
        regLocal.HashLocal = GenerarHashSha256($"{_trabajador.IdTrabajador}|{ahora:o}|{_tipoRegistro}");
        regLocal.IdLocal = _repository.InsertarRegistroLocal(regLocal);
        _repository.ActualizarSesionActividad(Environment.MachineName, _tipoRegistro);

        if (_tipoRegistro == 4)
        {
            if (_needPin)
            {
                _repository.CerrarSesionActiva(Environment.MachineName);
                _repository.RegistrarLog("INFO", "Sesion", $"Sesión cerrada por salida final desde alerta. Trabajador={_trabajador.IdTrabajador}");
            }
            else
            {
                _repository.ActualizarSesionActividad(Environment.MachineName, 4);
                _repository.RegistrarLog("INFO", "Sesion", $"Salida final registrada en modo autónomo desde alerta. Trabajador={_trabajador.IdTrabajador}");
            }
        }

        // 2. Intento de sincronización online inmediata
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
        if (respOnline != null)
        {
            if (respOnline.Exito == 1)
            {
                _repository.ActualizarEstadoSync(regLocal.IdLocal, 2);
            }
            else if (respOnline.Exito == 2)
            {
                _repository.ActualizarEstadoSync(regLocal.IdLocal, 3, respOnline.Mensaje);
                _repository.RegistrarLog("WARN", "Alerta", $"API rechazó marcaje por regla de negocio: {respOnline.Mensaje}");
            }
        }

        bool ventanaDisponible = !AlertaVencida && !IsDisposed && !Disposing;

        _repository.RegistrarLog("INFO", "Alerta", $"Checada tipo {_tipoRegistro} completada desde ventana de alerta. Evidencia={captura.ToUiSummary()}");

        if (respOnline != null && respOnline.Exito == 2)
        {
            if (ventanaDisponible)
            {
                MostrarMensajePrioritario(
                    $"Atención del servidor:\n{respOnline.Mensaje}\n\nEl marcaje se guardó localmente con observación de regla de negocio.",
                    "Regla de Negocio API",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        else if (ventanaDisponible)
        {
            MostrarMensajePrioritario(
                $"¡Asistencia registrada correctamente!\nTipo: {ObtenerTipoRegistroCorto(_tipoRegistro)}\nEvidencia: {captura.ToUiSummary()}",
                "Completado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // Autorizar desbloqueo y cierre
        if (ventanaDisponible)
        {
            _cierreAutorizado = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private bool ValidarVigencia(DateTime ahora)
    {
        bool mismoDia = ahora.Date == _fechaAlerta;
        bool diaLaborable = AlertScheduler.EsDiaLaboral(ahora);
        if (mismoDia && diaLaborable)
        {
            return true;
        }

        AlertaVencida = !mismoDia;
        AlertaEnDiaNoLaborable = !diaLaborable;
        if (AlertaVencida || AlertaEnDiaNoLaborable)
        {
            _repository.RegistrarLog(
                "INFO",
                "Alerta",
                $"Alerta tipo {_tipoRegistro} cerrada por vigencia. FechaAlerta={_fechaAlerta:yyyy-MM-dd}; FechaActual={ahora:yyyy-MM-dd}; DiaLaborable={diaLaborable}.");
        }

        _cierreAutorizado = true;
        if (!IsDisposed && !Disposing)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        return false;
    }

    private bool BloquearMarcajeDuplicado(DateTime ahora)
    {
        var ultimo = _repository.ObtenerUltimoRegistroDelDia(_trabajador.IdTrabajador, ahora);
        if (ultimo == null ||
            ultimo.TipoRegistro != _tipoRegistro ||
            (ahora - ultimo.FechaHoraCaptura).TotalMinutes >= 5)
        {
            return false;
        }

        _repository.RegistrarLog(
            "WARN",
            "Alerta",
            $"Intento bloqueado por duplicidad local. Tipo={_tipoRegistro}, Minutos={(ahora - ultimo.FechaHoraCaptura).TotalMinutes:F2}");

        _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
        _lblStatus.Text = "Ya existe un registro reciente del mismo tipo. Espere 5 minutos.";

        _btnRegistrar.Enabled = true;
        _btnPosponer.Enabled = PuedePosponerAlerta();

        MostrarMensajePrioritario(
            "Ya existe un registro reciente de este mismo tipo.\nDebe esperar al menos 5 minutos para volver a registrar.",
            "Registro duplicado bloqueado",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return true;
    }

    private bool DebeBloquearEntradaAnticipada(DateTime ahora)
    {
        if (_tipoRegistro is not (1 or 3))
        {
            return false;
        }

        if (!TimeSpan.TryParse(_horaProgramada, out var horaObjetivo) || ahora.TimeOfDay >= horaObjetivo)
        {
            return false;
        }

        string evento = _tipoRegistro == 1 ? "Entrada" : "Entrada de comer";
        string mensaje = $"Aún no es momento de registrar {evento}. Horario programado: {horaObjetivo:hh\\:mm}.";

        _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
        _lblStatus.Text = mensaje;

        MostrarMensajePrioritario(
            mensaje,
            "Marcaje no disponible",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        return true;
    }

    private static string GenerarHashSha256(string entrada)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(entrada));
        return Convert.ToHexString(bytes).ToLower();
    }

    private static string ObtenerTipoRegistroCorto(int tipoRegistro)
    {
        return tipoRegistro switch
        {
            1 => "ENTRADA",
            2 => "SALIDA A COMER",
            3 => "ENTRADA DE COMER",
            4 => "SALIDA FINAL",
            _ => "ASISTENCIA"
        };
    }

    private string ConstruirResumenHorarios()
    {
        return "Horarios asignados del colaborador:\n"
            + $"Entrada: {NormalizarHorario(_trabajador.HoraEntrada)}\n"
            + $"Salida a comer: {NormalizarHorario(_trabajador.HoraSalidaComida)}\n"
            + $"Entrada de comer: {NormalizarHorario(_trabajador.HoraEntradaComida)}\n"
            + $"Salida final: {NormalizarHorario(_trabajador.HoraSalida)}";
    }

    private static string NormalizarHorario(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? "No definido" : valor;
    }

    private void RegistrarDiagnosticoCaptura(string contexto, PhotoCaptureResult captura, string nivel)
    {
        _repository.RegistrarLog(
            nivel,
            "Camara",
            $"Contexto={contexto}; TipoRegistro={_tipoRegistro}; Estado={captura.Status}; Provider={captura.Provider}; Backend={captura.Backend}; Device={captura.DeviceId ?? "N/D"}; DuracionMs={captura.CaptureDurationMs}; Fallback={captura.EsFallback}; Motivo={captura.Motivo}");
    }

    private DialogResult MostrarMensajePrioritario(string mensaje, string titulo, MessageBoxButtons botones, MessageBoxIcon icono)
    {
        bool topMostOriginal = TopMost;

        try
        {
            TopMost = true;
            Activate();
            BringToFront();
            return MessageBox.Show(this, mensaje, titulo, botones, icono);
        }
        finally
        {
            TopMost = topMostOriginal;
            Activate();
        }
    }

    private bool SesionActivaValidaParaTrabajador()
    {
        var sesion = _repository.ObtenerSesionActiva(Environment.MachineName);
        if (sesion == null || !sesion.Activa)
        {
            return false;
        }

        if (sesion.IdTrabajador != _trabajador.IdTrabajador)
        {
            _repository.InvalidarSesionActiva(Environment.MachineName, "Conflicto de trabajador en alerta.");
            return false;
        }

        if ((DateTime.Now - sesion.FechaUltimaActividad).TotalHours > SessionExpiryHours)
        {
            _repository.InvalidarSesionActiva(Environment.MachineName, "Sesión expirada durante alerta.");
            return false;
        }

        return true;
    }

    private bool PuedePosponerAlerta()
    {
        return (_tipoRegistro == 2 || _tipoRegistro == 3) && _permitirPosponer;
    }

    private static bool CargarPermitirPosponerDesdeConfig()
    {
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            return bool.TryParse(config["Posponer"], out bool permitir) ? permitir : true;
        }
        catch
        {
            return true;
        }
    }

    private static bool CargarNeedPinDesdeConfig()
    {
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            return bool.TryParse(config["NeedPin"], out bool np) ? np : true;
        }
        catch
        {
            return true;
        }
    }

}
