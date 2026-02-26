using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class Notificaciones
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }

    public string Tipo { get; set; } = null!;

    public string Titulo { get; set; } = null!;

    public string Mensaje { get; set; } = null!;

    public string? EntidadRelacionada { get; set; }

    public int? EntidadId { get; set; }

    public bool? Leida { get; set; }

    public DateTime? FechaEnvio { get; set; }

    public string? Prioridad { get; set; }

    public virtual Empleados Empleado { get; set; } = null!;
}
