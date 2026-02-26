using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class EstadosSolicitud
{
    public int Id { get; set; }

    public string Codigo { get; set; } = null!;

    public string? Descripcion { get; set; }

    public int Orden { get; set; }

    public bool? EsEstadoFinal { get; set; }

    public virtual ICollection<SolicitudesViaje> SolicitudesViajes { get; set; } = new List<SolicitudesViaje>();
}
