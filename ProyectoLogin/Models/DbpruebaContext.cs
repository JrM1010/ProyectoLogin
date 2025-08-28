using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ProyectoLogin.Models;

public partial class DbpruebaContext : DbContext
{
    // Constructor que recibe las opciones de configuración del contexto.
    // ASP.NET Core lo usa para conectar la base de datos cuando registras el servicio en Program.cs.
    public DbpruebaContext(DbContextOptions<DbpruebaContext> options)
        : base(options)
    {
    }

    // Representa la tabla "Usuario" en la base de datos.
    public virtual DbSet<Usuario> Usuarios { get; set; }


    // Configuración de mapeo entre tu clase Usuario y la tabla "Usuario" en SQL.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            // Define la clave primaria de la tabla
            entity.HasKey(e => e.IdUsuario).HasName("PK__Usuario__5B65BF976FF740F1");

            // Define el nombre de la tabla en la BD
            entity.ToTable("Usuario");

            // Configuración de la columna "clave"
            entity.Property(e => e.Clave)
                .HasMaxLength(100)   
                .IsUnicode(false)    
                .HasColumnName("clave"); 

            // Configuración de la columna "Correo"
            entity.Property(e => e.Correo)
                .HasMaxLength(50)
                .IsUnicode(false);

            // Configuración de la columna "NombreUsuario"
            entity.Property(e => e.NombreUsuario)
                .HasMaxLength(50)
                .IsUnicode(false);

            // Campos para recuperación de contraseña
            entity.Property(e => e.Token_Recovery) // token de seguridad único para resetear clave
                .HasMaxLength(200)
                .IsUnicode(false);

            entity.Property(e => e.Date_Created); // fecha de creación del token
        });

        // Permite extender la configuración en otro archivo parcial si lo necesitas.
        OnModelCreatingPartial(modelBuilder);
    }

    // Método parcial para agregar configuración adicional.
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
