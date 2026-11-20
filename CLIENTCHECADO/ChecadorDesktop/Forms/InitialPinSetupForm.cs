using ChecadorDesktop.Data;
using ChecadorDesktop.Models;
using ChecadorDesktop.Services;

namespace ChecadorDesktop.Forms;

public class InitialPinSetupForm : Form
{
    private readonly ApiClientService _apiClient;
    private readonly SQLiteRepository _repository;
    private readonly int _idTrabajador;
    private readonly string _nombreTrabajador;

    private TextBox _txtPin = null!;
    private Button _btnSincronizar = null!;
    private Button _btnCancelar = null!;
    private Label _lblStatus = null!;

    public WorkerCacheItem? TrabajadorSincronizado { get; private set; }

    public InitialPinSetupForm(
        ApiClientService apiClient,
        SQLiteRepository repository,
        int idTrabajador,
        string nombreTrabajador)
    {
        _apiClient = apiClient;
        _repository = repository;
        _idTrabajador = idTrabajador;
        _nombreTrabajador = nombreTrabajador;

        ConstruirUI();
    }

    private void ConstruirUI()
    {
        Text = "Configuración Inicial de Horarios";
        Size = new Size(460, 290);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        BackColor = Color.FromArgb(248, 250, 252);

        var lblTit = new Label
        {
            Text = "Sincronización de Horarios (Única Ocasión)",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 16),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = "Para que el Modo Autónomo funcione sin solicitar PIN en cada marcaje, se requiere ingresar su PIN por única vez para descargar y registrar sus horarios oficiales de jornada.",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(20, 44),
            Size = new Size(405, 45)
        };

        var pnlInfo = new Panel
        {
            Location = new Point(20, 95),
            Size = new Size(405, 36),
            BackColor = Color.FromArgb(241, 245, 249),
            BorderStyle = BorderStyle.FixedSingle
        };
        var lblEmpleado = new Label
        {
            Text = $"Colaborador: {_nombreTrabajador} (ID: {_idTrabajador})",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(10, 8),
            AutoSize = true
        };
        pnlInfo.Controls.Add(lblEmpleado);

        var lblPin = new Label
        {
            Text = "Ingrese su PIN:",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 148),
            AutoSize = true
        };

        _txtPin = new TextBox
        {
            Location = new Point(135, 145),
            Width = 130,
            Font = new Font("Segoe UI", 10.5f),
            UseSystemPasswordChar = true,
            MaxLength = 8
        };

        _btnSincronizar = new Button
        {
            Text = "Sincronizar ➜",
            Location = new Point(280, 143),
            Width = 145,
            Height = 30,
            BackColor = Color.FromArgb(15, 118, 110),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnSincronizar.Click += async (s, e) => await SincronizarHorariosAsync();

        _lblStatus = new Label
        {
            Location = new Point(20, 185),
            Size = new Size(405, 20),
            ForeColor = Color.FromArgb(185, 28, 28),
            Font = new Font("Segoe UI", 8.5f)
        };

        _btnCancelar = new Button
        {
            Text = "Cancelar",
            Location = new Point(325, 210),
            Width = 100,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        _btnCancelar.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.AddRange(new Control[] { lblTit, lblDesc, pnlInfo, lblPin, _txtPin, _btnSincronizar, _lblStatus, _btnCancelar });
        AcceptButton = _btnSincronizar;
        CancelButton = _btnCancelar;
    }

    private async Task SincronizarHorariosAsync()
    {
        if (!int.TryParse(_txtPin.Text.Trim(), out int pin) || pin <= 0)
        {
            _lblStatus.Text = "Por favor ingrese su PIN numérico.";
            _txtPin.Focus();
            return;
        }

        _btnSincronizar.Enabled = false;
        _btnCancelar.Enabled = false;
        _lblStatus.ForeColor = Color.FromArgb(2, 132, 199);
        _lblStatus.Text = "Descargando horarios oficiales...";

        var respApi = await _apiClient.WorkerLoginAsync(_idTrabajador, pin);
        if (respApi != null && respApi.Exito == 1 && respApi.Data != null)
        {
            TrabajadorSincronizado = new WorkerCacheItem
            {
                IdTrabajador = _idTrabajador,
                Pin = pin,
                NombreCompleto = respApi.Data.NombreCompleto,
                HoraEntrada = respApi.Data.HoraEntrada,
                HoraSalidaComida = respApi.Data.HoraSalidaComida,
                HoraEntradaComida = respApi.Data.HoraEntradaComida,
                HoraSalida = respApi.Data.HoraSalida
            };

            _repository.GuardarWorkerCache(TrabajadorSincronizado);
            _repository.RegistrarLog("INFO", "GuardStartup", $"Horarios sincronizados por única ocasión para trabajador={_idTrabajador}. Entrada={TrabajadorSincronizado.HoraEntrada}, Salida={TrabajadorSincronizado.HoraSalida}");

            MessageBox.Show(
                $"Horarios oficiales sincronizados exitosamente:\n\n" +
                $"• Entrada: {TrabajadorSincronizado.HoraEntrada ?? "N/D"}\n" +
                $"• Salida Comida: {TrabajadorSincronizado.HoraSalidaComida ?? "N/D"}\n" +
                $"• Entrada Comida: {TrabajadorSincronizado.HoraEntradaComida ?? "N/D"}\n" +
                $"• Salida Final: {TrabajadorSincronizado.HoraSalida ?? "N/D"}\n\n" +
                "El sistema operará ahora de forma autónoma sin volver a solicitar su PIN.",
                "Sincronización Completa",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
            _lblStatus.Text = respApi?.Mensaje ?? "PIN incorrecto o sin conexión con la API.";
            _btnSincronizar.Enabled = true;
            _btnCancelar.Enabled = true;
            _txtPin.SelectAll();
            _txtPin.Focus();
        }
    }
}
