using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ChecadorWeb.Data;

public static class TransaccionExtensions
{
    /// <summary>
    /// Inicia una transacción propia o se une a la que ya esté activa en el contexto.
    /// Solo la transacción propia se confirma; así una operación puede formar parte de una unidad mayor.
    /// </summary>
    public static async Task<TransaccionPropia> IniciarTransaccionAsync(this DatabaseFacade db, IsolationLevel nivel, CancellationToken ct)
    {
        if (db.CurrentTransaction is not null) return new TransaccionPropia(null);
        return new TransaccionPropia(await db.BeginTransactionAsync(nivel, ct));
    }
}

public sealed class TransaccionPropia(IDbContextTransaction? transaccion) : IAsyncDisposable
{
    public Task ConfirmarAsync(CancellationToken ct) => transaccion?.CommitAsync(ct) ?? Task.CompletedTask;

    public ValueTask DisposeAsync() => transaccion?.DisposeAsync() ?? ValueTask.CompletedTask;
}
