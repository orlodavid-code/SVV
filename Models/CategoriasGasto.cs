using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class CategoriasGasto
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public bool? RequiereFactura { get; set; }

    public bool? AplicaLimiteDiario { get; set; }

    public bool? Activo { get; set; }

    public virtual ICollection<GastosReales> GastosReales { get; set; } = new List<GastosReales>();
}
