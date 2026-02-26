using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SVV.ViewModels
{
    public class ComprobacionCorreccionViewModel
    {
        [Required(ErrorMessage = "El ID de comprobación es requerido")]
        public int ComprobacionId { get; set; }

        public string CodigoComprobacion { get; set; } = "";
        public string ComentariosFinanzas { get; set; } = "";

        public string EmpleadoNombre { get; set; } = "";
        public string EmpleadoEmail { get; set; } = "";

        public decimal TotalComprobacion { get; set; }
        public decimal TotalAnticipo { get; set; }
        public decimal Diferencia { get; set; }

        public string EstadoActual { get; set; } = "DEVUELTO_CORRECCION";
        public int EstadoComprobacionId { get; set; }

        public bool EsVistaEmpleado { get; set; } = true;
        public bool EsVistaFinanzas { get; set; } = false;

        // Lista de gastos devueltos
        public List<GastoCorreccionViewModel> GastosDevueltos { get; set; } = new List<GastoCorreccionViewModel>();

        public string ComentariosDevolucion { get; set; } = "Finanzas devolvió este gasto para corrección";
        public List<int> GastosSeleccionados { get; set; } = new List<int>();

        public bool TodosLosArchivosSubidos => GastosDevueltos.All(g =>
    (!string.IsNullOrEmpty(g.FacturaPDFCorregido) || !string.IsNullOrEmpty(g.FacturaXMLCorregido)));

        public int ArchivosCorregidosCount => GastosDevueltos.Count(g =>
            !string.IsNullOrEmpty(g.FacturaPDFCorregido) || !string.IsNullOrEmpty(g.FacturaXMLCorregido));

        public int TotalArchivosEsperados => GastosDevueltos.Count * 2; // PDF + XML por gasto

        public decimal PorcentajeCompletitud => TotalArchivosEsperados > 0
            ? (decimal)ArchivosCorregidosCount / TotalArchivosEsperados * 100
            : 0;
    }

    public class GastoCorreccionViewModel
    {
        [Required(ErrorMessage = "El ID del gasto es requerido")]
        public int GastoId { get; set; }

        // Propiedades que vienen del formulario
        [Required(ErrorMessage = "El concepto es requerido")]
        public string Concepto { get; set; } = "";

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "La categoría es requerida")]
        public string Categoria { get; set; } = "";

        [Required(ErrorMessage = "La fecha del gasto es requerida")]
        [DataType(DataType.Date)]
        public DateTime FechaGasto { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "El proveedor es requerido")]
        public string Proveedor { get; set; } = "";

        // Archivos - estos vienen del formulario como IFormFile
        [Display(Name = "Archivo PDF Corregido")]
        [DataType(DataType.Upload)]
        public IFormFile? ArchivoPDF { get; set; }

        [Display(Name = "Archivo XML Corregido")]
        [DataType(DataType.Upload)]
        public IFormFile? ArchivoXML { get; set; }

        // Propiedades de solo visualización
        [BindNever]
        [JsonIgnore]
        public string ComentarioFinanzas { get; set; } = "";

        [BindNever]
        [JsonIgnore]
        public string FacturaPDFActual { get; set; } = "";

        [BindNever]
        [JsonIgnore]
        public string FacturaXMLActual { get; set; } = "";

        [BindNever]
        [JsonIgnore]
        public string FacturaPDFCorregido { get; set; } = "";

        [BindNever]
        [JsonIgnore]
        public string FacturaXMLCorregido { get; set; } = "";

        [BindNever]
        [JsonIgnore]
        public string EstadoGasto { get; set; } = "Devuelto para corrección";

        [BindNever]
        [JsonIgnore]
        public string EstadoGastoCodigo { get; set; } = "DEVUELTO_CORRECCION";

        [BindNever]
        [JsonIgnore]
        public string EstadoActual { get; set; } = "DEVUELTO_CORRECCION";

        [BindNever]
        [JsonIgnore]
        public bool TieneCorrecciones { get; set; }

        [BindNever]
        [JsonIgnore]
        public bool XmlEsValido { get; set; }

        [BindNever]
        [JsonIgnore]
        public string ErroresValidacionXml { get; set; } = "";

        [BindNever]
        [JsonIgnore]
        public string PDFParaMostrar => !string.IsNullOrEmpty(FacturaPDFCorregido) ? FacturaPDFCorregido : FacturaPDFActual;

        [BindNever]
        [JsonIgnore]
        public string XMLParaMostrar => !string.IsNullOrEmpty(FacturaXMLCorregido) ? FacturaXMLCorregido : FacturaXMLActual;

        // Métodos de ayuda (solo para vista, no se serializan)
        [BindNever]
        [JsonIgnore]
        public string DisplayMonto => Monto.ToString("C");

        [BindNever]
        [JsonIgnore]
        public string DisplayFecha => FechaGasto.ToString("dd/MM/yyyy");

        [BindNever]
        [JsonIgnore]
        public bool TienePDF => !string.IsNullOrEmpty(FacturaPDFActual) || !string.IsNullOrEmpty(FacturaPDFCorregido);

        [BindNever]
        [JsonIgnore]
        public bool TieneXML => !string.IsNullOrEmpty(FacturaXMLActual) || !string.IsNullOrEmpty(FacturaXMLCorregido);

        [BindNever]
        [JsonIgnore]
        public string EstadoBadgeClass
        {
            get
            {
                return EstadoGastoCodigo switch
                {
                    "APROBADO" => "badge bg-success",
                    "DEVUELTO_CORRECCION" => "badge bg-warning",
                    "RECHAZADO" => "badge bg-danger",
                    "CORREGIDO" => "badge bg-info",
                    "PENDIENTE" => "badge bg-secondary",
                    _ => "badge bg-secondary"
                };
            }
        }
    }

    // ViewModel para la vista de revisión de Finanzas
    public class RevisionCorreccionViewModel : ComprobacionCorreccionViewModel
    {
        // Información adicional para Finanzas
        public DateTime FechaCorreccion { get; set; } = DateTime.Now;
        public string NombreEmpleado { get; set; } = "";
        public string DepartamentoEmpleado { get; set; } = "";

        // Opciones de acción para Finanzas
        public string AccionRecomendada { get; set; } = "OBSERVAR_MENORES";
        public List<string> OpcionesAccion { get; } = new List<string>
        {
            "APROBAR_CORRECCION",
            "DEVOLVER_CORRECCION",
            "OBSERVAR_MENORES"
        };

        // Métricas de validación
        public int TotalGastos { get; set; }
        public int GastosConXmlValido { get; set; }
        public int GastosConPdfValido { get; set; }

        public decimal PorcentajeCompletitud { get; set; }

        // Constructor
        public RevisionCorreccionViewModel()
        {
            EsVistaEmpleado = false;
            EsVistaFinanzas = true;
        }
    }

    // ViewModel para respuesta de procesamiento
    public class ProcesarCorreccionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int ComprobacionId { get; set; }
        public string CodigoComprobacion { get; set; } = "";
        public int NuevoEstado { get; set; }
        public string AccionRealizada { get; set; } = "";
        public List<int> GastosAfectados { get; set; } = new List<int>();
        public DateTime FechaProcesamiento { get; set; } = DateTime.Now;
        public string RedirectUrl { get; set; } = "";
    }

    // ViewModel para el modal de devolución
    public class DevolucionGastosViewModel
    {
        public int ComprobacionId { get; set; }
        public string CodigoComprobacion { get; set; } = "";

        [Required(ErrorMessage = "Los comentarios son obligatorios")]
        [StringLength(1000, ErrorMessage = "Los comentarios deben tener entre 10 y 1000 caracteres", MinimumLength = 10)]
        public string Comentarios { get; set; } = "";

        [Required(ErrorMessage = "Debe seleccionar al menos un gasto")]
        public List<int> GastosSeleccionados { get; set; } = new List<int>();

        // Tipos de corrección
        public string TipoCorreccion { get; set; } = "OTRO";
        public List<string> TiposCorreccion { get; } = new List<string>
        {
            "FALTA_XML",
            "XML_INVALIDO",
            "PDF_INCOMPLETO",
            "MONTO_INCORRECTO",
            "PROVEEDOR_INVALIDO",
            "OTRO"
        };

        // Plazo para corrección
        public int DiasPlazo { get; set; } = 3;
        public DateTime FechaLimite => DateTime.Now.AddDays(DiasPlazo);
    }

    // Atributo personalizado para validación de tamaño de archivo
    public class MaxFileSizeAttribute : ValidationAttribute
    {
        private readonly int _maxFileSize;

        public MaxFileSizeAttribute(int maxFileSize)
        {
            _maxFileSize = maxFileSize;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is IFormFile file)
            {
                if (file.Length > _maxFileSize)
                {
                    return new ValidationResult(ErrorMessage);
                }
            }

            return ValidationResult.Success;
        }
    }
}