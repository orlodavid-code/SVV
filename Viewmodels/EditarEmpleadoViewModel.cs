using System.ComponentModel.DataAnnotations;

namespace SVV.ViewModels
{
    public class EditarEmpleadoViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "Los apellidos son requeridos")]
        [StringLength(100, ErrorMessage = "Los apellidos no pueden exceder 100 caracteres")]
        public string Apellidos { get; set; }

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        [StringLength(150, ErrorMessage = "El email no puede exceder 150 caracteres")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "El formato del teléfono no es válido")]
        [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres")]
        public string Telefono { get; set; }

        [Required(ErrorMessage = "El rol es requerido")]
        public int RolId { get; set; }

        public int? JefeDirectoId { get; set; }

        [Required(ErrorMessage = "El área de adscripción es requerida")]
        [StringLength(100, ErrorMessage = "El área de adscripción no puede exceder 100 caracteres")]
        public string AreaAdscripcion { get; set; }

        [StringLength(100, ErrorMessage = "El departamento no puede exceder 100 caracteres")]
        public string Departamento { get; set; }

        [StringLength(100, ErrorMessage = "El puesto no puede exceder 100 caracteres")]
        public string Puesto { get; set; }

        [StringLength(50, ErrorMessage = "El nivel de puesto no puede exceder 50 caracteres")]
        public string NivelPuesto { get; set; }

        [StringLength(100, ErrorMessage = "La ubicación base no puede exceder 100 caracteres")]
        public string UbicacionBase { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? FechaIngreso { get; set; }

        public bool ColaboradorRemoto { get; set; } // Cambiar a bool no nullable
       }
}