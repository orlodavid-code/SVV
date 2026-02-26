using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class AuditoriaSistema
{
    public int Id { get; set; }

    public int? EmpleadoId { get; set; }

    public string Accion { get; set; } = null!;

    public string Entidad { get; set; } = null!;

    public int? EntidadId { get; set; }

    public string? ValoresAnteriores { get; set; }

    public string? ValoresNuevos { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Empleados? Empleado { get; set; }
}
