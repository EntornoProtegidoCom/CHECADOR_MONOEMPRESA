using System;
using System.Collections.Generic;

namespace ChecadorWeb.Data.Entities;


public partial class DiaFestivo
{
    public int ID { get; set; }

    public DateTime Fecha { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public bool EsOficial { get; set; } = true;
    public bool Estatus { get; set; } = true;

    public DateTime create_time { get; set; } = DateTime.Now;
    public string? Create_ID_USUARIO { get; set; }
    public DateTime update_time { get; set; } = DateTime.Now;
    public string? Update_ID_USUARIO { get; set; }
}
