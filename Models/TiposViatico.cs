using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class TiposViatico
{
    public int Id { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool? RequiereAprobacionDireccion { get; set; }

    public bool? AplicaLimitesLegales { get; set; }

    public virtual ICollection<SolicitudesViaje> SolicitudesViajes { get; set; } = new List<SolicitudesViaje>();
}
