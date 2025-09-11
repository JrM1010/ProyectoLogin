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

    public virtual DbSet<Producto> Productos { get; set; }
    public virtual DbSet<Categoria> Categorias { get; set; }
    public virtual DbSet<Marca> Marcas { get; set; }
    public virtual DbSet<Proveedor> Proveedores { get; set; }
    public virtual DbSet<MovimientoInventario> MovimientosInventario { get; set; }


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

        //Aqui empieza la implementacion de Productos en el POS

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.IdProducto);
            entity.ToTable("Producto");

            entity.Property(e => e.Nombre).HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.Descripcion).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.CodigoBarras).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.PrecioCompra).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PrecioVenta).HasColumnType("decimal(18,2)");

            entity.HasOne(p => p.Categoria)
                  .WithMany(c => c.Productos)
                  .HasForeignKey(p => p.IdCategoria);

            entity.HasOne(p => p.Marca)
                  .WithMany(m => m.Productos)
                  .HasForeignKey(p => p.IdMarca);

            entity.HasOne(p => p.Proveedor)
                  .WithMany(pr => pr.Productos)
                  .HasForeignKey(p => p.IdProveedor);
        });

        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasKey(e => e.IdCategoria);
            entity.ToTable("Categoria");
            entity.Property(e => e.Nombre).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Descripcion).HasMaxLength(300).IsUnicode(false);
        });

        modelBuilder.Entity<Marca>(entity =>
        {
            entity.HasKey(e => e.IdMarca);
            entity.ToTable("Marca");
            entity.Property(e => e.Nombre).HasMaxLength(100).IsUnicode(false);
        });

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.HasKey(e => e.IdProveedor);
            entity.ToTable("Proveedor");
            entity.Property(e => e.Nombre).HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.Contacto).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Telefono).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Email).HasMaxLength(100).IsUnicode(false);
        });

        modelBuilder.Entity<MovimientoInventario>(entity =>
        {
            entity.HasKey(e => e.IdMovimiento);
            entity.ToTable("MovimientoInventario");

            entity.Property(e => e.TipoMovimiento).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Cantidad).HasColumnType("int");
            entity.Property(e => e.Motivo).HasMaxLength(300).IsUnicode(false);

            entity.HasOne(m => m.Producto)
                  .WithMany()
                  .HasForeignKey(m => m.IdProducto);

            entity.HasOne(m => m.Usuario)
                  .WithMany()
                  .HasForeignKey(m => m.IdUsuario);
        });



    }

    
    

    // Método parcial para agregar configuración adicional.
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
