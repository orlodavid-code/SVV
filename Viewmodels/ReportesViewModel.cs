using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SVV.ViewModels
{
    public class DashboardReportesViewModel
    {
        public ResumenGeneralViewModel Resumen { get; set; }
        public FiltrosReportesViewModel Filtros { get; set; }
        public List<string> Departamentos { get; set; } // Agregar esta propiedad

    }

    public class ResumenGeneralViewModel
    {
        public decimal TotalGastado { get; set; }
        public decimal TotalAnticipos { get; set; }
        public int TotalSolicitudes { get; set; }
        public int TotalComprobaciones { get; set; }
        public decimal PromedioAnticipo { get; set; }
        public int SolicitudesAprobadas { get; set; }
        public int SolicitudesPendientes { get; set; }
    }

    public class FiltrosReportesViewModel
    {
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int? DepartamentoId { get; set; }
        public int? EmpleadoId { get; set; }
        public string? Escenario { get; set; }
        public string? Departamento { get; set; }
    }
}