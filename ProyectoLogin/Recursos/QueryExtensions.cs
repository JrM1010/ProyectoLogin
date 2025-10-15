using Microsoft.EntityFrameworkCore;

namespace ProyectoLogin.Recursos
{
    public static class QueryExtensions
    {

        /// Filtra solo los registros cuyo campo Activo == true (si el modelo tiene esa propiedad)
        public static IQueryable<T> WhereActivo<T>(this IQueryable<T> query) where T : class
        {
            var property = typeof(T).GetProperty("Activo");
            if (property == null)
                return query; // si el modelo no tiene "Activo", retorna igual

            // EF.Property permite acceder dinámicamente a la propiedad "Activo"
            return query.Where(e => EF.Property<bool>(e, "Activo"));
        }

    }
}
