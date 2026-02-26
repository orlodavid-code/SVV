using System.Collections.Generic;

namespace SVV.DTOs.Cotizacion
{
    public class ResultadoCotizacionDto
    {
        public bool EsCalculoAutomatico { get; set; }
        public string? Mensaje { get; set; }
        public decimal? DistanciaCalculada { get; set; }
        public decimal TotalTransporte { get; set; }
        public decimal TotalGasolina { get; set; }
        public decimal TotalUberTaxi { get; set; }
        public decimal TotalCasetas { get; set; }
        public decimal TotalHospedaje { get; set; }
        public decimal TotalAlimentos { get; set; }
        public decimal TotalGeneral { get; set; }

        public bool EsValido => Errores == null || Errores.Count == 0;

        // Nuevas propiedades
        public string? EstadoDestino { get; set; }
        public string? ZonaAlimentos { get; set; }
        public string? ZonaHospedaje { get; set; }

        public List<ConceptoItemDto> DetalleTransporte { get; set; } = new List<ConceptoItemDto>();
        public List<ConceptoItemDto> DetalleGasolina { get; set; } = new List<ConceptoItemDto>();
        public List<ConceptoItemDto> DetalleUberTaxi { get; set; } = new List<ConceptoItemDto>();
        public List<ConceptoItemDto> DetalleCasetas { get; set; } = new List<ConceptoItemDto>();
        public List<ConceptoItemDto> DetalleHospedaje { get; set; } = new List<ConceptoItemDto>();
        public List<ConceptoItemDto> DetalleAlimentos { get; set; } = new List<ConceptoItemDto>();

        public List<string> Alertas { get; set; } = new List<string>();
        public List<string> Errores { get; set; } = new List<string>();

        // Para compatibilidad con la vista
        public bool Success => EsValido;
    }
}