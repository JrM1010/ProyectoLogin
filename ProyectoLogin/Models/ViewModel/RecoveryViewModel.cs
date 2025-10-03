using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;


namespace ProyectoLogin.Models.ViewModel
{
    public class RecoveryViewModel
    {
        [EmailAddress(ErrorMessage = "Por favor, ingresa una dirección de correo electrónico válida.")]
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        public string? Email { get; set; }
        
    }
}
