using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SVV.Services
{
    public interface IRazorViewToStringRenderer
    {
        Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model);
    }

    public class RazorViewToStringRenderer : IRazorViewToStringRenderer
    {
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public RazorViewToStringRenderer(
            IRazorViewEngine viewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public async Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model)
        {
            var actionContext = GetActionContext();
            var view = FindView(actionContext, viewName);

            using var output = new StringWriter();

            var viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };

            var tempData = new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);

            var viewContext = new ViewContext(
                actionContext,
                view,
                viewData,
                tempData,
                output,
                new HtmlHelperOptions()
            );

            viewContext.RouteData = new RouteData();

            await view.RenderAsync(viewContext);
            return output.ToString();
        }

        private IView FindView(ActionContext actionContext, string viewName)
        {
            var getViewResult = _viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: false);
            if (getViewResult.Success)
                return getViewResult.View;

            getViewResult = _viewEngine.GetView(executingFilePath: null, viewPath: viewName + ".cshtml", isMainPage: false);
            if (getViewResult.Success)
                return getViewResult.View;

            getViewResult = _viewEngine.GetView(executingFilePath: null, viewPath: "/Views/Emails/SolicitudCreada.cshtml", isMainPage: false);
            if (getViewResult.Success)
                return getViewResult.View;

            var findViewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: false);
            if (findViewResult.Success)
                return findViewResult.View;

            var searchedLocations = string.Join(", ", getViewResult.SearchedLocations);
            throw new InvalidOperationException($"No se pudo encontrar la vista '{viewName}'. Ubicaciones buscadas: {searchedLocations}");
        }

        private ActionContext GetActionContext()
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext == null)
            {
                httpContext = new DefaultHttpContext
                {
                    RequestServices = _serviceProvider
                };

                // Leer la URL base desde la configuración (appsettings.json)
                var baseUrl = _configuration["AppBaseUrl"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    var uri = new Uri(baseUrl);
                    httpContext.Request.Scheme = uri.Scheme;
                    httpContext.Request.Host = new HostString(uri.Host, uri.Port);
                    httpContext.Request.PathBase = uri.AbsolutePath.TrimEnd('/');
                }
                else
                {
                    // Fallback seguro: usar localhost (solo para desarrollo)
                    httpContext.Request.Scheme = "http";
                    httpContext.Request.Host = new HostString("localhost");
                }
            }

            return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        }
    }
}