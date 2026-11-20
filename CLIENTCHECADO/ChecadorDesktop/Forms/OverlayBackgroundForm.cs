using System.Drawing;
using System.Windows.Forms;

namespace ChecadorDesktop.Forms;

public class OverlayBackgroundForm : Form
{
    private bool _cierreAutorizado;

    public OverlayBackgroundForm(Screen pantalla)
    {
        // Posicionar exactamente en los limites del monitor secundario.
        StartPosition = FormStartPosition.Manual;
        Bounds = pantalla.Bounds;

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(15, 23, 42);
        Opacity = 0.92;

        var lblMensaje = new Label
        {
            Text = "ATENCION REQUERIDA\n\nPor favor, dirijase a su pantalla principal\npara registrar su asistencia obligatoria.",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };

        Controls.Add(lblMensaje);
    }

    public void CerrarForzado()
    {
        _cierreAutorizado = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Prevenir el cierre manual mediante Alt + F4 o teclado.
        if (!_cierreAutorizado && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }
}
