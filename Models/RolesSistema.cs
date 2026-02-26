using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class RolesSistema
{
    public int Id { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public int? NivelAprobacion { get; set; }

    public virtual ICollection<Empleados> Empleados { get; set; } = new List<Empleados>();
}
