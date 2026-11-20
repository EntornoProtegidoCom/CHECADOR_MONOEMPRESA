using ChecadorDesktop.Data;
using ChecadorDesktop.Models;
using ChecadorDesktop.Services;

namespace ChecadorDesktop.Forms;

public class BindingForm : Form
{
    private readonly ApiClientService _apiClient;
    private readonly SQLiteRepository _repository;
    private TextBox _txtIdTrabajador = null!;
    private TextBox _txtPinTrabajador = null!;
    private Label _lblEquipo = null!;
    private Label _lblStatus = null!;
    private Button _btnVincular = null!;

    public bool VinculadoExitosamente { get; private set; }

    public BindingForm(ApiClientService apiClient, SQLiteRepository repository)
    {
        _apiClient = apiClient;
        _repository = repository;
        ConstruirUI();
    }

    private void ConstruirUI()
    {
        Text = "Vinculación de Estación de Trabajo";
        Size = new Size(430, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 247, 250);

        var lblTit = new Label { Text = "Asignación de Estación", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(25, 18), AutoSize = true, ForeColor = Color.FromArgb(217, 83, 79) };
        var lblDesc = new Label { Text = "Esta computadora debe asociarse al ID del trabajador que la utilizará habitualmente.", Location = new Point(25, 46), Size = new Size(365, 35), Font = new Font("Segoe UI", 8.5f) };

        _lblEquipo = new Label { Text = $"Equipo: {Environment.MachineName}", Location = new Point(25, 88), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };

        var lblTrab = new Label { Text = "ID Trabajador asignado:", Location = new Point(25, 118), AutoSize = true, Font = new Font("Segoe UI", 9) };
        _txtIdTrabajador = new TextBox { Location = new Point(180, 115), Width = 190, Font = new Font("Segoe UI", 9) };

        var lblPin = new Label { Text = "PIN del Trabajador:", Location = new Point(25, 150), AutoSize = true, Font = new Font("Segoe UI", 9) };
        _txtPinTrabajador = new TextBox { Location = new Point(180, 147), Width = 190, Font = new Font("Segoe UI", 9), UseSystemPasswordChar = true, MaxLength = 8 };

        _lblStatus = new Label { Location = new Point(25, 182), AutoSize = true, MaximumSize = new Size(350, 0), ForeColor = Color.Red, Font = new Font("Segoe UI", 8.5f) };

        _btnVincular = new Button { Text = "Vincular Estación", Location = new Point(230, 215), Width = 140, Height = 34, BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
        _btnVincular.Click += async (s, e) => await ProcederVinculacionAsync();
        _lblStatus.TextChanged += (s, e) =>
        {
            _btnVincular.Top = Math.Max(215, _lblStatus.Bottom + 12);
            ClientSize = new Size(ClientSize.Width, Math.Max(ClientSize.Height, _btnVincular.Bottom + 35));
        };

        Controls.AddRange(new Control[] { lblTit, lblDesc, _lblEquipo, lblTrab, _txtIdTrabajador, lblPin, _txtPinTrabajador, _lblStatus, _btnVincular });
        AcceptButton = _btnVincular;
    }

    private async Task ProcederVinculacionAsync()
    {
        if (!int.TryParse(_txtIdTrabajador.Text.Trim(), out int idTrab) || idTrab <= 0)
        {
            _lblStatus.Text = "Ingrese un ID de trabajador válido.";
            return;
        }

        if (!int.TryParse(_txtPinTrabajador.Text.Trim(), out int pinTrab) || pinTrab <= 0)
        {
            _lblStatus.Text = "Ingrese el PIN numérico del trabajador.";
            return;
        }

        // Requiere autorización de administrador
        using var loginAdmin = new AdminLoginForm(_apiClient);
        if (loginAdmin.ShowDialog() != DialogResult.OK)
        {
            _lblStatus.Text = "Se requiere autorización administrativa.";
            return;
        }

        _btnVincular.Enabled = false;
        _lblStatus.ForeColor = Color.DarkBlue;
        _lblStatus.Text = "Validando trabajador y registrando vínculo...";

        // 1. Validar identidad del trabajador y descargar sus horarios
        var workerLogin = await _apiClient.WorkerLoginAsync(idTrab, pinTrab);
        if (workerLogin == null || workerLogin.Exito != 1 || workerLogin.Data == null)
        {
            _lblStatus.ForeColor = Color.Red;
            _lblStatus.Text = workerLogin?.Mensaje ?? "PIN de trabajador incorrecto o sin conexión con la API.";
            _btnVincular.Enabled = true;
            return;
        }

        // 2. Registrar el vínculo en el servidor
        var res = await _apiClient.CreateBindingAsync(Environment.MachineName, idTrab, loginAdmin.UsuarioAutenticado);
        if (res != null && res.Exito == 1)
        {
            string nombreCompleto = workerLogin.Data.NombreCompleto;

            // Guardar vínculo en SQLite local
            _repository.GuardarVinculoLocal(new EstacionVinculoLocal
            {
                NbEquipo = Environment.MachineName,
                IdTrabajador = idTrab,
                NombreTrabajador = nombreCompleto,
                FechaVinculacion = DateTime.Now,
                VigenteHasta = DateTime.MaxValue,
                Activo = true
            });

            // Guardar inmediatamente en WORKER_CACHE para el programador de alertas y modo autónomo
            _repository.GuardarWorkerCache(new WorkerCacheItem
            {
                IdTrabajador = idTrab,
                Pin = pinTrab,
                NombreCompleto = nombreCompleto,
                HoraEntrada = workerLogin.Data.HoraEntrada,
                HoraSalidaComida = workerLogin.Data.HoraSalidaComida,
                HoraEntradaComida = workerLogin.Data.HoraEntradaComida,
                HoraSalida = workerLogin.Data.HoraSalida
            });

            VinculadoExitosamente = true;
            MessageBox.Show($"Equipo vinculado correctamente a {nombreCompleto} (ID: {idTrab}).\nEstación vinculada con horarios sincronizados.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblStatus.ForeColor = Color.Red;
            _lblStatus.Text = res?.Mensaje ?? "Error al registrar el vínculo.";
            _btnVincular.Enabled = true;
        }
    }
}
