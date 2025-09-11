using Microsoft.EntityFrameworkCore; // Para trabajar con Entity Framework Core y bases de datos
using ProyectoLogin.Models;           // Para acceder a los modelos de la aplicación

using ProyectoLogin.Servicios.Contrato;      // Para usar las interfaces de servicios
using ProyectoLogin.Servicios.Implementacion; // Para usar las implementaciones de los servicios

using Microsoft.AspNetCore.Authentication.Cookies; // Para autenticación basada en cookies
using Microsoft.AspNetCore.Mvc;
using ProyectoLogin.Servicios;                    // Para MVC y filtros



// Crear el builder de la aplicación web
var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();

// Agregar logging detallado
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Resto de la configuración...
builder.Services.AddDbContext<DbpruebaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<EmailService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Inicio/IniciarSesion";
        options.AccessDeniedPath = "/Home/Error";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();