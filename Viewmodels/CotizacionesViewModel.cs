using SVV.Models;
using SVV.ViewModels;

namespace SVV.ViewModels;

public class CotizacionesViewModel 
{
    public List<ConceptoItemViewModel> Concepto { get; set; }
    public CotizacionesFinanzas Cotizaciones { get; set; }

}
