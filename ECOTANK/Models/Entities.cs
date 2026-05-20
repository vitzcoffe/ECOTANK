namespace ECOTANK.Models
{
    // Modelos POCO que reflejan las tablas de database/schema.sql.
    // Nombres en inglés (código) mapeando columnas en español (BD).

    public class Rol
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
    }

    public class Usuario
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Telefono { get; set; }
        public string PasswordHash { get; set; } = "";
        public int RolId { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime FechaRegistro { get; set; }
    }

    public class Ubicacion
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public string? Ciudad { get; set; }
        public string? Pais { get; set; }
    }

    public class Alojamiento
    {
        public int Id { get; set; }
        public int AnfitrionId { get; set; }
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public string? Tipo { get; set; }
        public decimal PrecioNoche { get; set; }
        public int Capacidad { get; set; }
        public int? UbicacionId { get; set; }
        public string? ImagenUrl { get; set; }
        public bool Disponible { get; set; } = true;
        public DateTime FechaCreacion { get; set; }
    }

    public class Actividad
    {
        public int Id { get; set; }
        public int OrganizadorId { get; set; }
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public decimal? DuracionHoras { get; set; }
        public int CupoMaximo { get; set; }
        public int? UbicacionId { get; set; }
        public string? ImagenUrl { get; set; }
        public bool Disponible { get; set; } = true;
        public DateTime FechaCreacion { get; set; }
    }

    public class NegocioLocal
    {
        public int Id { get; set; }
        public int PropietarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public string? Categoria { get; set; }
        public string? Telefono { get; set; }
        public int? UbicacionId { get; set; }
        public string? ImagenUrl { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class Reserva
    {
        public int Id { get; set; }
        public int TuristaId { get; set; }
        public int? AlojamientoId { get; set; }
        public int? ActividadId { get; set; }
        public DateTime FechaReserva { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int NumPersonas { get; set; } = 1;
        public string Estado { get; set; } = "pendiente";
        public decimal Total { get; set; }
    }

    public class Pago
    {
        public int Id { get; set; }
        public int ReservaId { get; set; }
        public decimal Monto { get; set; }
        public string MetodoPago { get; set; } = "";
        public string Estado { get; set; } = "pendiente";
        public string? Referencia { get; set; }
        public DateTime FechaPago { get; set; }
    }

    public class Resena
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public int? AlojamientoId { get; set; }
        public int? ActividadId { get; set; }
        public byte Calificacion { get; set; }
        public string? Comentario { get; set; }
        public DateTime Fecha { get; set; }
    }
}
