using ProyectoLogin.Recursos;

namespace ProyectoLogin.Models
{
    public class RecuperacionPassword
    {
        public int IdRecuperacion { get; set; }
        public int IdUsuario { get; set; }
        public string Token { get; set; } = null!;
        public DateTime FechaCreacion { get; set; } = FechaLocal.Ahora();
        public DateTime FechaExpiracion { get; set; } = FechaLocal.Ahora().AddHours(1);
        public bool Usado { get; set; }

        public virtual Usuario? Usuario { get; set; }


    }
}
