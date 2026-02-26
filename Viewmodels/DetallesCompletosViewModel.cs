using System;
using System.Collections.Generic;

namespace SVV.ViewModels
{
    public class DetallesCompletosViewModel
    {
        // Información de la comprobación
        public int ComprobacionId { get; set; }
        public string CodigoComprobacion { get; set; }
        public DateTime FechaComprobacion { get; set; }
        public string EstadoComprobacion { get; set; }
        public decimal TotalAnticipo { get; set; }
        public decimal TotalGastosComprobados { get; set; }
        public decimal Diferencia { get; set; }
        public string EscenarioLiquidacion { get; set; }
        public string DescripcionActividades { get; set; }
        public string ResultadosViaje { get; set; }

        // Información de la solicitud
        public int SolicitudId { get; set; }
        public string CodigoSolicitud { get; set; }
        public string EmpleadoNombre { get; set; }
        public string Departamento { get; set; }
        public string Proyecto { get; set; }
        public string Destino { get; set; }
        public DateOnly FechaSalida { get; set; }
        public DateOnly FechaRegreso { get; set; }
        public string Motivo { get; set; }
        public string EstadoSolicitud { get; set; }
        public string TipoViatico { get; set; }

        // Información de la cotización
        public int? CotizacionId { get; set; }
        public string CodigoCotizacion { get; set; }
        public decimal TotalAutorizadoCotizacion { get; set; }
        public string EstadoCotizacion { get; set; }

        // Listas
        public List<AnticipoViewModel> Anticipos { get; set; } = new();
        public List<GastoViewModel> Gastos { get; set; } = new();
    }

    public class AnticipoViewModel
    {
        public int Id { get; set; }
        public string CodigoAnticipo { get; set; }
        public decimal MontoSolicitado { get; set; }
        public decimal MontoAutorizado { get; set; }
        public string Estado { get; set; }
        public DateTime FechaSolicitud { get; set; }
    }

    public class GastoViewModel
    {
        public int Id { get; set; }
        public string Concepto { get; set; }
        public string Categoria { get; set; }
        public decimal Monto { get; set; }
        public DateOnly FechaGasto { get; set; }
        public string Proveedor { get; set; }
    }
    public class DetalleGastoReporteViewModel
    {
        public int ComprobacionId { get; set; }
        public int SolicitudId { get; set; }
        public int? CotizacionId { get; set; }
        public string Empleado { get; set; }
        public string Departamento { get; set; }
        public string Proyecto { get; set; }
        public decimal Anticipo { get; set; }
        public decimal Gastado { get; set; }
        public decimal Diferencia { get; set; }
        public string Escenario { get; set; }
        public string Estado { get; set; }
        public string CodigoSolicitud { get; set; }
        public string CodigoComprobacion { get; set; }
        public string CodigoCotizacion { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public DateTime? FechaComprobacion { get; set; }
    }
}