using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ViewModel
{
    public class ResetPasswordViewModel
    {

        [Required]
        public string? Email { get; set; }

        [Required]
        public string? resetToken { get; set; }

        [Required(ErrorMessage = "Este campo debe ser llenado.")]
        [DataType(DataType.Password)]
        [MinLength(7, ErrorMessage = "La contraseña debe tener al menos 7 caracteres.")]
        [RegularExpression(
            pattern: @"^(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{7,}$",
            ErrorMessage = "La contraseña debe tener al menos 7 caracteres, incluir 1 mayúscula, 1 número y 1 caracter especial."
        )]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "Este campo debe ser llenado.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Las contraseñas no coinciden.")]
        public string? ConfirmPassword { get; set; }

    }
}
