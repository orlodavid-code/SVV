using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class SolicitudesViaje
{
    public int Id { get; set; }

    public string CodigoSolicitud { get; set; } = null!;

    public int EmpleadoId { get; set; }

    public int TipoViaticoId { get; set; }

    public int EstadoId { get; set; }

    public string Destino { get; set; } = null!;

    public string? DireccionEmpresa { get; set; }

    public string Motivo { get; set; } = null!;

    public DateOnly FechaSalida { get; set; }

    public DateOnly FechaRegreso { get; set; }

    public string? MedioTrasladoPrincipal { get; set; }

    public bool? RequiereTaxiDomicilio { get; set; }

    public string? DireccionTaxiOrigen { get; set; }

    public bool? RequiereHospedaje { get; set; }

    public int? NochesHospedaje { get; set; }

    public string EmpresaVisitada { get; set; } = null!;

    public string LugarComisionDetallado { get; set; } = null!;

    public TimeOnly? HoraSalida { get; set; }

    public TimeOnly? HoraRegreso { get; set; }

    public int? NumeroPersonas { get; set; }

    public string? ClasificacionDistancia { get; set; }

    public bool? ValidacionPlazos { get; set; }

    public bool? CumplePlazoMinimo { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Colaboradores { get; set; }

    public bool RequiereAnticipo { get; set; }

    public decimal? MontoAnticipo { get; set; }

    public string NombreProyecto { get; set; } = null!;

    public string? DireccionTaxiDestino { get; set; }

    public virtual ICollection<Anticipos> Anticipos { get; set; } = new List<Anticipos>();

    public virtual ICollection<ComprobacionesViaje> ComprobacionesViajes { get; set; } = new List<ComprobacionesViaje>();

    public virtual ICollection<CotizacionesFinanzas> CotizacionesFinanzas { get; set; } = new List<CotizacionesFinanzas>();

    public virtual Empleados Empleado { get; set; } = null!;

    public virtual EstadosSolicitud Estado { get; set; } = null!;

    public virtual ICollection<FlujoAprobaciones> FlujoAprobaciones { get; set; } = new List<FlujoAprobaciones>();

    public virtual ICollection<GastosReales> GastosReales { get; set; } = new List<GastosReales>();

    public virtual TiposViatico TipoViatico { get; set; } = null!;
}
