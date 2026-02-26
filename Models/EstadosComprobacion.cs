using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class EstadosComprobacion
{
    public int Id { get; set; }

    public string Codigo { get; set; } = null!;

    public string? Descripcion { get; set; }

    public virtual ICollection<ComprobacionesViaje> ComprobacionesViajes { get; set; } = new List<ComprobacionesViaje>();
}
