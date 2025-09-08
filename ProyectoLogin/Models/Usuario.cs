using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }
    public string? NombreUsuario { get; set; }
    public string? Correo { get; set; }

    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
    public string? Clave { get; set; }


    // Relaciones
    public int IdRol { get; set; }
    public virtual Rol Rol { get; set; }

}
