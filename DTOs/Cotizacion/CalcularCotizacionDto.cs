using System;
using System.Collections.Generic;

namespace SVV.DTOs.Cotizacion
{
    public class CalcularCotizacionDto
    {
        public int SolicitudViajeId { get; set; }

        public string? Origen { get; set; }
        public string? Destino { get; set; }

        public DateOnly? FechaSalida { get; set; }
        public DateOnly? FechaRegreso { get; set; }
        public TimeOnly? HoraSalida { get; set; }
        public TimeOnly? HoraRegreso { get; set; }
        public int NumeroPersonas { get; set; } = 1;
        public bool RequiereHospedaje { get; set; }
        public int NochesHospedaje { get; set; }

        public string? MedioTraslado { get; set; }
        public bool RequiereTaxiDomicilio { get; set; }
        public string? DireccionTaxiOrigen { get; set; }
        public string? DireccionTaxiDestino { get; set; }
        public bool EsCalculoAutomatico { get; set; }

        public List<ConceptoItemDto> Transporte { get; set; } = new();
        public List<ConceptoItemDto> Gasolina { get; set; } = new();
        public List<ConceptoItemDto> UberTaxi { get; set; } = new();
        public List<ConceptoItemDto> Casetas { get; set; } = new();
        public List<ConceptoItemDto> Hospedaje { get; set; } = new();
        public List<ConceptoItemDto> Alimentos { get; set; } = new();
    }
}