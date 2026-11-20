using System.ComponentModel.DataAnnotations;
using ChecadorWeb.Data;
using ChecadorWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ChecadorWeb.Services;

public sealed record MarcajeHora(DateTime Hora, short Tipo);

public static class CalculadorJornada
{
    public static DetalleJornadaDiaDto Calcular(IEnumerable<MarcajeHora> marcajes)
    {
        var ordenados = marcajes.OrderBy(m => m.Hora).ToArray();
        if (ordenados.Length == 0)
            return new DetalleJornadaDiaDto { EsIncompleto = true, Estatus = "FALTA" };

        var tipos = ordenados.Select(m => m.Tipo).ToArray();
        var valida = tipos.SequenceEqual(new short[] { 1, 4 })
            || tipos.SequenceEqual(new short[] { 1, 2, 3, 4 });
        if (!valida || ordenados.Any(m => m.Hora.Date != ordenados[0].Hora.Date)
            || ordenados.Zip(ordenados.Skip(1)).Any(par => par.First.Hora >= par.Second.Hora))
            return new DetalleJornadaDiaDto { EsIncompleto = true, Estatus = "INCOMPLETO" };

        var comida = ordenados.Length == 4 ? ordenados[2].Hora - ordenados[1].Hora : TimeSpan.Zero;
        return new DetalleJornadaDiaDto
        {
            HorasEfectivas = (ordenados[^1].Hora - ordenados[0].Hora - comida).TotalHours,
            TiempoComida = ordenados.Length == 4 ? comida : null,
        };
    }
}

public interface IResumenHorasService
{
    Task<ReporteHorasResultado> ObtenerAsync(ResumenHorasFiltro filtro, CancellationToken ct = default);
}

public sealed class ResumenHorasService(ChecadorDbContext db) : IResumenHorasService
{
    public async Task<ReporteHorasResultado> ObtenerAsync(ResumenHorasFiltro filtro, CancellationToken ct = default)
    {
        filtro.AplicarPredeterminados();
        Validator.ValidateObject(filtro, new ValidationContext(filtro), validateAllProperties: true);
        var desde = filtro.Desde!.Value;
        var hasta = filtro.Hasta!.Value;
        var inicio = desde.ToDateTime(TimeOnly.MinValue);

        var empleadosQuery = db.MM_TRABAJADOREs.AsNoTracking().Where(t => t.status == true);
        foreach (var termino in EmpleadoService.Terminos(filtro.Trabajador))
        {
            if (int.TryParse(termino, out var id))
                empleadosQuery = empleadosQuery.Where(t => t.IDTrabajador == id);
            else
                empleadosQuery = empleadosQuery.Where(t => t.Nombre.Contains(termino)
                    || t.ApellidoP.Contains(termino) || t.ApellidoM.Contains(termino));
        }

        var empleados = await empleadosQuery
            .OrderBy(t => t.ApellidoP).ThenBy(t => t.ApellidoM).ThenBy(t => t.Nombre).ThenBy(t => t.IDTrabajador)
            .Select(t => new ResumenHorasEmpleadoDto
            {
                IdTrabajador = t.IDTrabajador,
                NombreTrabajador = t.ApellidoP + " " + t.ApellidoM + " " + t.Nombre,
            }).ToListAsync(ct);

        var query = db.MM_HORARIOs.AsNoTracking().Where(h => h.Horaregistro >= inicio);
        if (hasta != DateOnly.MaxValue)
        {
            var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(h => h.Horaregistro < fin);
        }
        var registros = await (from h in query
                               join t in empleadosQuery on h.IDTrabajador equals (int?)t.IDTrabajador
                               select new { t.IDTrabajador, h.Horaregistro, h.TipoRegistro }).ToListAsync(ct);
        var porDia = registros.ToLookup(h => (h.IDTrabajador, h.Horaregistro.Date));
        var resultado = new ReporteHorasResultado(desde, hasta, empleados);
        var fechas = resultado.Fechas;
        foreach (var empleado in empleados)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var fecha in fechas)
                empleado.Dias[fecha] = CalculadorJornada.Calcular(porDia[(empleado.IdTrabajador, fecha)]
                    .Select(h => new MarcajeHora(h.Horaregistro, h.TipoRegistro)));
        }
        return resultado;
    }
}
