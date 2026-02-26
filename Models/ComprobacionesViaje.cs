using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class ComprobacionesViaje
{
    public int Id { get; set; }

    public int SolicitudViajeId { get; set; }

    public string CodigoComprobacion { get; set; } = null!;

    public string DescripcionActividades { get; set; } = null!;

    public string ResultadosViaje { get; set; } = null!;

    public decimal? TotalGastosComprobados { get; set; }

    public decimal? TotalAnticipo { get; set; }

    public decimal? Diferencia { get; set; }

    public int EstadoComprobacionId { get; set; }

    public string EscenarioLiquidacion { get; set; } = null!;

    public bool? RequiereAprobacionJefe { get; set; }

    public int? AprobacionJefeId { get; set; }

    public string? ComentariosFinanzas { get; set; }

    public DateTime? FechaComprobacion { get; set; }

    public DateTime? FechaCierre { get; set; }

    public int? ReabiertoPorId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Empleados? AprobacionJefe { get; set; }

    public virtual EstadosComprobacion EstadoComprobacion { get; set; } = null!;

    public virtual Empleados? ReabiertoPor { get; set; }

    public virtual SolicitudesViaje SolicitudViaje { get; set; } = null!;
}
