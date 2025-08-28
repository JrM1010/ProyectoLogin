using System.Security.Cryptography;
using System.Text;

namespace ProyectoLogin.Recursos
{
    public class Utilidades
    {
        public static string EncriptarClave(string clave)
        {            
            StringBuilder sb = new StringBuilder(); // StringBuilder se utiliza para ir construyendo la cadena final con los valores encriptados.
   
            using (SHA256 hash = SHA256Managed.Create()) // Se crea una instancia del algoritmo SHA256 mediante SHA256Managed. El "using" asegura que el recurso se libere correctamente.
            {
                
                Encoding enc = Encoding.UTF8; //se convierte la clave en bytes usando codificacion UTF8
                
                byte[] result = hash.ComputeHash(enc.GetBytes(clave)); // Se genera el hash a partir de la clave proporcionada.

                foreach (byte b in result) // se itera sobre cada byte del hash resultante.
                    sb.Append(b.ToString("x2")); // "x2" indica que el valor se mostrará en formato hexadecimal de 2 dígitos.

            }
            return sb.ToString();
        }
    }
}

