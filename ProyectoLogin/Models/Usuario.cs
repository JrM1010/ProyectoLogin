using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ProyectoLogin.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
    public string? NombreUsuario { get; set; }

    [Required(ErrorMessage = "El correo es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del correo no es válido")]
    public string? Correo { get; set; }


    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>?]).+$",
    ErrorMessage = "Debe contener mayúscula, número y carácter especial")]
    public string? Clave { get; set; }



    // Relaciones
    [Required(ErrorMessage = "Debe seleccionar un rol válido")]
    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un rol válido")]
    public int IdRol { get; set; }
    public virtual Rol? Rol { get; set; }

    // 🔹 Nuevo campo para soft delete
    public bool Activo { get; set; } = true;
}