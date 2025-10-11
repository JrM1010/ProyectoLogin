using Microsoft.EntityFrameworkCore; // Para trabajar con Entity Framework Core y bases de datos
using ProyectoLogin.Models;           // Para acceder a los modelos de la aplicaci�n

using ProyectoLogin.Servicios.Contrato;      // Para usar las interfaces de servicios
using ProyectoLogin.Servicios.Implementacion; // Para usar las implementaciones de los servicios

using Microsoft.AspNetCore.Authentication.Cookies; // Para autenticaci�n basada en cookies
using Microsoft.AspNetCore.Mvc;
using ProyectoLogin.Servicios;                    // Para MVC y filtros



// Crear el builder de la aplicaci�n web
var builder = WebApplication.CreateBuilder(args);


// Registrar un servicio de usuario usando inyecci�n de dependencias
// Scoped: se crea una instancia por cada solicitud HTTP
builder.Services.AddScoped<IUsuarioService, UsuarioService>();



// Configurar autenticaci�n basada en cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        
        options.LoginPath = "/Inicio/IniciarSesion";
        
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);// Duraci�n de la cookie (sesi�n) en minutos
    });


// Registro de envios de correos electr�nicos
builder.Services.AddSingleton<EmailService>();

// Configurar DbContext para conectarse a SQL Server usando la cadena de conexi�n definida en appsettings.json
// Configura la cadena de conexi�n desde appsettings.json
builder.Services.AddDbContext<DbPruebaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CadenaSQL")));

builder.Services.AddControllersWithViews();



// Configuraci�n adicional para MVC: agregar un filtro global para evitar cache de respuestas
builder.Services.AddControllersWithViews(options => {
    options.Filters.Add(
        new ResponseCacheAttribute
        {
            NoStore = true, // No almacenar la respuesta en cach�
            Location = ResponseCacheLocation.None // No almacenar en cliente ni en servidores intermedios
        }
    );
});

// Construir la aplicaci�n a partir de la configuraci�n anterior
var app = builder.Build();

// Configurar pipeline de middleware
if (!app.Environment.IsDevelopment())
{
    // En producci�n: usar p�gina de error personalizada
    app.UseExceptionHandler("/Home/Error");
    // HSTS: mejora la seguridad forzando HTTPS
    app.UseHsts();
}

// Redirigir autom�ticamente HTTP a HTTPS
app.UseHttpsRedirection();

// Habilitar ruteo de solicitudes HTTP
app.UseRouting();

// Habilitar autenticaci�n y autorizaci�n
app.UseAuthentication();
app.UseAuthorization();

// Mapear archivos est�ticos (CSS, JS, im�genes, etc.)
// M�todo personalizado del proyecto
app.MapStaticAssets();

// Configurar rutas MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=IniciarSesion}/{id?}") // Controlador y acci�n por defecto
    .WithStaticAssets(); // Posiblemente una extensi�n personalizada para servir assets

// Iniciar la aplicaci�n y comenzar a escuchar solicitudes
app.Run();
