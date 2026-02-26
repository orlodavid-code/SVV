using System.ComponentModel.DataAnnotations;

namespace SVV.ViewModels
{

    public class GuardarInformeViewModel
    {
        public int SolicitudId { get; set; }

        [Required(ErrorMessage = "La descripción de actividades es obligatoria")]
        [MinLength(50, ErrorMessage = "La descripción debe tener al menos 50 caracteres")]
        public string DescripcionActividades { get; set; }

        [Required(ErrorMessage = "Los resultados del viaje son obligatorios")]
        [MinLength(30, ErrorMessage = "Los resultados deben tener al menos 30 caracteres")]
        public string ResultadosViaje { get; set; }
    }
}
