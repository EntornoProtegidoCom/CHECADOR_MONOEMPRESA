using System;
using System.Collections.Generic;
using ChecadorWeb.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChecadorWeb.Data;

public partial class ChecadorDbContext : DbContext
{
    public ChecadorDbContext(DbContextOptions<ChecadorDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DD_EQUIPO_HEARTBEAT> DD_EQUIPO_HEARTBEATs { get; set; }

    public virtual DbSet<DD_EQUIPO_VINCULO> DD_EQUIPO_VINCULOs { get; set; }

    public virtual DbSet<DiaFestivo> DiaFestivos { get; set; }

    public virtual DbSet<MM_HORARIO> MM_HORARIOs { get; set; }

    public virtual DbSet<MM_TRABAJADOR_VACACIONE> MM_TRABAJADOR_VACACIONEs { get; set; }

    public virtual DbSet<MM_TRABAJADORE> MM_TRABAJADOREs { get; set; }

    public virtual DbSet<SS_USUARIO> SS_USUARIOs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DD_EQUIPO_HEARTBEAT>(entity =>
        {
            entity.HasKey(e => e.NBEquipo).HasName("PK__DD_EQUIP__5BE86C6DE8DDA81A");

            entity.ToTable("DD_EQUIPO_HEARTBEAT");

            entity.Property(e => e.NBEquipo)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EstadoEstacion)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVO");
            entity.Property(e => e.IPRegistro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UltimoHeartbeat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.VersionApp)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<DD_EQUIPO_VINCULO>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PK__DD_EQUIP__3214EC274DCCA549");

            entity.ToTable("DD_EQUIPO_VINCULO");

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.Create_ID_USUARIO)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.NBEquipo)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Update_ID_USUARIO)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.create_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.update_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IDTrabajadorNavigation).WithMany(p => p.DD_EQUIPO_VINCULOs)
                .HasForeignKey(d => d.IDTrabajador)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DD_EQUIPO_VINCULO_MM_TRABAJADORES");
        });

        modelBuilder.Entity<MM_HORARIO>(entity =>
        {
            entity.ToTable("MM_HORARIO");

            entity.Property(e => e.Foto).HasColumnType("image");
            entity.Property(e => e.HashRegistro).HasMaxLength(32);
            entity.Property(e => e.Horaregistro).HasColumnType("datetime");
            entity.Property(e => e.IPRegistro)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.NBEquipo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NuIntentos).HasDefaultValue((short)0);
            entity.Property(e => e.stEnviado).HasDefaultValue(false);

            entity.HasOne(d => d.IDTrabajadorNavigation).WithMany(p => p.MM_HORARIOs)
                .HasForeignKey(d => d.IDTrabajador)
                .HasConstraintName("FK_MM_HORAR_REFERENCE_MM_TRABAJA");
        });

        modelBuilder.Entity<MM_TRABAJADORE>(entity =>
        {
            entity.HasKey(e => e.IDTrabajador).HasName("PK_MM_EMPLEADOS");

            entity.ToTable("MM_TRABAJADORES");

            entity.Property(e => e.IDTrabajador).ValueGeneratedNever();
            entity.Property(e => e.ApellidoM)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ApellidoP)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Contrasena)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Correo).HasMaxLength(100);
            entity.Property(e => e.Create_ID_USUARIO)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.Foto2).HasColumnType("image");
            entity.Property(e => e.HoraEntrada).HasPrecision(0);
            entity.Property(e => e.HoraEntradaComida).HasPrecision(0);
            entity.Property(e => e.HoraSalida).HasPrecision(0);
            entity.Property(e => e.HoraSalidaComida).HasPrecision(0);
            entity.Property(e => e.IDCARD)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.Nombre)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Update_ID_USUARIO)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.create_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.status).HasDefaultValue(false);
            entity.Property(e => e.update_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<DiaFestivo>(entity =>
        {
            entity.ToTable("MM_DIAS_FESTIVOS");

            entity.HasKey(e => e.ID);

            entity.HasIndex(e => e.Fecha)
                .IsUnique()
                .HasDatabaseName("UQ_MM_DIAS_FESTIVOS_Fecha");

            entity.Property(e => e.Descripcion)
                .HasMaxLength(150)
                .IsUnicode(false);

            entity.Property(e => e.EsOficial)
                .HasDefaultValue(true);

            entity.Property(e => e.Estatus)
                .HasDefaultValue(true);

            entity.Property(e => e.Create_ID_USUARIO)
                .HasMaxLength(25)
                .IsUnicode(false);

            entity.Property(e => e.Update_ID_USUARIO)
                .HasMaxLength(25)
                .IsUnicode(false);

            entity.Property(e => e.create_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.Property(e => e.update_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.Property(e => e.Fecha)
                .HasColumnType("date");
        });

        modelBuilder.Entity<MM_TRABAJADOR_VACACIONE>(entity =>
        {
            entity.HasKey(e => e.ID).HasName("PK_MM_TRABAJADOR_VACACIONES");

            entity.ToTable("MM_TRABAJADOR_VACACIONES");

            entity.HasIndex(e => new { e.IDTrabajador, e.FechaInicio, e.FechaFin, e.Estatus }, "IX_MM_TRABAJADOR_VACACIONES_Consulta");

            entity.Property(e => e.FechaInicio).HasColumnType("date");
            entity.Property(e => e.FechaFin).HasColumnType("date");
            entity.Property(e => e.Estatus).HasDefaultValue(true);
            entity.Property(e => e.Observaciones)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.Create_ID_USUARIO)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.Update_ID_USUARIO)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.create_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.update_time)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IDTrabajadorNavigation).WithMany(p => p.MM_TRABAJADOR_VACACIONEs)
                .HasForeignKey(d => d.IDTrabajador)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MM_VACACIONES_TRABAJADOR");
        });

        modelBuilder.Entity<SS_USUARIO>(entity =>
        {
            entity.HasKey(e => e.ID_USUARIO).IsClustered(false);

            entity.ToTable("SS_USUARIOS");

            entity.Property(e => e.ID_USUARIO)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.NB_EMAIL)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.NB_MATERNO)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.NB_NOMBRE)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.NB_PATERNO)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ST_VIGENTE)
                .HasMaxLength(1)
                .IsUnicode(false);
            entity.Property(e => e.TX_PASSWORD)
                .HasMaxLength(64)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
