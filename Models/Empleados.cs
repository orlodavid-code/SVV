using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class Empleados
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string Apellidos { get; set; } = null!;

    public int RolId { get; set; }

    public int? JefeDirectoId { get; set; }

    public string AreaAdscripcion { get; set; } = null!;

    public string? NivelPuesto { get; set; }

    public DateOnly? FechaIngreso { get; set; }

    public string? Departamento { get; set; }

    public string? Puesto { get; set; }

    public string? Telefono { get; set; }

    public bool? ColaboradorRemoto { get; set; }

    public string? UbicacionBase { get; set; }

    public bool? Activo { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Anticipos> Anticipos { get; set; } = new List<Anticipos>();

    public virtual ICollection<AuditoriaSistema> AuditoriaSistemas { get; set; } = new List<AuditoriaSistema>();

    public virtual ICollection<ComprobacionesViaje> ComprobacionesViajeAprobacionJeves { get; set; } = new List<ComprobacionesViaje>();

    public virtual ICollection<ComprobacionesViaje> ComprobacionesViajeReabiertoPors { get; set; } = new List<ComprobacionesViaje>();

    public virtual ICollection<CotizacionesFinanzas> CotizacionesFinanzaCreadoPors { get; set; } = new List<CotizacionesFinanzas>();

    public virtual ICollection<CotizacionesFinanzas> CotizacionesFinanzaRevisadoPors { get; set; } = new List<CotizacionesFinanzas>();

    public virtual ICollection<FlujoAprobaciones> FlujoAprobaciones { get; set; } = new List<FlujoAprobaciones>();

    public virtual ICollection<Empleados> InverseJefeDirecto { get; set; } = new List<Empleados>();

    public virtual Empleados? JefeDirecto { get; set; }

    public virtual ICollection<Notificaciones> Notificaciones { get; set; } = new List<Notificaciones>();

    public virtual RolesSistema Rol { get; set; } = null!;

    public virtual ICollection<SolicitudesViaje> SolicitudesViajes { get; set; } = new List<SolicitudesViaje>();
}
