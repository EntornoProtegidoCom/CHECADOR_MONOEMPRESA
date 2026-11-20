using System;
using System.Collections.Generic;

namespace ChecadorWeb.Data.Entities;

public partial class SS_USUARIO
{
    public string ID_USUARIO { get; set; } = null!;

    public int ID_PERFIL { get; set; }

    public string? NB_NOMBRE { get; set; }

    public string? NB_PATERNO { get; set; }

    public string? NB_MATERNO { get; set; }

    public string? TX_PASSWORD { get; set; }

    public string? NB_EMAIL { get; set; }

    public string? ST_VIGENTE { get; set; }
}
