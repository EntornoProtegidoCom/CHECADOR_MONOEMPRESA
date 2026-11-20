using System;
using System.Collections.Generic;

namespace ChecadorWeb.Data.Entities;

public partial class DD_EQUIPO_VINCULO
{
    public int ID { get; set; }

    public string NBEquipo { get; set; } = null!;

    public int IDTrabajador { get; set; }

    public bool Activo { get; set; }

    public DateTime create_time { get; set; }

    public string? Create_ID_USUARIO { get; set; }

    public DateTime update_time { get; set; }

    public string? Update_ID_USUARIO { get; set; }

    public virtual MM_TRABAJADORE IDTrabajadorNavigation { get; set; } = null!;
}
