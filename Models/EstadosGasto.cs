using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class EstadosGasto
{
    public int Id { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public virtual ICollection<GastosReales> GastosReales { get; set; } = new List<GastosReales>();
}
