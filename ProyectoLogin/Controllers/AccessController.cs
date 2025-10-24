using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using System;

namespace ProyectoLogin.Controllers
{
    public class AccessController : Controller
    {
        private readonly DbPruebaContext _context;

        public AccessController(DbPruebaContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult StartRecovery()
        {
            TempData.Remove("SuccessMessage");
            TempData.Remove("ErrorMessage");
            return View(new Models.ViewModel.RecoveryViewModel());
        }

        [HttpPost]
        public ActionResult StartRecovery(Models.ViewModel.RecoveryViewModel model, [FromServices] EmailService emailService)
        {
            if (!ModelState.IsValid)
                return View(model);

            var usuario = _context.Usuarios.FirstOrDefault(u => u.Correo == model.Email);

            if (usuario != null)
            {
                var resetToken = Utilidades.EncriptarClave(Guid.NewGuid().ToString());

                var recuperacion = new RecuperacionPassword
                {
                    IdUsuario = usuario.IdUsuario,
                    Token = resetToken,
                    FechaCreacion = FechaLocal.Ahora(),
                    FechaExpiracion = FechaLocal.Ahora().AddHours(1),
                    Usado = false
                };

                _context.Recuperaciones.Add(recuperacion);
                _context.SaveChanges();

                string link = Url.Action("Recovery", "Access",
                            new { resetToken = resetToken, email = usuario.Correo }, Request.Scheme);

                string subject = "Recuperación de Contraseña";
                string body = $@"
            <h3>Hola {usuario.NombreUsuario},</h3>
            <p>Haz clic en el siguiente enlace para restablecer tu contraseña:</p>
            <p><a href='{link}'>Restablecer Contraseña</a></p>
            <p>Este enlace es válido por 1 hora.</p>";

                try
                {
                    emailService.SendEmail(usuario.Correo, subject, body);
                    TempData["SuccessMessage"] = "Se ha enviado un enlace de recuperación a tu correo electrónico.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error al enviar el correo. Por favor, intenta nuevamente.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "No se encontró un usuario con ese correo electrónico.";
            }

            return View(model);
        }

        [HttpGet]
        public ActionResult Recovery(string resetToken, string email)
        {
            var recuperacion = _context.Recuperaciones
                .Include(r => r.Usuario)
                .FirstOrDefault(r => r.Token == resetToken && r.Usuario.Correo == email);

            if (recuperacion == null || recuperacion.FechaExpiracion < FechaLocal.Ahora() || recuperacion.Usado)
            {
                return BadRequest("El token es inválido o ha expirado.");
            }

            return View(new Models.ViewModel.ResetPasswordViewModel
            {
                Email = email,
                resetToken = resetToken
            });
        }

        [HttpPost]
        public IActionResult Recovery(Models.ViewModel.ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var recuperacion = _context.Recuperaciones
                    .Include(r => r.Usuario)
                    .FirstOrDefault(r => r.Token == model.resetToken && r.Usuario.Correo == model.Email);

            if (recuperacion == null || recuperacion.FechaExpiracion < FechaLocal.Ahora() || recuperacion.Usado)
            {
                TempData["ErrorMessage"] = "El token es inválido o ha expirado.";
                return View(model);
            }

            try
            {
                recuperacion.Usuario.Clave = Utilidades.EncriptarClave(model.NewPassword);
                recuperacion.Usado = true;
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Contraseña restablecida correctamente. Ya puedes iniciar sesión.";
                return RedirectToAction("IniciarSesion", "Inicio");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al restablecer la contraseña. Por favor, intenta nuevamente.";
                return View(model);
            }
        }
    }
}