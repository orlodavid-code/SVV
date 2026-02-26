using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SVV.ViewModels
{
    public class ReembolsoParcialViewModel
    {
        public int ComprobacionId { get; set; }
        public string CodigoComprobacion { get; set; } = string.Empty;
        public string EmpleadoNombre { get; set; } = string.Empty;
        public string EmpleadoEmail { get; set; } = string.Empty;
        public decimal TotalComprobacion { get; set; }
        public decimal TotalAnticipo { get; set; }
        public decimal Diferencia { get; set; }

        [Required(ErrorMessage = "Debe agregar comentarios para el empleado")]
        public string Comentarios { get; set; } = string.Empty;

        public List<GastoReembolsoViewModel> Gastos { get; set; } = new List<GastoReembolsoViewModel>();
    }

    public class GastoReembolsoViewModel
    {
        public int GastoId { get; set; }
        public string Concepto { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public DateTime FechaGasto { get; set; }
        public string Proveedor { get; set; } = string.Empty;
        public bool TieneXML { get; set; }
        public bool TienePDF { get; set; }
        public bool Seleccionado { get; set; }
        public string EstadoActual { get; set; } = string.Empty;
        public string EstadoGastoCodigo { get; set; } = string.Empty;
        public string? ComentarioEspecifico { get; set; }

        // Propiedades calculadas
        public string DisplayMonto => Monto.ToString("C");
        public string DisplayFecha => FechaGasto.ToString("dd/MM/yyyy");

        public string EstadoDocumentosBadgeClass => TieneXML && TienePDF ? "bg-success" : "bg-warning";
        public string EstadoDocumentosTexto => TieneXML && TienePDF ? "COMPLETO" : "INCOMPLETO";

        public string EstadoGastoBadgeClass => EstadoGastoCodigo switch
        {
            "APROBADO" => "bg-success",
            "RECHAZADO" => "bg-danger",
            "DEVUELTO_CORRECCION" => "bg-warning",
            _ => "bg-secondary"
        };
    }
}