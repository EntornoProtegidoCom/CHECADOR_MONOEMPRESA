using System;
using System.Collections.Generic;

namespace ChecadorWeb.Data.Entities;

public partial class MM_TRABAJADOR_VACACIONE
{
    public int ID { get; set; }

    public int IDTrabajador { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime FechaFin { get; set; }

    public int DiasTomados { get; set; }

    public int? PeriodoAnio { get; set; }

    public string? Observaciones { get; set; }

    public bool Estatus { get; set; } = true;

    public DateTime create_time { get; set; } = DateTime.Now;

    public string? Create_ID_USUARIO { get; set; }

    public DateTime update_time { get; set; } = DateTime.Now;

    public string? Update_ID_USUARIO { get; set; }

    public virtual MM_TRABAJADORE IDTrabajadorNavigation { get; set; } = null!;
}
