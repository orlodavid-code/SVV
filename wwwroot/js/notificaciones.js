
class NotificacionesManager {
    constructor() {
        this.config = {
            theme: {
                background: '#2b2e38',
                color: '#f1f1f1',
                success: '#5cc87b',
                warning: '#f39c12',
                error: '#e74c3c',
                info: '#3498db'
            },
            animations: {
                show: 'animate__animated animate__fadeInDown',
                hide: 'animate__animated animate__fadeOutUp',
                success: 'animate__animated animate__bounceInRight',
                error: 'animate__animated animate__wobble'
            }
        };
        this.init();
    }

    // INICIALIZACIÓN PRINCIPAL DEL SISTEMA
    init() {
        this.configurarEventListeners();
        this.mostrarAlertasTempData();
        this.configurarInterceptores();
    }

    // CONFIGURACIÓN DE EVENT LISTENERS PARA BOTONES DE ACCIÓN
    configurarEventListeners() {
        this.configurarBotones('.btn-aprobar', 'confirmarAprobacion');
        this.configurarBotones('.btn-rechazar', 'confirmarRechazo');
        this.configurarBotones('.btn-aprobar-dashboard', 'confirmarAprobacion');
        this.configurarBotones('.btn-rechazar-dashboard', 'confirmarRechazo');
        this.configurarBotones('.btn-enviar-aprobacion', 'confirmarEnvioAprobacion');
        this.configurarBotones('.btn-eliminar', 'confirmarEliminacion');
        this.configurarBotonesUtilidad();
    }

    // CONFIGURACIÓN GENÉRICA DE BOTONES POR SELECTOR
    configurarBotones(selector, metodo) {
        document.querySelectorAll(selector).forEach(btn => {
            btn.removeEventListener('click', this[metodo]);
            btn.addEventListener('click', (e) => this[metodo](e));
        });
    }

    // CONFIGURACIÓN DE BOTONES DE UTILIDAD (IMPRESIÓN, FILTROS, EXPORTACIÓN)
    configurarBotonesUtilidad() {
        const btnImprimir = document.getElementById('btnImprimir');
        const btnFiltros = document.getElementById('btnFiltros');
        const btnExportar = document.getElementById('btnExportar');

        if (btnImprimir) {
            btnImprimir.addEventListener('click', (e) => {
                e.preventDefault();
                this.mostrarConfirmacionImpresion();
            });
        }

        [btnFiltros, btnExportar].forEach(btn => {
            if (btn) {
                btn.addEventListener('click', (e) => {
                    e.preventDefault();
                    this.mostrarProximamente(btn.id === 'btnFiltros' ? 'Filtros' : 'Exportación');
                });
            }
        });
    }

    // INTERCEPTORES PARA FORMULARIOS CON CLASE 'needs-sweet-alert'
    configurarInterceptores() {
        document.addEventListener('submit', (e) => {
            const form = e.target;
            if (form.classList.contains('needs-sweet-alert')) {
                e.preventDefault();
                this.procesarEnvioFormulario(form);
            }
        });
    }

    // PROCESAMIENTO DE ALERTAS TEMPORALES EN DATA-ATTRIBUTES
    mostrarAlertasTempData() {
        const tipos = ['success', 'error', 'warning'];

        tipos.forEach(tipo => {
            const alerta = document.querySelector(`.alert-${tipo}[data-sweet-alert="${tipo}"]`);
            if (alerta) {
                const mensaje = alerta.textContent?.trim();
                if (mensaje && !this.esAlertaEstatica(mensaje)) {
                    this[`mostrarNotificacion${tipo.charAt(0).toUpperCase() + tipo.slice(1)}`](mensaje);
                    alerta.remove();
                }
            }
        });
    }

    // DETECCIÓN DE ALERTAS ESTÁTICAS (NO MOSTRAR COMO NOTIFICACIONES)
    esAlertaEstatica(mensaje) {
        const alertasEstaticas = [
            "Responsabilidad: Revisa y aprueba solicitudes de viáticos de tu equipo antes de que pasen a RH.",
            "Responsabilidad: Verifica que las solicitudes cumplan con las políticas de la empresa antes de enviarlas a Finanzas.",
            "Tu rol: Jefe de Proceso",
            "Tu rol: Recursos Humanos",
            "Plazos importantes:",
            "¿Sabías que?",
            "El presupuesto trimestral",
            "Tienes facturas pendientes"
        ];

        return alertasEstaticas.some(alerta => mensaje.includes(alerta));
    }

    // CONFIRMACIÓN DE APROBACIÓN CON DETALLES DE SOLICITUD
    confirmarAprobacion(event) {
        event.preventDefault();
        event.stopPropagation();

        const form = event.target.closest('form');
        const codigoSolicitud = event.target.getAttribute('data-solicitud-codigo') || 'SOL-XXXX';

        this.mostrarConfirmacion({
            title: '¿Aprobar Solicitud?',
            html: `¿Estás seguro de aprobar la solicitud <strong class="text-success">${codigoSolicitud}</strong>?<br>
                  <small class="text-warning">Esta acción no se puede deshacer</small>`,
            icon: 'question',
            confirmButtonText: 'Sí, aprobar',
            onConfirm: () => this.procesarEnvioFormulario(form, 'Aprobando solicitud...')
        });
    }

    // CONFIRMACIÓN DE RECHAZO CON DETALLES DE SOLICITUD
    confirmarRechazo(event) {
        event.preventDefault();
        event.stopPropagation();

        const form = event.target.closest('form');
        const codigoSolicitud = event.target.getAttribute('data-solicitud-codigo') || 'SOL-XXXX';

        this.mostrarConfirmacion({
            title: '¿Rechazar Solicitud?',
            html: `¿Estás seguro de rechazar la solicitud <strong class="text-danger">${codigoSolicitud}</strong>?<br>
                  <small class="text-warning">Esta acción no se puede deshacer</small>`,
            icon: 'warning',
            confirmButtonText: 'Sí, rechazar',
            confirmButtonColor: this.config.theme.error,
            onConfirm: () => this.procesarEnvioFormulario(form, 'Rechazando solicitud...')
        });
    }

    // CONFIRMACIÓN DE ENVÍO A APROBACIÓN CON VALIDACIÓN DE ELEMENTOS
    confirmarEnvioAprobacion(event) {
        event.preventDefault();
        event.stopPropagation();

        const boton = event.target.closest('.btn-enviar-aprobacion');
        if (!boton) {
            return;
        }

        const form = boton.closest('form');
        if (!form) {
            this.mostrarNotificacionError('Error: No se pudo encontrar el formulario');
            return;
        }

        const codigoSolicitud = boton.getAttribute('data-solicitud-codigo') || 'SOL-XXXX';

        this.mostrarConfirmacion({
            title: '¿Enviar a Aprobación?',
            html: `¿Estás seguro de enviar la solicitud <strong class="text-info">${codigoSolicitud}</strong> a aprobación?<br>
                  <small class="text-warning"><i class="fas fa-exclamation-triangle me-1"></i>Una vez enviada, no podrás editarla</small>`,
            icon: 'question',
            confirmButtonText: 'Sí, enviar',
            onConfirm: () => {
                this.mostrarCargando('Enviando solicitud...');
                this.procesarEnvioFormulario(form, 'Enviando solicitud...');
            }
        });
    }

    // CONFIRMACIÓN DE ELIMINACIÓN CON MANEJO DE ENLACES Y FORMULARIOS
    confirmarEliminacion(event) {
        event.preventDefault();
        event.stopPropagation();

        const boton = event.target.closest('.btn-eliminar');
        if (!boton) {
            this.mostrarNotificacionError('Error: No se pudo encontrar el botón de eliminación');
            return;
        }

        const esEnlace = boton.tagName === 'A';
        let urlEliminar = '';

        if (esEnlace) {
            urlEliminar = boton.getAttribute('href');
            if (!urlEliminar) {
                this.mostrarNotificacionError('Error: No se pudo encontrar la URL de eliminación');
                return;
            }
        } else {
            const form = boton.closest('form');
            if (!form) {
                this.mostrarNotificacionError('Error: No se pudo encontrar el formulario');
                return;
            }
            this.mostrarConfirmacion({
                title: '¿Eliminar Solicitud?',
                html: `¿Estás seguro de eliminar esta solicitud?<br>
                  <small class="text-danger"><i class="fas fa-exclamation-triangle me-1"></i>Esta acción no se puede deshacer</small>`,
                icon: 'warning',
                confirmButtonText: 'Sí, eliminar',
                confirmButtonColor: this.config.theme.error,
                onConfirm: () => this.procesarEnvioFormulario(form, 'Eliminando solicitud...')
            });
            return;
        }

        this.mostrarConfirmacion({
            title: '¿Eliminar Solicitud?',
            html: `¿Estás seguro de eliminar esta solicitud?<br>
              <small class="text-danger"><i class="fas fa-exclamation-triangle me-1"></i>Esta acción te llevará a una pantalla de confirmación</small>`,
            icon: 'warning',
            confirmButtonText: 'Sí, continuar',
            confirmButtonColor: this.config.theme.error,
            onConfirm: () => {
                this.mostrarCargando('Redirigiendo...');
                setTimeout(() => {
                    window.location.href = urlEliminar;
                }, 500);
            }
        });
    }

    // SISTEMA REUTILIZABLE DE CONFIRMACIÓN SWEETALERT
    mostrarConfirmacion(config) {
        const {
            title,
            html,
            icon = 'question',
            confirmButtonText = 'Confirmar',
            confirmButtonColor = this.config.theme.success,
            onConfirm
        } = config;

        Swal.fire({
            title,
            html,
            icon,
            showCancelButton: true,
            confirmButtonColor,
            cancelButtonColor: '#6c757d',
            confirmButtonText: `<i class="fas fa-check me-2"></i>${confirmButtonText}`,
            cancelButtonText: '<i class="fas fa-times me-2"></i>Cancelar',
            background: this.config.theme.background,
            color: this.config.theme.color,
            iconColor: confirmButtonColor,
            customClass: {
                popup: 'sweetalert-dark',
                confirmButton: 'btn-sweet-confirm',
                cancelButton: 'btn-sweet-cancel'
            },
            showClass: {
                popup: this.config.animations.show
            },
            hideClass: {
                popup: this.config.animations.hide
            }
        }).then((result) => {
            if (result.isConfirmed && onConfirm) {
                onConfirm();
            }
        });
    }

    // PROCESAMIENTO DE ENVÍO DE FORMULARIOS CON MANEJO DE ERRORES
    procesarEnvioFormulario(form, mensajeCargando = 'Procesando...') {
        if (!form) {
            this.mostrarNotificacionError('Error: No se pudo procesar el formulario');
            return;
        }

        this.mostrarCargando(mensajeCargando);

        setTimeout(() => {
            try {
                if (form && form.parentNode) {
                    form.submit();
                } else {
                    Swal.close();
                    this.mostrarNotificacionError('Error: El formulario ya no está disponible');
                }
            } catch (error) {
                Swal.close();
                this.mostrarNotificacionError('Error al procesar la solicitud: ' + error.message);
            }
        }, 500);
    }

    // CONFIRMACIÓN DE IMPRESIÓN CON PREPARACIÓN DE ESTILOS
    mostrarConfirmacionImpresion() {
        this.mostrarConfirmacion({
            title: '¿Generar PDF/Imprimir?',
            html: `¿Deseas generar un PDF de esta solicitud?<br>
              <small class="text-info"><i class="fas fa-info-circle me-1"></i>Se optimizará el diseño para impresión</small>`,
            icon: 'info',
            confirmButtonText: 'Sí, generar PDF',
            onConfirm: () => this.prepararYImprimir()
        });
    }

    // PREPARACIÓN DE DOCUMENTO PARA IMPRESIÓN CON ESTILOS TEMPORALES
    prepararYImprimir() {
        this.mostrarCargando('Preparando documento para impresión...');

        const elementosOcultar = [
            '.aprobaciones-header',
            '.detalles-botones',
            '.alert',
            '.btn-group',
            '.navbar',
            '.footer',
            '.custom-navbar',
            '.custom-footer'
        ];

        const elementosOriginales = [];

        elementosOcultar.forEach(selector => {
            const elementos = document.querySelectorAll(selector);
            elementos.forEach(el => {
                elementosOriginales.push({
                    element: el,
                    display: el.style.display
                });
                el.style.display = 'none';
            });
        });

        const estiloImpresion = `
        @media print {
            body {
                background: white !important;
                color: black !important;
                font-size: 12pt;
            }
            .recent-activity-card {
                background: white !important;
                color: black !important;
                box-shadow: none !important;
                border: 1px solid #ddd !important;
                margin: 0 !important;
                padding: 0 !important;
            }
            .text-accent, .text-success, .viamtek-green {
                color: #006400 !important;
            }
            .badge {
                border: 1px solid #333 !important;
                color: #333 !important;
                background: #f8f9fa !important;
            }
            .btn-group, .navbar, .footer, .alert, .aprobaciones-header, .detalles-botones {
                display: none !important;
            }
            .seccion {
                break-inside: avoid;
                page-break-inside: avoid;
                margin-bottom: 1rem !important;
            }
            .doc-header {
                border-bottom: 2px solid #006400 !important;
            }
            .dato-valor {
                background: #f8f9fa !important;
                border: 1px solid #dee2e6 !important;
                color: #333 !important;
            }
        }
        @page {
            size: A4;
            margin: 1cm;
        }
    `;

        const styleSheet = document.createElement("style");
        styleSheet.type = "text/css";
        styleSheet.id = "estilo-impresion-temporal";
        styleSheet.innerText = estiloImpresion;
        document.head.appendChild(styleSheet);

        setTimeout(() => {
            Swal.close();
            window.print();

            setTimeout(() => {
                elementosOriginales.forEach(item => {
                    if (item.element && item.element.style) {
                        item.element.style.display = item.display;
                    }
                });

                const estilo = document.getElementById('estilo-impresion-temporal');
                if (estilo && estilo.parentNode) {
                    estilo.parentNode.removeChild(estilo);
                }

                this.mostrarToast('PDF generado correctamente', 'success');
            }, 500);
        }, 1000);
    }

    // ALERTA PARA FUNCIONALIDADES EN DESARROLLO
    mostrarProximamente(funcionalidad) {
        Swal.fire({
            title: `${funcionalidad}`,
            text: 'Esta funcionalidad estará disponible próximamente',
            icon: 'info',
            confirmButtonColor: this.config.theme.info,
            background: this.config.theme.background,
            color: this.config.theme.color,
            customClass: {
                popup: 'sweetalert-dark'
            },
            showClass: {
                popup: 'animate__animated animate__bounceIn'
            }
        });
    }

    // NOTIFICACIÓN DE ÉXITO CON TIMER AUTOMÁTICO
    mostrarNotificacionExito(mensaje) {
        if (this.esAlertaEstatica(mensaje)) return;

        Swal.fire({
            title: '¡Éxito!',
            text: mensaje,
            icon: 'success',
            confirmButtonColor: this.config.theme.success,
            background: this.config.theme.background,
            color: this.config.theme.color,
            iconColor: this.config.theme.success,
            timer: 4000,
            timerProgressBar: true,
            showConfirmButton: false,
            toast: true,
            position: 'top-end',
            showClass: {
                popup: this.config.animations.success
            }
        });
    }

    // NOTIFICACIÓN DE ERROR CON BOTÓN DE CONFIRMACIÓN
    mostrarNotificacionError(mensaje) {
        if (this.esAlertaEstatica(mensaje)) return;

        Swal.fire({
            title: 'Error',
            text: mensaje,
            icon: 'error',
            confirmButtonColor: this.config.theme.error,
            background: this.config.theme.background,
            color: this.config.theme.color,
            iconColor: this.config.theme.error,
            timer: 5000,
            timerProgressBar: true,
            showConfirmButton: true,
            showClass: {
                popup: this.config.animations.error
            }
        });
    }

    // NOTIFICACIÓN DE ADVERTENCIA (ACTUALMENTE DESHABILITADA)
    mostrarNotificacionAdvertencia(mensaje) {
        return;
    }

    // VISUALIZACIÓN DE ESTADO DE CARGA
    mostrarCargando(mensaje = 'Procesando...') {
        Swal.fire({
            title: mensaje,
            allowEscapeKey: false,
            allowOutsideClick: false,
            showConfirmButton: false,
            didOpen: () => {
                Swal.showLoading();
            },
            background: this.config.theme.background,
            color: this.config.theme.color,
            customClass: {
                popup: 'sweetalert-dark'
            }
        });
    }

    // NOTIFICACIONES TOAST TEMPORALES
    mostrarToast(mensaje, tipo = 'info', posicion = 'top-end') {
        const iconos = {
            success: '',
            error: '',
            warning: '',
            info: ''
        };

        const Toast = Swal.mixin({
            toast: true,
            position: posicion,
            showConfirmButton: false,
            timer: 3000,
            timerProgressBar: true,
            background: this.config.theme.background,
            color: this.config.theme.color,
            didOpen: (toast) => {
                toast.addEventListener('mouseenter', Swal.stopTimer);
                toast.addEventListener('mouseleave', Swal.resumeTimer);
            },
            showClass: {
                popup: 'animate__animated animate__slideInRight'
            }
        });

        Toast.fire({
            title: `${iconos[tipo] || ''} ${mensaje}`,
            icon: tipo
        });
    }

    // NOTIFICACIÓN PERSONALIZADA GENÉRICA
    notificar(titulo, mensaje, tipo = 'info') {
        Swal.fire({
            title: titulo,
            text: mensaje,
            icon: tipo,
            confirmButtonColor: this.config.theme[tipo] || this.config.theme.info,
            background: this.config.theme.background,
            color: this.config.theme.color,
            customClass: {
                popup: 'sweetalert-dark'
            }
        });
    }

    // CONFIRMACIÓN PARA EXPORTACIÓN A EXCEL
    mostrarConfirmacionExportacionExcel() {
        this.mostrarConfirmacion({
            title: '¿Exportar a Excel?',
            html: `¿Estás seguro de que deseas descargar el listado de empleados en formato Excel?<br>
              <small class="text-info"><i class="fas fa-info-circle me-1"></i>Se generará un archivo .xlsx con todos los empleados activos</small>`,
            icon: 'question',
            confirmButtonText: 'Sí, descargar Excel',
            onConfirm: () => this.procesarExportacionExcelMejorada()
        });
    }

    // PROCESO DE EXPORTACIÓN A EXCEL CON BARRA DE PROGRESO
    procesarExportacionExcelMejorada() {
        this.mostrarCargando('Preparando archivo Excel...');

        setTimeout(() => {
            Swal.fire({
                title: 'Generando Excel',
                html: `
                <div class="text-center">
                    <div class="mb-3">
                        <i class="fas fa-file-excel fa-3x text-success"></i>
                    </div>
                    <p>Exportando empleados a formato Excel...</p>
                    <div class="progress mb-3" style="height: 10px;">
                        <div class="progress-bar progress-bar-striped progress-bar-animated" 
                             role="progressbar" style="width: 75%"></div>
                    </div>
                    <small class="text-muted">El archivo se descargará automáticamente</small>
                </div>
            `,
                showConfirmButton: false,
                allowOutsideClick: false,
                background: this.config.theme.background,
                color: this.config.theme.color,
                customClass: {
                    popup: 'sweetalert-dark'
                }
            });

            setTimeout(() => {
                try {
                    window.location.href = this.obtenerUrlExportacionExcel();
                    Swal.close();
                    this.mostrarToast('Archivo Excel generado correctamente', 'success');
                } catch (error) {
                    Swal.close();
                    this.mostrarNotificacionError('Error al generar el archivo Excel');
                }
            }, 2000);
        }, 500);
    }

    // OBTENCIÓN DE URL PARA EXPORTACIÓN DE EMPLEADOS
    obtenerUrlExportacionExcel() {
        const enlaceExportacion = document.querySelector('a[href*="ExportarEmpleadosExcel"]');
        if (enlaceExportacion) {
            return enlaceExportacion.href;
        }

        return '/Admin/ExportarEmpleadosExcel';
    }

    // CONFIRMACIÓN PARA EXPORTACIÓN DE SOLICITUDES A EXCEL
    mostrarConfirmacionExportacionExcelSolicitudes() {
        this.mostrarConfirmacion({
            title: '¿Exportar Solicitudes a Excel?',
            html: `¿Estás seguro de que deseas descargar el listado de solicitudes pendientes en formato Excel?<br>
              <small class="text-info"><i class="fas fa-info-circle me-1"></i>Se generará un archivo .xlsx con todas las solicitudes pendientes</small>`,
            icon: 'question',
            confirmButtonText: 'Sí, descargar Excel',
            onConfirm: () => this.procesarExportacionExcelSolicitudesMejorada()
        });
    }

    // PROCESO DE EXPORTACIÓN DE SOLICITUDES A EXCEL
    procesarExportacionExcelSolicitudesMejorada() {
        this.mostrarCargando('Preparando archivo Excel de solicitudes...');

        setTimeout(() => {
            const cantidadSolicitudes = document.querySelectorAll('.table-row-custom').length || document.querySelectorAll('table tbody tr').length;

            Swal.fire({
                title: 'Generando Excel de Solicitudes',
                html: `
                <div class="text-center">
                    <div class="mb-3">
                        <i class="fas fa-file-excel fa-3x text-success"></i>
                    </div>
                    <p>Exportando ${cantidadSolicitudes} solicitudes pendientes a formato Excel...</p>
                    <div class="progress mb-3" style="height: 10px;">
                        <div class="progress-bar progress-bar-striped progress-bar-animated" 
                             role="progressbar" style="width: 75%"></div>
                    </div>
                    <small class="text-muted">El archivo se descargará automáticamente</small>
                </div>
            `,
                showConfirmButton: false,
                allowOutsideClick: false,
                background: this.config.theme.background,
                color: this.config.theme.color,
                customClass: {
                    popup: 'sweetalert-dark'
                }
            });

            setTimeout(() => {
                try {
                    window.location.href = this.obtenerUrlExportacionExcelSolicitudes();
                    Swal.close();
                    this.mostrarToast('Archivo Excel de solicitudes generado correctamente', 'success');
                } catch (error) {
                    Swal.close();
                    this.mostrarNotificacionError('Error al generar el archivo Excel de solicitudes');
                }
            }, 2000);
        }, 500);
    }

    // OBTENCIÓN DE URL PARA EXPORTACIÓN DE SOLICITUDES
    obtenerUrlExportacionExcelSolicitudes() {
        const enlaceExportacion = document.querySelector('a[href*="ExportarSolicitudesPendientesExcel"]');
        if (enlaceExportacion) {
            return enlaceExportacion.href;
        }

        return '/Aprobaciones/ExportarSolicitudesPendientesExcel';
    }
}

// INICIALIZACIÓN DEL SISTEMA CON VERIFICACIÓN DE DEPENDENCIAS
document.addEventListener('DOMContentLoaded', function () {
    try {
        if (typeof Swal === 'undefined') {
            return;
        }

        window.notificaciones = new NotificacionesManager();

        window.mostrarNotificacionExito = (mensaje) => window.notificaciones.mostrarNotificacionExito(mensaje);
        window.mostrarNotificacionError = (mensaje) => window.notificaciones.mostrarNotificacionError(mensaje);
        window.mostrarToast = (mensaje, tipo) => window.notificaciones.mostrarToast(mensaje, tipo);

    } catch (error) {
        console.error('Error al inicializar el sistema de notificaciones:', error);
    }
});

// INYECCIÓN DE ESTILOS CSS PARA MEJORAS VISUALES
const estilosNotificaciones = `
.loading-spinner {
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 1.1em;
}

.loading-sweet .swal2-loader {
    border-color: #5cc87b transparent #5cc87b transparent !important;
}

.btn-sweet-confirm {
    background: linear-gradient(135deg, #308184, #5cc87b) !important;
    border: none !important;
    border-radius: 8px !important;
    padding: 12px 24px !important;
    font-weight: 600 !important;
    transition: all 0.3s ease !important;
}

.btn-sweet-confirm:hover {
    transform: translateY(-2px) !important;
    box-shadow: 0 6px 20px rgba(92, 200, 123, 0.4) !important;
}

.btn-sweet-cancel {
    background: #6c757d !important;
    border: none !important;
    border-radius: 8px !important;
    padding: 12px 24px !important;
    font-weight: 600 !important;
    transition: all 0.3s ease !important;
}

.btn-sweet-cancel:hover {
    background: #5a6268 !important;
    transform: translateY(-2px) !important;
}

.btn-sweet-danger {
    background: linear-gradient(135deg, #c0392b, #e74c3c) !important;
}

.btn-sweet-danger:hover {
    box-shadow: 0 6px 20px rgba(231, 76, 60, 0.4) !important;
}
`;

if (!document.querySelector('#estilos-notificaciones')) {
    const style = document.createElement('style');
    style.id = 'estilos-notificaciones';
    style.textContent = estilosNotificaciones;
    document.head.appendChild(style);
}