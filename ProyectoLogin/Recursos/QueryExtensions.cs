using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosProducts;

namespace ProyectoLogin.Recursos
{
    public static class QueryExtensions
    {
        public static IQueryable<Cliente> WhereActivo(this IQueryable<Cliente> query)
        {
            return query.Where(c => c.Activo);
        }

        public static IQueryable<Proveedor> WhereActivo(this IQueryable<Proveedor> query)
        {
            return query.Where(p => p.Activo);
        }

        public static IQueryable<ProductoCore> WhereActivo(this IQueryable<ProductoCore> query)
        {
            return query.Where(p => p.Activo);
        }

        public static IQueryable<Marca> WhereActivo(this IQueryable<Marca> query)
        {
            return query.Where(m => m.Activo);
        }
    }
}
