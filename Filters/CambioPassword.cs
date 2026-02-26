using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace SVV.Filters
{
    // Filtro de acción para verificar si el usuario debe cambiar su contraseña temporal
    public class CambioPassword : ActionFilterAttribute
    {
        // Este método se ejecuta antes de cualquier acción del controlador
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Verificar si el usuario está autenticado en el sistema
            if (context.HttpContext.User.Identity.IsAuthenticated)
            {
                // Buscar el claim que indica si la contraseña es temporal
                var contraseñaTemporalClaim = context.HttpContext.User.FindFirst("ContraseñaTemporal");

                // Si el usuario tiene contraseña temporal activa
                if (contraseñaTemporalClaim != null && contraseñaTemporalClaim.Value == "True")
                {
                    // Obtener el controlador y acción actual de la ruta
                    var controllerName = context.RouteData.Values["controller"].ToString();
                    var actionName = context.RouteData.Values["action"].ToString();

                    // Definir las rutas que están exentas del cambio obligatorio de contraseña
                    var rutasPermitidas = new[]
                    {
                        new { Controller = "Perfil", Action = "CambiarPassword" },
                        new { Controller = "Perfil", Action = "MiPerfil" },
                        new { Controller = "Auth", Action = "Logout" },
                        new { Controller = "Auth", Action = "LogoutGet" },
                        new { Controller = "Home", Action = "Error" }
                    };

                    // Verificar si la ruta actual está en la lista de rutas permitidas
                    bool esRutaPermitida = rutasPermitidas.Any(r =>
                        r.Controller.Equals(controllerName, StringComparison.OrdinalIgnoreCase) &&
                        r.Action.Equals(actionName, StringComparison.OrdinalIgnoreCase));

                    // Si la ruta actual no está permitida, redirigir al cambio de contraseña
                    if (!esRutaPermitida)
                    {
                        // Redireccionar a la página de cambio de contraseña con mensaje
                        context.Result = new RedirectToActionResult("CambiarPassword", "Perfil", new
                        {
                            mensaje = "Debes cambiar tu contraseña temporal antes de acceder a esta sección"
                        });
                    }
                }
            }

            // Continuar con la ejecución normal del filtro
            base.OnActionExecuting(context);
        }
    }
}