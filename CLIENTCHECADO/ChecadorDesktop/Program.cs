using System;
using System.Threading;
using System.Runtime.InteropServices;
using ChecadorDesktop.Forms;

namespace ChecadorDesktop;

static class Program
{
    private const string NombreSenalMostrar = @"Local\EntornoProtegidoCom.ChecadorDesktop.Mostrar";

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    private const uint ASFW_ANY = uint.MaxValue;

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\EntornoProtegidoCom.ChecadorDesktop",
            createdNew: out bool createdNew);

        // Sin escucha previa, la señal queda pendiente y la primera instancia la consume al iniciar.
        var senalMostrar = new EventWaitHandle(false, EventResetMode.AutoReset, NombreSenalMostrar);

        if (!createdNew)
        {
            AllowSetForegroundWindow(ASFW_ANY);
            senalMostrar.Set();
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            var formularioPrincipal = new MainForm();
            IniciarEscuchaSenal(senalMostrar, formularioPrincipal);
            Application.Run(formularioPrincipal);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static void IniciarEscuchaSenal(EventWaitHandle senal, MainForm formulario)
    {
        var hilo = new Thread(() =>
        {
            while (senal.WaitOne())
            {
                formulario.MostrarDesdeOtraInstancia();
            }
        })
        {
            IsBackground = true,
            Name = "ChecadorDesktop.SenalMostrar"
        };
        hilo.Start();
    }
}