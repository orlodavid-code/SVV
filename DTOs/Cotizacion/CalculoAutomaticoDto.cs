using System;
using System.Collections.Generic;

namespace SVV.DTOs.Cotizacion
{
    //  AÑADE ESTA DEFINICIÓN
    public class ConceptoItemDto
    {
        public decimal Precio { get; set; }
        public string? Descripcion { get; set; }
    }

    public class CalculoAutomaticoDto
    {
        public int SolicitudViajeId { get; set; }
        public string UbicacionBase { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;
        public DateOnly? FechaSalida { get; set; }
        public DateOnly? FechaRegreso { get; set; }
        public TimeOnly? HoraSalida { get; set; }
        public TimeOnly? HoraRegreso { get; set; }
        public int NumeroPersonas { get; set; } = 1;
        public bool RequiereHospedaje { get; set; }
        public int NochesHospedaje { get; set; }
        public string MedioTraslado { get; set; } = "Vehículo Utilitario";
        public bool RequiereTaxiDomicilio { get; set; }
        public string? DireccionTaxiOrigen { get; set; }
        public string? DireccionTaxiDestino { get; set; }
    }

    public class ResultadoCalculoAutomaticoDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public decimal? DistanciaCalculada { get; set; }
        public string? TiempoEstimado { get; set; }
        public DesgloseCalculoDto? Desglose { get; set; }
        public List<string>? Alertas { get; set; }
        public List<string>? Errores { get; set; }

        public List<ConceptoItemDto>? DetallesTransporte { get; set; }
        public List<ConceptoItemDto>? DetallesGasolina { get; set; }
        public List<ConceptoItemDto>? DetallesUberTaxi { get; set; }
        public List<ConceptoItemDto>? DetallesCasetas { get; set; }
        public List<ConceptoItemDto>? DetallesHospedaje { get; set; }
        public List<ConceptoItemDto>? DetallesAlimentos { get; set; }
    }

    public class DesgloseCalculoDto
    {
        public decimal Transporte { get; set; }
        public decimal Gasolina { get; set; }
        public decimal UberTaxi { get; set; }
        public decimal Casetas { get; set; }
        public decimal Hospedaje { get; set; }
        public decimal Alimentos { get; set; }
        public decimal Total => Transporte + Gasolina + UberTaxi + Casetas + Hospedaje + Alimentos;
    }
}