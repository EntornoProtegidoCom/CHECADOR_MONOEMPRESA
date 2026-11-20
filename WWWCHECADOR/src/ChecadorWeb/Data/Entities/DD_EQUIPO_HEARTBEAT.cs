using System;
using System.Collections.Generic;

namespace ChecadorWeb.Data.Entities;

public partial class DD_EQUIPO_HEARTBEAT
{
    public string NBEquipo { get; set; } = null!;

    public int IDTrabajador { get; set; }

    public DateTime UltimoHeartbeat { get; set; }

    public string? IPRegistro { get; set; }

    public string? VersionApp { get; set; }

    public string EstadoEstacion { get; set; } = null!;
}
