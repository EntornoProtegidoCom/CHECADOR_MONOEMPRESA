using System.Diagnostics;

namespace ChecadorGuard;

static class Program
{
    private const string TargetProcessName = "ChecadorDesktop";
    private const int CheckIntervalMs = 2000; // Evalua cada 2 segundos

    static void Main(string[] args)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string targetExePath = Path.Combine(baseDir, $"{TargetProcessName}.exe");

        // Bucle de supervision continuo
        while (true)
        {
            try
            {
                var processes = Process.GetProcessesByName(TargetProcessName);

                if (processes.Length == 0)
                {
                    if (File.Exists(targetExePath))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = targetExePath,
                            UseShellExecute = true,
                            WorkingDirectory = baseDir
                        };
                        Process.Start(psi);
                    }
                }
            }
            catch
            {
                // Manejo silencioso de excepciones para evitar que el centinela muera
            }

            Thread.Sleep(CheckIntervalMs);
        }
    }
}
