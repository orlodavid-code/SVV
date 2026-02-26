using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SVV.Models;

namespace SVV.ViewModels
{
    public class CrearSolicitudViewModel
    {
        public int Id { get; set; } // Agregado para la edición

        [Required(ErrorMessage = "El nombre del proyecto es requerido")]
        [Display(Name = "Nombre del Proyecto")]
        [StringLength(200, ErrorMessage = "El nombre del proyecto no puede tener más de 200 caracteres")]
        public string NombreProyecto { get; set; }

        [Required(ErrorMessage = "El destino es requerido")]
        [StringLength(200, ErrorMessage = "El destino no puede tener más de 200 caracteres")]
        [Display(Name = "Destino del Viaje")]
        public string Destino { get; set; }

        [Required(ErrorMessage = "La fecha de salida es requerida")]
        [Display(Name = "Fecha de Salida")]
        [DataType(DataType.Date)]
        public DateTime FechaSalida { get; set; } = DateTime.Today.AddDays(3);

        [Required(ErrorMessage = "La fecha de regreso es requerida")]
        [Display(Name = "Fecha de Regreso")]
        [DataType(DataType.Date)]
        public DateTime FechaRegreso { get; set; } = DateTime.Today.AddDays(4);

        [Required(ErrorMessage = "El tipo de viático es requerido")]
        [Display(Name = "Tipo de Viático")]
        public int TipoViaticoId { get; set; }

        [Required(ErrorMessage = "El motivo es requerido")]
        [StringLength(500, ErrorMessage = "El motivo no puede tener más de 500 caracteres")]
        [Display(Name = "Motivo del Viaje")]
        public string Motivo { get; set; }

        [Required(ErrorMessage = "La empresa visitada es requerida")]
        [StringLength(200, ErrorMessage = "La empresa visitada no puede tener más de 200 caracteres")]
        [Display(Name = "Empresa Visitada")]
        public string EmpresaVisitada { get; set; }

        [Required(ErrorMessage = "El lugar de comisión es requerido")]
        [StringLength(500, ErrorMessage = "El lugar de comisión no puede tener más de 500 caracteres")]
        [Display(Name = "Lugar de Comisión Detallado")]
        public string LugarComisionDetallado { get; set; }

        // Campos opcionales del modelo original
        [Display(Name = "Dirección Destino")]
        [StringLength(300)]
        public string? DireccionEmpresa { get; set; }

        [Display(Name = "Medio de Traslado Principal")]
        public string? MedioTrasladoPrincipal { get; set; }

        [Display(Name = "¿Requiere taxi desde domicilio?")]
        public bool RequiereTaxiDomicilio { get; set; }

        [Display(Name = "Dirección de origen (domicilio)")]
        [StringLength(300)]
        public string? DireccionTaxiOrigen { get; set; }

        [Display(Name = "Dirección de destino")]
        [StringLength(300)]
        public string? DireccionTaxiDestino { get; set; }

        [Display(Name = "¿Requiere hospedaje?")]
        public bool RequiereHospedaje { get; set; }

        [Display(Name = "Noches de hospedaje")]
        [Range(0, 30, ErrorMessage = "El número de noches debe estar entre 0 y 30")]
        public int? NochesHospedaje { get; set; }

        [Display(Name = "Hora de Salida")]
        [DataType(DataType.Time)]
        public TimeSpan? HoraSalida { get; set; }

        [Display(Name = "Hora de Regreso")]
        [DataType(DataType.Time)]
        public TimeSpan? HoraRegreso { get; set; }

        [Display(Name = "Número de Personas")]
        [Range(1, 50, ErrorMessage = "Debe haber entre 1 y 50 personas")]
        public int NumeroPersonas { get; set; } = 1;

        [Display(Name = "¿Requiere anticipo?")]
        public bool requiere_anticipo { get; set; }

        public List<TiposViatico> TiposViatico { get; set; } = new List<TiposViatico>();

        [Display(Name = "Nombres de las Personas")]
        [Required(ErrorMessage = "Debe ingresar al menos el nombre del solicitante")]
        [MinLength(1, ErrorMessage = "Debe ingresar al menos el nombre del solicitante")]
        public List<string> NombresPersonas { get; set; } = new List<string>();

        // Validaciones personalizadas
        public bool FechasValidas()
        {
            return FechaRegreso >= FechaSalida;
        }

        public bool CumplePlazoMinimo()
        {
            var diasTotales = (FechaSalida.Date - DateTime.Today).TotalDays;
            return diasTotales >= 3; // Ahora incluye sábados y domingos
        }

        public bool NombresPersonasValidos()
        {
            if (NombresPersonas == null || NombresPersonas.Count == 0)
                return false;

            // Verificar que al menos el solicitante tenga nombre
            if (string.IsNullOrWhiteSpace(NombresPersonas[0]))
                return false;

            // Verificar que el número de nombres coincida con el número de personas
            if (NombresPersonas.Count != NumeroPersonas)
                return false;

            // Verificar que todos los nombres tengan valor
            foreach (var nombre in NombresPersonas)
            {
                if (string.IsNullOrWhiteSpace(nombre))
                    return false;
            }

            return true;
        }

        public string? GetNombreSolicitante()
        {
            return NombresPersonas?.Count > 0 ? NombresPersonas[0] : null;
        }

        public List<string> GetNombresColaboradores()
        {
            var colaboradores = new List<string>();

            if (NombresPersonas?.Count > 1)
            {
                for (int i = 1; i < NombresPersonas.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(NombresPersonas[i]))
                    {
                        colaboradores.Add(NombresPersonas[i]);
                    }
                }
            }

            return colaboradores;
        }

        public string? GetColaboradoresFormateados()
        {
            var colaboradores = GetNombresColaboradores();
            return colaboradores.Count > 0 ? string.Join(", ", colaboradores) : null;
        }
    }
}