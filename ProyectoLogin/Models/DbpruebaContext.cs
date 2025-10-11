using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models.ModelosProducts;
using System;
using System.Collections.Generic;

namespace ProyectoLogin.Models;

public partial class DbPruebaContext : DbContext
{
    // Constructor que recibe las opciones de configuración del contexto.
    public DbPruebaContext(DbContextOptions<DbPruebaContext> options) : base(options)
    {
    }

    // Representa las tablas en la base de datos.
    public virtual DbSet<Usuario> Usuarios { get; set; }
    public virtual DbSet<Rol> Roles { get; set; }
    public virtual DbSet<RecuperacionPassword> Recuperaciones { get; set; }
    public virtual DbSet<Proveedor> Proveedores { get; set; }
    public virtual DbSet<Cliente> Clientes { get; set; }


    // Parte de Productos
    public virtual DbSet<ProductoCore> Productos { get; set; }
    public virtual DbSet<Categoria> Categorias { get; set; }
    public virtual DbSet<Marca> Marcas { get; set; }
    public virtual DbSet<Inventario> Inventarios { get; set; }
    public virtual DbSet<MovInventario> MovInventarios { get; set; }



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

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.HasKey(e => e.IdProveedor);
            entity.ToTable("Proveedor");
            entity.Property(e => e.Nombre).HasMaxLength(200).IsUnicode(false).IsRequired(false);
            entity.Property(e => e.Contacto).HasMaxLength(100).IsUnicode(false).IsRequired(false);
            entity.Property(e => e.Telefono).HasMaxLength(20).IsUnicode(false).IsRequired(false);
            entity.Property(e => e.Email).HasMaxLength(100).IsUnicode(false).IsRequired(false);
        });


        modelBuilder.Entity<Cliente>()
        .HasIndex(c => c.Nit)
        .IsUnique(false); // cambia a true si quieres forzar unicidad



        modelBuilder.Entity<ProductoCore>(entity =>
        {
            entity.HasKey(e => e.IdProducto);
            entity.ToTable("ProductoCore");

            entity.Property(e => e.Nombre)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Descripcion)
                .HasMaxLength(500);

            entity.Property(e => e.CodigoBarras)
                .HasMaxLength(100);

            entity.Property(e => e.Activo)
                .HasDefaultValue(true);

            // Relación con Categoría
            entity.HasOne(p => p.Categoria)
                  .WithMany()
                  .HasForeignKey(p => p.IdCategoria)
                  .OnDelete(DeleteBehavior.Restrict);

            // Relación con Marca
            entity.HasOne(p => p.Marca)
                  .WithMany()
                  .HasForeignKey(p => p.IdMarca)
                  .OnDelete(DeleteBehavior.Restrict);
        });



        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasKey(e => e.IdCategoria);
            entity.ToTable("Categoria");

            entity.Property(e => e.Nombre)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Descripcion)
                  .HasMaxLength(255);

            entity.Property(e => e.Activo)
                  .HasDefaultValue(true);
        });



        modelBuilder.Entity<Marca>(entity =>
        {
            entity.HasKey(e => e.IdMarca);
            entity.ToTable("Marca");

            entity.Property(e => e.Nombre)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Activo)
                  .HasDefaultValue(true);
        });



        modelBuilder.Entity<Inventario>(entity =>
        {
            entity.HasKey(e => e.IdInventario);
            entity.ToTable("Inventario");

            entity.Property(e => e.StockActual)
                  .HasColumnType("int(18,2)")
                  .HasDefaultValue(0);

            entity.Property(e => e.StockMinimo)
                  .HasColumnType("int(18,2)")
                  .HasDefaultValue(0);

            entity.Property(e => e.FechaUltimaActualizacion)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("GETDATE()");

            // 🔗 Relación uno-a-uno con ProductoCore
            entity.HasOne(i => i.Producto)
                  .WithOne(p => p.Inventario)
                  .HasForeignKey<Inventario>(i => i.IdProducto)
                  .HasPrincipalKey<ProductoCore>(p => p.IdProducto)
                  .OnDelete(DeleteBehavior.Cascade);
        });



        modelBuilder.Entity<MovInventario>(entity =>
        {
            entity.HasKey(e => e.IdMovimiento);
            entity.ToTable("MovInventario");

            entity.Property(e => e.TipoMovimiento)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Cantidad)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Referencia)
                  .HasMaxLength(100);

            entity.Property(e => e.Observacion)
                  .HasMaxLength(255);

            entity.Property(e => e.Fecha)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("GETDATE()");

            // Relación con ProductoCore (muchos movimientos por producto)
            entity.HasOne(m => m.Producto)
                  .WithMany()
                  .HasForeignKey(m => m.IdProducto)
                  .HasPrincipalKey(p => p.IdProducto)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    
    

    // Método parcial para agregar configuración adicional.
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
