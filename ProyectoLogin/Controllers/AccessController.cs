using Microsoft.AspNetCore.Mvc;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using System;

namespace ProyectoLogin.Controllers
{
    public class AccessController : Controller
    {
        // Contexto de base de datos para acceder a la tabla Usuarios
        private readonly DbpruebaContext _context;

        // Constructor: recibe el contexto por inyección de dependencias
        public AccessController(DbpruebaContext context)
        {
            _context = context;
        }

        // Vista principal (Index) del controlador
        public IActionResult Index()
        {
            return View();
        }

        // GET: muestra el formulario para iniciar la recuperación de contraseña
        [HttpGet]
        public ActionResult StartRecovery()
        {
            return View(new Models.ViewModel.RecoveryViewModel());
        }

        // POST: procesa el formulario de recuperación de contraseña
        [HttpPost]
        public ActionResult StartRecovery(Models.ViewModel.RecoveryViewModel model, [FromServices] EmailService emailService)
        {
            // Si el modelo no es válido, se devuelve la vista con los errores
            if (!ModelState.IsValid)
                return View(model);

            // Busca al usuario por su correo
            var usuario = _context.Usuarios.FirstOrDefault(u => u.Correo == model.Email);

            if (usuario != null)
            {
                
                string resetToken = Utilidades.EncriptarClave(Guid.NewGuid().ToString()); // Generación de token único de recuperación

                usuario.Token_Recovery = resetToken;
                usuario.Date_Created = DateTime.Now.AddHours(2); //le da tiempo al token de dos horas 

                // Actualiza el usuario en la BD
                _context.Update(usuario);
                _context.SaveChanges();

                // Genera el enlace para restablecer contraseña
                string link = Url.Action("Recovery", "Access",
                            new { resetToken = resetToken, email = usuario.Correo }, Request.Scheme);

                // Asunto y cuerpo del correo con el enlace
                string subject = "Recuperación de Contraseña";
                string body = $@"
                    <h3>Hola {usuario.NombreUsuario},</h3>
                    <p>Haz clic en el siguiente enlace para restablecer tu contraseña:</p>
                    <p><a href='{link}'>Restablecer Contraseña</a></p>
                    <p>Este enlace es válido por 2 horas.</p>";

                // Envía el correo al usuario
                emailService.SendEmail(usuario.Correo, subject, body);

                // Mensaje de confirmación
                ViewBag.Message = "Se ha enviado un enlace de recuperación a tu correo electrónico.";
            }
            else
            {
                // Si no existe el correo, muestra error
                ModelState.AddModelError("", "No se encontró un usuario con ese correo.");
            }

            return View(model);
        }

        // GET: muestra la vista para ingresar nueva contraseña
        [HttpGet]
        public ActionResult Recovery(string resetToken, string email)
        {
            // Busca al usuario con correo y token válidos
            var usuario = _context.Usuarios
                                 .FirstOrDefault(u => u.Correo == email && u.Token_Recovery == resetToken);

            // Valida que el token no haya expirado
            if (usuario == null || usuario.Date_Created < DateTime.Now)
            {
                return BadRequest("El token es inválido o ha expirado.");
            }

            // Devuelve el modelo a la vista con email y token
            return View(new Models.ViewModel.ResetPasswordViewModel
            {
                Email = email,
                resetToken = resetToken
            });
        }

        // POST: procesa el formulario para cambiar la contraseña
        [HttpPost]
        public IActionResult Recovery(Models.ViewModel.ResetPasswordViewModel model)
        {
            // Valida el modelo
            if (!ModelState.IsValid)
                return View(model);

            // Busca al usuario con correo y token
            var usuario = _context.Usuarios
                .FirstOrDefault(u => u.Correo == model.Email && u.Token_Recovery == model.resetToken);

            // Si no existe o expiró, error
            if (usuario == null || usuario.Date_Created < DateTime.Now)
            {
                ModelState.AddModelError("", "El token es inválido o ha expirado.");
                return View(model);
            }

            // Encripta y guarda la nueva contraseña
            usuario.Clave = Utilidades.EncriptarClave(model.NewPassword);

            // Limpia el token para que no pueda usarse otra vez
            usuario.Token_Recovery = null;

            // Actualiza cambios en la BD
            _context.Update(usuario);
            _context.SaveChanges();

            return RedirectToAction("Index", "Home");
        }
    }
}