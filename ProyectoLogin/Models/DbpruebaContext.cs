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

    // Representa las tablas en la base de datos.
    public virtual DbSet<Usuario> Usuarios { get; set; }
    public virtual DbSet<Rol> Roles { get; set; }
    public virtual DbSet<RecuperacionPassword> Recuperaciones { get; set; }


    // Configuración de mapeo entre tu clase Usuario y la tabla "Usuario" en SQL.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario);
            entity.ToTable("Usuario");

            entity.Property(e => e.NombreUsuario).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Correo).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Clave).HasMaxLength(500).IsUnicode(false);

            // Relación Usuario → Rol
            entity.HasOne(u => u.Rol)
                  .WithMany(r => r.Usuarios)
                  .HasForeignKey(u => u.IdRol);
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.HasKey(e => e.IdRol);
            entity.ToTable("Rol");
            entity.Property(e => e.NombreRol).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<RecuperacionPassword>(entity =>
        {
            entity.HasKey(e => e.IdRecuperacion);
            entity.ToTable("RecuperacionPassword");

            entity.Property(e => e.Token).HasMaxLength(200).IsUnicode(false);

            // Relación con Usuario
            entity.HasOne(r => r.Usuario)
                  .WithMany()
                  .HasForeignKey(r => r.IdUsuario);
        });
    }

    
    

    // Método parcial para agregar configuración adicional.
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
