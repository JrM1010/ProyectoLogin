using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using System;
using System.Text.RegularExpressions;


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
            // Normaliza entradas mínimamente
            model.Email = model.Email?.Trim();
            model.resetToken = model.resetToken?.Trim();

            // Valida DataAnnotations primero (Required, Compare, etc.)
            if (!ModelState.IsValid)
                return View(model);

            // Política de contraseña: 7+ chars, 1 mayúscula, 1 número, 1 carácter especial
            const string PasswordPolicy = @"^(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{7,}$";
            if (string.IsNullOrWhiteSpace(model.NewPassword) || !Regex.IsMatch(model.NewPassword, PasswordPolicy))
            {
                ModelState.AddModelError(nameof(model.NewPassword),
                    "La contraseña debe tener al menos 7 caracteres, incluir 1 mayúscula, 1 número y 1 carácter especial.");
                return View(model);
            }

            // Reconfirma coincidencia por seguridad (además del [Compare] del modelo)
            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Las contraseñas no coinciden.");
                return View(model);
            }

            var ahora = FechaLocal.Ahora();

            var recuperacion = _context.Recuperaciones
                .Include(r => r.Usuario)
                .FirstOrDefault(r =>
                    r.Token == model.resetToken &&
                    r.Usuario != null &&
                    r.Usuario.Correo == model.Email);

            if (recuperacion == null)
            {
                TempData["ErrorMessage"] = "El token es inválido o no corresponde al usuario.";
                return View(model);
            }

            if (recuperacion.FechaExpiracion < ahora)
            {
                TempData["ErrorMessage"] = "El token ha expirado.";
                return View(model);
            }

            if (recuperacion.Usado)
            {
                TempData["ErrorMessage"] = "Este enlace ya fue utilizado.";
                return View(model);
            }

            try
            {
                recuperacion.Usuario.Clave = Utilidades.EncriptarClave(model.NewPassword);
                recuperacion.Usado = true;

                // Opcional: marca explícitamente como modificado (según tu configuración de tracking)
                _context.Update(recuperacion);

                _context.SaveChanges();

                TempData["SuccessMessage"] = "Contraseña restablecida correctamente. Ya puedes iniciar sesión.";
                return RedirectToAction("IniciarSesion", "Inicio");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Error al restablecer la contraseña. Por favor, intenta nuevamente.";
                return View(model);
            }
        }

    }
}