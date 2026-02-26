using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SVV.ViewModels
{
    public class FacturasViewModel
    {
        public int ComprobacionId { get; set; }
        public string CodigoComprobacion { get; set; }
        public string CodigoSolicitud { get; set; }
        public string EmpleadoNombre { get; set; }
        public string Destino { get; set; }
        public DateTime FechaComprobacion { get; set; }
        public decimal? TotalGastosComprobados { get; set; }
        public decimal? TotalAnticipo { get; set; }
        public decimal? Diferencia { get; set; }
        public string EstadoComprobacion { get; set; }
        public int EstadoComprobacionId { get; set; }
        public string ComentariosFinanzas { get; set; }
        public string DescripcionActividades { get; set; }
        public string ResultadosViaje { get; set; }
        public List<GastoFacturaViewModel> Gastos { get; set; } = new List<GastoFacturaViewModel>();
        public bool TieneFacturasPendientes { get; set; }

        // Propiedades calculadas para facilitar el uso en vistas
        public string DisplayDiferencia
        {
            get
            {
                return Diferencia?.ToString("C") ?? "$0.00";
            }
        }

        public string DisplayTotalGastos
        {
            get
            {
                return TotalGastosComprobados?.ToString("C") ?? "$0.00";
            }
        }

        public string DisplayTotalAnticipo
        {
            get
            {
                return TotalAnticipo?.ToString("C") ?? "$0.00";
            }
        }

        public string ClaseCssDiferencia
        {
            get
            {
                return (Diferencia ?? 0) >= 0 ? "text-success" : "text-danger";
            }
        }
    }

    public class GastoFacturaViewModel
    {
        public int GastoId { get; set; }
        public string Categoria { get; set; }
        public string Concepto { get; set; }
        public DateTime FechaGasto { get; set; }
        public decimal Monto { get; set; }
        public string Proveedor { get; set; }
        public string FacturaPDF { get; set; }
        public string FacturaXML { get; set; }
        public string EstadoValidacion { get; set; }
        public string ErroresValidacion { get; set; }
        public bool TieneXML { get; set; }
        public bool TienePDF { get; set; }
        public string EstadoGasto { get; set; }
        public int EstadoGastoId { get; set; }
        public string EstadoGastoCodigo { get; set; }

        // Propiedades calculadas
        public string DisplayMonto { get { return Monto.ToString("C"); } }
        public string DisplayFecha { get { return FechaGasto.ToString("dd/MM/yyyy"); } }

        public string EstadoBadgeClass
        {
            get
            {
                return TieneXML ? "bg-success" : "bg-warning";
            }
        }

        public string EstadoTexto
        {
            get
            {
                return TieneXML ? "COMPLETA" : "PENDIENTE XML";
            }
        }

        public string EstadoGastoBadgeClass
        {
            get
            {
                return EstadoGastoCodigo switch
                {
                    "APROBADO" => "bg-success",
                    "RECHAZADO" => "bg-danger",
                    "DEVUELTO_CORRECCION" => "bg-warning",
                    _ => "bg-secondary"
                };
            }
        }
    }
}