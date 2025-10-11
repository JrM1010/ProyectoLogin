using Microsoft.EntityFrameworkCore; // Para trabajar con Entity Framework Core y bases de datos
using ProyectoLogin.Models;           // Para acceder a los modelos de la aplicación

using ProyectoLogin.Servicios.Contrato;      // Para usar las interfaces de servicios
using ProyectoLogin.Servicios.Implementacion; // Para usar las implementaciones de los servicios

using Microsoft.AspNetCore.Authentication.Cookies; // Para autenticación basada en cookies
using Microsoft.AspNetCore.Mvc;
using ProyectoLogin.Servicios;                    // Para MVC y filtros



// Crear el builder de la aplicación web
var builder = WebApplication.CreateBuilder(args);


// Registrar un servicio de usuario usando inyección de dependencias
// Scoped: se crea una instancia por cada solicitud HTTP
builder.Services.AddScoped<IUsuarioService, UsuarioService>();



// Configurar autenticación basada en cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        
        options.LoginPath = "/Inicio/IniciarSesion";
        
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);// Duración de la cookie (sesión) en minutos
    });


// Registro de envios de correos electrónicos
builder.Services.AddSingleton<EmailService>();

// Configurar DbContext para conectarse a SQL Server usando la cadena de conexión definida en appsettings.json
// Configura la cadena de conexión desde appsettings.json
builder.Services.AddDbContext<DbPruebaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CadenaSQL")));

builder.Services.AddControllersWithViews();



// Configuración adicional para MVC: agregar un filtro global para evitar cache de respuestas
builder.Services.AddControllersWithViews(options => {
    options.Filters.Add(
        new ResponseCacheAttribute
        {
            NoStore = true, // No almacenar la respuesta en caché
            Location = ResponseCacheLocation.None // No almacenar en cliente ni en servidores intermedios
        }
    );
});

// Construir la aplicación a partir de la configuración anterior
var app = builder.Build();

// Configurar pipeline de middleware
if (!app.Environment.IsDevelopment())
{
    // En producción: usar página de error personalizada
    app.UseExceptionHandler("/Home/Error");
    // HSTS: mejora la seguridad forzando HTTPS
    app.UseHsts();
}

// Redirigir automáticamente HTTP a HTTPS
app.UseHttpsRedirection();

// Habilitar ruteo de solicitudes HTTP
app.UseRouting();

// Habilitar autenticación y autorización
app.UseAuthentication();
app.UseAuthorization();

// Mapear archivos estáticos (CSS, JS, imágenes, etc.)
// Método personalizado del proyecto
app.MapStaticAssets();

// Configurar rutas MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=IniciarSesion}/{id?}") // Controlador y acción por defecto
    .WithStaticAssets(); // Posiblemente una extensión personalizada para servir assets

// Iniciar la aplicación y comenzar a escuchar solicitudes
app.Run();
