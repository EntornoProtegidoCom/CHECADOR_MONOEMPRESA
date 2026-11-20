using Microsoft.Extensions.Options;

namespace ChecadorWeb.Tests;

public sealed class MonitorFijo<T>(T valor) : IOptionsMonitor<T>
{
    public T CurrentValue => valor;
    public T Get(string? name) => valor;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
