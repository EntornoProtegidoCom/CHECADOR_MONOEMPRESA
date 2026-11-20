using System;
using System.Collections.Generic;

namespace ChecadorWeb.Data.Entities;

public partial class MM_TRABAJADORE
{
    public int IDTrabajador { get; set; }

    public string Nombre { get; set; } = null!;

    public string ApellidoP { get; set; } = null!;

    public string ApellidoM { get; set; } = null!;

    public byte[]? Foto { get; set; }

    public int? PIN { get; set; }

    public string Contrasena { get; set; } = null!;

    public string? IDCARD { get; set; }

    public string? Correo { get; set; }

    public bool? status { get; set; }

    public DateTime? create_time { get; set; }

    public string? Create_ID_USUARIO { get; set; }

    public DateTime? update_time { get; set; }

    public string? Update_ID_USUARIO { get; set; }

    public byte[]? Foto2 { get; set; }

    public TimeOnly? HoraEntrada { get; set; }

    public TimeOnly? HoraSalidaComida { get; set; }

    public TimeOnly? HoraEntradaComida { get; set; }

    public TimeOnly? HoraSalida { get; set; }

    public virtual ICollection<DD_EQUIPO_VINCULO> DD_EQUIPO_VINCULOs { get; set; } = new List<DD_EQUIPO_VINCULO>();

    public virtual ICollection<MM_HORARIO> MM_HORARIOs { get; set; } = new List<MM_HORARIO>();

    public virtual ICollection<MM_TRABAJADOR_VACACIONE> MM_TRABAJADOR_VACACIONEs { get; set; } = new List<MM_TRABAJADOR_VACACIONE>();
}
