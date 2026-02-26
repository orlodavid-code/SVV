using System;

namespace SVV.ViewModels
{
    public class SolicitudDashboardViewModel
    {
        public int IdSolicitud { get; set; }  // ✅ AGREGAR ESTA PROPIEDAD
        public string Codigo { get; set; }
        public string EmpleadoNombre { get; set; }
        public string NombreProyecto { get; set; }
        public string Destino { get; set; } = null!;
        public string MontoSolicitado { get; set; }
        public string FechaCreacion { get; set; }
        public string EstadoNombre { get; set; }
        public string EstadoCssClass { get; set; } // Clase CSS para el badge (pending, review)
    }
}
