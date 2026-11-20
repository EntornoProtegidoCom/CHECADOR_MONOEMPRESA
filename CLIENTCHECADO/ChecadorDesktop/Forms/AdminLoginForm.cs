using ChecadorDesktop.Services;

namespace ChecadorDesktop.Forms;

public class AdminLoginForm : Form
{
    private readonly ApiClientService _apiClient;
    private TextBox _txtUsuario = null!;
    private TextBox _txtPassword = null!;
    private Button _btnAceptar = null!;
    private Button _btnCancelar = null!;
    private Label _lblStatus = null!;

    public string UsuarioAutenticado { get; private set; } = string.Empty;

    public AdminLoginForm(ApiClientService apiClient)
    {
        _apiClient = apiClient;
        ConstruirUI();
    }

    private void ConstruirUI()
    {
        Text = "Autorización de Administrador";
        Size = new Size(380, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 247, 250);

        var lblTit = new Label { Text = "Credenciales de Administrador", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(25, 20), AutoSize = true, ForeColor = Color.FromArgb(33, 37, 41) };
        var lblU = new Label { Text = "Usuario:", Location = new Point(25, 60), AutoSize = true, Font = new Font("Segoe UI", 9) };
        _txtUsuario = new TextBox { Location = new Point(120, 57), Width = 210, Font = new Font("Segoe UI", 9) };

        var lblP = new Label { Text = "Contraseña:", Location = new Point(25, 95), AutoSize = true, Font = new Font("Segoe UI", 9) };
        _txtPassword = new TextBox { Location = new Point(120, 92), Width = 210, UseSystemPasswordChar = true, Font = new Font("Segoe UI", 9) };

        _lblStatus = new Label { Location = new Point(25, 125), Size = new Size(310, 20), ForeColor = Color.Red, Font = new Font("Segoe UI", 8.5f) };

        _btnAceptar = new Button { Text = "Autorizar", Location = new Point(135, 150), Width = 95, Height = 32, BackColor = Color.FromArgb(0, 122, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnCancelar = new Button { Text = "Cancelar", Location = new Point(235, 150), Width = 95, Height = 32, FlatStyle = FlatStyle.Flat };

        _btnAceptar.Click += async (s, e) => await ValidarAdminAsync();
        _btnCancelar.Click += (s, e) => DialogResult = DialogResult.Cancel;
        _lblStatus.TextChanged += (s, e) =>
        {
            Size statusTextSize = TextRenderer.MeasureText(
                _lblStatus.Text,
                _lblStatus.Font,
                new Size(_lblStatus.Width, int.MaxValue),
                TextFormatFlags.WordBreak);
            _lblStatus.Height = Math.Max(20, statusTextSize.Height + 4);

            int buttonTop = Math.Max(150, _lblStatus.Bottom + 12);
            _btnAceptar.Top = buttonTop;
            _btnCancelar.Top = buttonTop;
            ClientSize = new Size(ClientSize.Width, Math.Max(ClientSize.Height, _btnAceptar.Bottom + 35));
        };

        Controls.AddRange(new Control[] { lblTit, lblU, _txtUsuario, lblP, _txtPassword, _lblStatus, _btnAceptar, _btnCancelar });
        AcceptButton = _btnAceptar;
        CancelButton = _btnCancelar;
    }

    private async Task ValidarAdminAsync()
    {
        string u = _txtUsuario.Text.Trim();
        string p = _txtPassword.Text.Trim();

        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
        {
            _lblStatus.Text = "Ingrese usuario y contraseña.";
            return;
        }

        _btnAceptar.Enabled = false;
        _lblStatus.ForeColor = Color.DarkBlue;
        _lblStatus.Text = "Validando con la API...";

        var resp = await _apiClient.AdminLoginAsync(u, p);
        if (resp != null && resp.Exito == 1)
        {
            UsuarioAutenticado = u;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblStatus.ForeColor = Color.Red;
            _lblStatus.Text = resp?.Mensaje ?? "Credenciales inválidas.";
            _btnAceptar.Enabled = true;
        }
    }
}
