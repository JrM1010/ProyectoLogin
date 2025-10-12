using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models.ModelosCompras;
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

    // Parte de Usuarios, roles y recuperación de contraseña
    public virtual DbSet<Usuario> Usuarios { get; set; }
    public virtual DbSet<Rol> Roles { get; set; }
    public virtual DbSet<RecuperacionPassword> Recuperaciones { get; set; }


    // Parte de Productos
    public virtual DbSet<ProductoCore> Productos { get; set; }
    public virtual DbSet<Categoria> Categorias { get; set; }
    public virtual DbSet<Marca> Marcas { get; set; }
    public virtual DbSet<Inventario> Inventarios { get; set; }
    public virtual DbSet<MovInventario> MovInventarios { get; set; }
    public DbSet<ProductoPrecio> ProductoPrecio { get; set; }
    public virtual DbSet<Proveedor> Proveedores { get; set; }
    public virtual DbSet<Cliente> Clientes { get; set; }
    public DbSet<ProductoProveedor> ProductoProveedor { get; set; }


    //Entidades de Compras
    public virtual DbSet<Compra> Compras { get; set; }
    public virtual DbSet<DetalleCompra> DetallesCompra { get; set; }
    public virtual DbSet<UnidadMedida> Unidades { get; set; }


    // Configuración de mapeo entre tu clase Usuario y la tabla "Usuario" en SQL.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ------------------ USUARIOS ------------------
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario);
            entity.ToTable("Usuario");

            entity.Property(e => e.NombreUsuario).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Correo).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Clave).HasMaxLength(500).IsUnicode(false);

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

            entity.HasOne(r => r.Usuario)
                  .WithMany()
                  .HasForeignKey(r => r.IdUsuario);
        });

        // ------------------ PROVEEDOR ------------------
        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.HasKey(e => e.IdProveedor);
            entity.ToTable("Proveedor");

            entity.Property(e => e.Nombre).HasMaxLength(200).IsUnicode(false).IsRequired(false);
            entity.Property(e => e.Contacto).HasMaxLength(100).IsUnicode(false).IsRequired(false);
            entity.Property(e => e.Telefono).HasMaxLength(20).IsUnicode(false).IsRequired(false);
            entity.Property(e => e.Email).HasMaxLength(100).IsUnicode(false).IsRequired(false);
            entity.Property(p => p.Activo).HasDefaultValue(true);
        });

        // ------------------ CLIENTE ------------------
        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Nit)
            .IsUnique(false);

        // ------------------ PRODUCTOS ------------------
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

            entity.HasOne(p => p.Categoria)
                  .WithMany()
                  .HasForeignKey(p => p.IdCategoria)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Marca)
                  .WithMany()
                  .HasForeignKey(p => p.IdMarca)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ------------------ CATEGORIA ------------------
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

        // ------------------ MARCA ------------------
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

        // ------------------ INVENTARIO ------------------
        modelBuilder.Entity<Inventario>(entity =>
        {
            entity.HasKey(e => e.IdInventario);
            entity.ToTable("Inventario");

            entity.Property(e => e.StockActual)
                  .HasColumnType("int")
                  .HasDefaultValue(0);

            entity.Property(e => e.StockMinimo)
                  .HasColumnType("int")
                  .HasDefaultValue(0);

            entity.Property(e => e.FechaUltimaActualizacion)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("GETDATE()");

            entity.HasOne(i => i.Producto)
                  .WithOne(p => p.Inventario)
                  .HasForeignKey<Inventario>(i => i.IdProducto)
                  .HasPrincipalKey<ProductoCore>(p => p.IdProducto)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------ MOVIMIENTOS INVENTARIO ------------------
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

            entity.HasOne(m => m.Producto)
                  .WithMany()
                  .HasForeignKey(m => m.IdProducto)
                  .HasPrincipalKey(p => p.IdProducto)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------ UNIDAD DE MEDIDA ------------------
        modelBuilder.Entity<UnidadMedida>(entity =>
        {
            entity.HasKey(e => e.IdUnidad);
            entity.ToTable("UnidadMedida");

            entity.Property(e => e.Nombre)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.FactorConversion)
                  .HasColumnType("decimal(18,2)")
                  .HasDefaultValue(1m);
        });

        // ------------------ COMPRAS ------------------
        modelBuilder.Entity<Compra>(entity =>
        {
            entity.HasKey(e => e.IdCompra);
            entity.ToTable("Compras");

            entity.Property(e => e.FechaCompra)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("GETDATE()");

            entity.Property(e => e.NumeroDocumento)
                  .HasMaxLength(50)
                  .IsUnicode(false);

            entity.Property(e => e.Subtotal)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.IVA)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Total)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.MetodoPago)
                  .HasMaxLength(50)
                  .IsUnicode(false);

            entity.Property(e => e.Observaciones)
                  .HasMaxLength(255)
                  .IsUnicode(false);

            entity.Property(e => e.Estado)
                  .HasMaxLength(30)
                  .IsUnicode(false)
                  .HasDefaultValue("Completada");

            entity.HasOne(c => c.Proveedor)
                  .WithMany()
                  .HasForeignKey(c => c.IdProveedor)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ------------------ DETALLE COMPRA ------------------
        modelBuilder.Entity<DetalleCompra>(entity =>
        {
            entity.HasKey(e => e.IdDetalle);
            entity.ToTable("DetalleCompra");

            entity.Property(e => e.Cantidad)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.PrecioUnitario)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Subtotal)
                  .HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.Compra)
                  .WithMany(c => c.Detalles)
                  .HasForeignKey(d => d.IdCompra)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Producto)
                  .WithMany()
                  .HasForeignKey(d => d.IdProducto)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Unidad)
                  .WithMany()
                  .HasForeignKey(d => d.IdUnidad)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ------------------ PRODUCTO PRECIO ------------------
        modelBuilder.Entity<ProductoPrecio>(entity =>
        {
            entity.HasKey(e => e.IdPrecio);
            entity.ToTable("ProductoPrecio"); // asegurar nombre exacto de la tabla

            entity.Property(e => e.PrecioCompra)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.PrecioVenta)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.FechaInicio)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("GETDATE()");

            entity.Property(e => e.FechaFin)
                  .HasColumnType("datetime")
                  .IsRequired(false);

            entity.Property(e => e.Activo)
                  .HasDefaultValue(true);

            // Mapeo explícito de la relación con ProductoCore usando la propiedad FK IdProducto
            entity.HasOne(pp => pp.Producto)     // navegación en ProductoPrecio
                  .WithMany()                    // si ProductoCore no tiene colección, dejar sin argumento; si tiene, poner p => p.ProductoPrecios
                  .HasForeignKey(pp => pp.IdProducto)
                  .HasPrincipalKey(p => p.IdProducto)
                  .OnDelete(DeleteBehavior.Cascade);
        });

    }




    // Método parcial para agregar configuración adicional.
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
