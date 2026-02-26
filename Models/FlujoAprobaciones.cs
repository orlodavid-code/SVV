using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class FlujoAprobaciones
{
    public int Id { get; set; }

    public int SolicitudViajeId { get; set; }

    public string Etapa { get; set; } = null!;

    public int EmpleadoAprobadorId { get; set; }

    public string EstadoAprobacion { get; set; } = null!;

    public string? Comentarios { get; set; }

    public DateTime? FechaAprobacion { get; set; }

    public string? FirmaElectronicaUrl { get; set; }

    public int OrdenEtapa { get; set; }

    public bool? Notificado { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Empleados EmpleadoAprobador { get; set; } = null!;

    public virtual SolicitudesViaje SolicitudViaje { get; set; } = null!;
}
