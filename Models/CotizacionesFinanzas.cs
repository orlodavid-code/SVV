using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class CotizacionesFinanzas
{
    public int Id { get; set; }

    public int SolicitudViajeId { get; set; }

    public string CodigoCotizacion { get; set; } = null!;

    public decimal TotalAutorizado { get; set; }

    public string Estado { get; set; } = null!;

    public int CreadoPorId { get; set; }

    public int? RevisadoPorId { get; set; }

    public DateTime? FechaCotizacion { get; set; }

    public DateTime? FechaAprobacion { get; set; }

    public string? Observaciones { get; set; }

    public DateTime? CreatedAt { get; set; }

    public decimal? TransporteCantidad { get; set; }

    public decimal? GasolinaCantidad { get; set; }

    public decimal? UberTaxiCantidad { get; set; }

    public decimal? CasetasCantidad { get; set; }

    public decimal? HospedajeCantidad { get; set; }

    public decimal? AlimentosCantidad { get; set; }

    public string? TransportePreciosJson { get; set; }

    public string? GasolinaPreciosJson { get; set; }

    public string? UberTaxiPreciosJson { get; set; }

    public string? CasetasPreciosJson { get; set; }

    public string? HospedajePreciosJson { get; set; }

    public string? AlimentosPreciosJson { get; set; }

    public decimal TransporteTotal { get; set; }

    public decimal GasolinaTotal { get; set; }

    public decimal UberTaxiTotal { get; set; }

    public decimal CasetasTotal { get; set; }

    public decimal HospedajeTotal { get; set; }

    public decimal AlimentosTotal { get; set; }

    public virtual Empleados CreadoPor { get; set; } = null!;

    public virtual Empleados? RevisadoPor { get; set; }

    public virtual SolicitudesViaje SolicitudViaje { get; set; } = null!;
}
