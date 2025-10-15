using System;

namespace ProyectoLogin.Recursos
{
    public static class FechaLocal
    {
        private static readonly TimeZoneInfo ZonaGuatemala =
            TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); 

        public static DateTime Ahora()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaGuatemala);
        }

        public static DateTime ConvertirDeUtc(DateTime fechaUtc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, ZonaGuatemala);
        }

        public static DateTime ConvertirAUtc(DateTime fechaLocal)
        {
            return TimeZoneInfo.ConvertTimeToUtc(fechaLocal, ZonaGuatemala);
        }
    }
}
