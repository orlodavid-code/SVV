// autorizaciones-pago.js- 

document.addEventListener('DOMContentLoaded', function () {
    // PUNTO DE ENTRADA PRINCIPAL - INICIALIZACIÓN DEL MÓDULO
    initAutorizaciones();
});

// CONFIGURACIÓN INICIAL Y COMPONENTES
//  Inicialización principal del módulo de autorizaciones
function initAutorizaciones() {
    initDataTable();
    initEventHandlers();
    loadEstadisticas();
}

//Configuración de DataTable para tablas de comprobaciones
function initDataTable() {
    $('#dataTable').DataTable({
        language: {
            url: '//cdn.datatables.net/plug-ins/1.13.6/i18n/es-ES.json'
        },
        order: [[1, 'desc']], // Orden por fecha descendente (columna 1)
        pageLength: 10,
        responsive: true
    });
}

//Configuración de manejadores de eventos para toda la página
function initEventHandlers() {
    // MANEJADOR PARA SELECCIÓN MASIVA (CHECKBOX "SELECCIONAR TODO")
    $('#selectAll').on('click', function () {
        $('.select-comprobacion').prop('checked', this.checked);
        updateAutorizarButton();
    });

    // ACTUALIZACIÓN DEL BOTÓN DE AUTORIZACIÓN AL CAMBIAR SELECCIONES
    $('.select-comprobacion').on('change', updateAutorizarButton);

    // MANEJADOR PARA AUTORIZACIÓN MASIVA DE COMPROBACIONES SELECCIONADAS
    $('#btnAutorizarSeleccionados').on('click', autorizarSeleccionados);

    // DELEGACIÓN DE EVENTOS PARA BOTONES DINÁMICOS EN FILAS
    $(document).on('click', '.btn-autorizar', function () {
        const id = $(this).data('id');
        autorizarComprobacion(id);
    });

    $(document).on('click', '.btn-detalle', function () {
        const id = $(this).data('id');
        mostrarDetalle(id);
    });

    // MANEJADORES PARA FILTROS DINÁMICOS
    $('#filtroEmpleado, #filtroEscenario, #filtroEstado').on('change', aplicarFiltros);
}

//  GESTIÓN DE INTERFAZ Y ESTADOS
// Actualiza el estado del botón de autorización masiva
//Cambia texto y habilitación según cantidad de elementos seleccionados
function updateAutorizarButton() {
    const seleccionados = $('.select-comprobacion:checked').length;
    const btn = $('#btnAutorizarSeleccionados');

    if (seleccionados > 0) {
        btn.prop('disabled', false);
        btn.html(`<i class="fas fa-check-double me-1"></i>Autorizar (${seleccionados})`);
    } else {
        btn.prop('disabled', true);
        btn.html('<i class="fas fa-check-double me-1"></i>Autorizar Seleccionados');
    }
}

//Calcula y actualiza estadísticas financieras en tiempo real
 // Total a pagar, total a reintegrar y contadores de pendientes
function loadEstadisticas() {
    let totalPagar = 0;
    let totalReintegrar = 0;
    let pendientes = 0;

    $('.select-comprobacion').each(function () {
        const diferencia = parseFloat($(this).data('diferencia')) || 0;

        if (diferencia > 0) {
            totalPagar += diferencia;
            pendientes++;
        } else if (diferencia < 0) {
            totalReintegrar += Math.abs(diferencia);
            pendientes++;
        }
    });

    $('#contadorPendientes').text(pendientes);
    $('#totalPagar').text('$' + totalPagar.toFixed(2));
    $('#totalReintegrar').text('$' + totalReintegrar.toFixed(2));
    $('#totalComprobaciones').text($('.select-comprobacion').length);
}

// Aplica filtros combinados a la tabla de comprobaciones
// Filtra por empleado, escenario y estado simultáneamente
function aplicarFiltros() {
    const empleadoId = $('#filtroEmpleado').val();
    const escenario = $('#filtroEscenario').val();
    const estado = $('#filtroEstado').val();

    $('tbody tr').each(function () {
        let mostrar = true;
        const row = $(this);

        // APLICACIÓN DE FILTROS EN CASCADA
        if (empleadoId && row.data('empleado') != empleadoId) mostrar = false;
        if (escenario && row.data('escenario') != escenario) mostrar = false;
        if (estado && row.data('estado') != estado) mostrar = false;

        row.toggle(mostrar);
    });

    // RE-DIBUJADO DE DATATABLE PARA MANTENER FUNCIONALIDADES
    if ($.fn.DataTable.isDataTable('#dataTable')) {
        $('#dataTable').DataTable().draw();
    }
}

// ============================================================
// SECCIÓN: GESTIÓN DE DETALLES Y MODALES
// Muestra detalles de una comprobación específica en modal
function mostrarDetalle(id) {
    Swal.fire({
        title: 'Cargando detalles...',
        allowOutsideClick: false,
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' },
        didOpen: () => Swal.showLoading()
    });

    // SOLICITUD AJAX PARA OBTENER DETALLES ESPECÍFICOS
    $.get(`/Finanzas/DetallesComprobacion/${id}`, function (data) {
        Swal.fire({
            title: 'Detalles de Comprobación',
            html: data,
            width: '90%',
            showCloseButton: true,
            showConfirmButton: true,
            confirmButtonText: 'Autorizar',
            showDenyButton: true,
            denyButtonText: 'Cerrar',
            showCancelButton: true,
            cancelButtonText: 'Ver Completo',
            background: 'var(--card-bg)',
            color: 'var(--primary-text)',
            customClass: { popup: 'sweetalert-dark' }
        }).then((result) => {
            if (result.isConfirmed) {
                autorizarComprobacion(id);
            } else if (result.dismiss === Swal.DismissReason.cancel) {
                window.open(`/Comprobaciones/Detalles/${id}`, '_blank');
            }
        });
    }).fail(() => {
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'No se pudieron cargar los detalles',
            background: 'var(--card-bg)',
            color: 'var(--primary-text)',
            customClass: { popup: 'sweetalert-dark' }
        });
    });
}

// AUTORIZACIÓN INDIVIDUAL Y MASIVA

/* Autoriza una comprobación individual con confirmación previa
 * @param {number} id - ID de la comprobación a autorizar
 */
function autorizarComprobacion(id) {
    Swal.fire({
        title: '¿Autorizar comprobación?',
        text: 'Esta acción autorizará el pago/reintegro según el escenario.',
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'Sí, autorizar',
        cancelButtonText: 'Cancelar',
        reverseButtons: true,
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' }
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.fire({
                title: 'Autorizando...',
                allowOutsideClick: false,
                background: 'var(--card-bg)',
                color: 'var(--primary-text)',
                customClass: { popup: 'sweetalert-dark' },
                didOpen: () => Swal.showLoading()
            });

            // ENVÍO DE SOLICITUD DE AUTORIZACIÓN AL SERVIDOR
            $.post(`/Finanzas/AutorizarComprobacion/${id}`, function (response) {
                if (response.success) {
                    Swal.fire({
                        title: '¡Autorizado!',
                        text: response.message,
                        icon: 'success',
                        timer: 2000,
                        showConfirmButton: false,
                        background: 'var(--card-bg)',
                        color: 'var(--primary-text)',
                        customClass: { popup: 'sweetalert-dark' }
                    }).then(() => location.reload());
                } else {
                    Swal.fire({
                        icon: 'error',
                        title: 'Error',
                        text: response.message,
                        background: 'var(--card-bg)',
                        color: 'var(--primary-text)',
                        customClass: { popup: 'sweetalert-dark' }
                    });
                }
            }).fail(() => {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: 'Error en la solicitud',
                    background: 'var(--card-bg)',
                    color: 'var(--primary-text)',
                    customClass: { popup: 'sweetalert-dark' }
                });
            });
        }
    });
}

// Procesa autorización masiva de múltiples comprobaciones seleccionadas
 * Incluye validación y confirmación previa al envío masivo
 */
function autorizarSeleccionados() {
    const ids = [];
    $('.select-comprobacion:checked').each(function () {
        ids.push($(this).data('id'));
    });

    // VALIDACIÓN DE SELECCIÓN MÍNIMA
    if (ids.length === 0) {
        Swal.fire({
            icon: 'warning',
            title: 'Advertencia',
            text: 'Selecciona al menos una comprobación',
            background: 'var(--card-bg)',
            color: 'var(--primary-text)',
            customClass: { popup: 'sweetalert-dark' }
        });
        return;
    }

    Swal.fire({
        title: `¿Autorizar ${ids.length} comprobaciones?`,
        html: `Se autorizarán <strong>${ids.length}</strong> comprobaciones seleccionadas.<br>
               <small>Esta acción no se puede deshacer.</small>`,
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: `Sí, autorizar ${ids.length}`,
        cancelButtonText: 'Cancelar',
        reverseButtons: true,
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' }
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.fire({
                title: 'Autorizando...',
                html: `Procesando ${ids.length} comprobaciones...`,
                allowOutsideClick: false,
                background: 'var(--card-bg)',
                color: 'var(--primary-text)',
                customClass: { popup: 'sweetalert-dark' },
                didOpen: () => Swal.showLoading()
            });

            // ENVÍO MASIVO DE AUTORIZACIONES AL SERVIDOR
            $.post('/Finanzas/AutorizarMultiplesComprobaciones', { ids: ids }, function (response) {
                if (response.success) {
                    Swal.fire({
                        title: '¡Completado!',
                        html: `${response.message}<br>
                               <strong>${response.autorizadas}</strong> autorizadas correctamente`,
                        icon: 'success',
                        timer: 3000,
                        showConfirmButton: false,
                        background: 'var(--card-bg)',
                        color: 'var(--primary-text)',
                        customClass: { popup: 'sweetalert-dark' }
                    }).then(() => location.reload());
                } else {
                    Swal.fire({
                        icon: 'error',
                        title: 'Error',
                        text: response.message,
                        background: 'var(--card-bg)',
                        color: 'var(--primary-text)',
                        customClass: { popup: 'sweetalert-dark' }
                    });
                }
            }).fail(() => {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: 'Error en la solicitud',
                    background: 'var(--card-bg)',
                    color: 'var(--primary-text)',
                    customClass: { popup: 'sweetalert-dark' }
                });
            });
        }
    });
}