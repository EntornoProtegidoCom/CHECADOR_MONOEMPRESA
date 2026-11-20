using System;
using System.Collections.Generic;

namespace ChecadorWeb.Data.Entities;

public partial class MM_HORARIO
{
    public int ID { get; set; }

    public int? IDTrabajador { get; set; }

    public DateTime Horaregistro { get; set; }

    public short TipoRegistro { get; set; }

    public string? IPRegistro { get; set; }

    public string? NBEquipo { get; set; }

    public byte[]? Foto { get; set; }

    public byte[]? HashRegistro { get; set; }

    public bool? stEnviado { get; set; }

    public short? NuIntentos { get; set; }

    public virtual MM_TRABAJADORE? IDTrabajadorNavigation { get; set; }
}
