using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;


namespace ProyectoLogin.Models.ViewModel
{
    public class RecoveryViewModel
    {
        [EmailAddress]
        [Required]
        public string Email { get; set; }
        
    }
}
