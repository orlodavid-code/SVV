using System;
using System.Collections.Generic;

namespace SVV.Models;

public partial class GastosReales
{
    public int Id { get; set; }

    public int SolicitudViajeId { get; set; }

    public int CategoriaGastoId { get; set; }

    public string Concepto { get; set; } = null!;

    public DateOnly FechaGasto { get; set; }

    public decimal Monto { get; set; }

    public string? Proveedor { get; set; }

    public string? Descripcion { get; set; }

    public string? MedioPago { get; set; }

    public bool? PagoConTarjeta { get; set; }

    public bool? FueraHorarioLaboral { get; set; }

    public bool? AplicaLimiteHorario { get; set; }

    public string? LugarGasto { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int EstadoGastoId { get; set; }

    public virtual CategoriasGasto CategoriaGasto { get; set; } = null!;

    public virtual EstadosGasto EstadoGasto { get; set; } = null!;

    public virtual Facturas? Factura { get; set; }

    public virtual SolicitudesViaje SolicitudViaje { get; set; } = null!;
}
