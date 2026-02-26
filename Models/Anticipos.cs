using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class Anticipos
{
    public int Id { get; set; }

    public int SolicitudViajeId { get; set; }

    public string CodigoAnticipo { get; set; } = null!;

    public decimal MontoSolicitado { get; set; }

    public decimal? MontoAutorizado { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime? FechaSolicitud { get; set; }

    public DateTime? FechaAutorizacion { get; set; }

    public int? AutorizadoPorId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Empleados? AutorizadoPor { get; set; }

    public virtual SolicitudesViaje SolicitudViaje { get; set; } = null!;
}
