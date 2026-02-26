// comprobaciones.js - Sistema de Comprobación de Gastos de Viáticos
// Responsable: Gestión completa de comprobaciones, validaciones XML, envío a finanzas
// Dependencias: DataTables, jQuery, SweetAlert2, Bootstrap 5, DOMParser

// INICIALIZACIÓN PRINCIPAL DEL MÓDULO
document.addEventListener('DOMContentLoaded', function () {
    inicializarComponentes();
    configurarValidaciones();
    verificarFechaLimite();
    inicializarAlertasGuardado();
});

// CONFIGURACIÓN DE COMPONENTES DE INTERFAZ
function inicializarComponentes() {
    inicializarTooltips();
    inicializarSweetAlertComprobaciones();
    inicializarDataTablesComprobaciones();
    inicializarDragAndDrop();
}

// INICIALIZACIÓN DE TOOLTIPS DE BOOTSTRAP
function inicializarTooltips() {
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl));
}

// CONFIGURACIÓN DE EVENTOS PARA SWEETALERT Y BOTONES DE ACCIÓN
function inicializarSweetAlertComprobaciones() {
    document.querySelectorAll('.btn-eliminar-gasto').forEach(boton => {
        boton.addEventListener('click', function (e) {
            e.preventDefault();
            const gastoId = this.getAttribute('data-gasto-id');
            const concepto = this.getAttribute('data-gasto-concepto');
            const monto = this.getAttribute('data-gasto-monto');
            if (gastoId && concepto && monto) {
                mostrarConfirmacionEliminarGasto(gastoId, concepto, monto);
            }
        });
    });

    const btnEnviarFinanzas = document.getElementById('btnEnviarFinanzas');
    if (btnEnviarFinanzas) {
        btnEnviarFinanzas.addEventListener('click', function (e) {
            e.preventDefault();
            manejarEnvioFinanzas();
        });
    }
}

// MANEJO DEL PROCESO DE ENVÍO A FINANZAS
function manejarEnvioFinanzas() {
    const estadoGastos = verificarGastosConXML();

    if (estadoGastos.total === 0) {
        mostrarAlerta('warning', 'Sin Gastos Registrados', 'No hay gastos registrados para enviar a finanzas.');
        return;
    }

    if (!estadoGastos.todosTienenXML) {
        mostrarAlertaArchivosFaltantes(estadoGastos);
    } else {
        mostrarConfirmacionEnviarFinanzas(estadoGastos);
    }
}

// ALERTA PARA GASTOS SIN ARCHIVOS ADJUNTOS
function mostrarAlertaArchivosFaltantes(estadoGastos) {
    Swal.fire({
        title: 'Archivos Faltantes',
        html: `
            <div class="text-left">
                <p>Algunos gastos no tienen archivos adjuntos:</p>
                <div style="background: rgba(245, 158, 11, 0.1); padding: 1rem; border-radius: 8px; margin: 1rem 0;">
                    <strong>Resumen:</strong><br>
                    • Total de gastos: <strong>${estadoGastos.total}</strong><br>
                    • Con archivos: <strong style="color: var(--viamtek-green);">${estadoGastos.conXML}</strong><br>
                    • Sin archivos: <strong style="color: #e74c3c;">${estadoGastos.total - estadoGastos.conXML}</strong>
                </div>
            </div>
        `,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Enviar de Todas Formas',
        cancelButtonText: 'Revisar Gastos',
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' }
    }).then((result) => {
        if (result.isConfirmed) {
            mostrarConfirmacionEnviarFinanzas(estadoGastos);
        }
    });
}

// INICIALIZACIÓN DE DATATABLES PARA TABLA DE GASTOS
function inicializarDataTablesComprobaciones() {
    const tablaGastos = $('#tablaGastos');

    if (typeof $.fn.DataTable === 'undefined') {
        return;
    }

    if (!tablaGastos.length || $.fn.DataTable.isDataTable('#tablaGastos')) {
        return;
    }

    try {
        tablaGastos.DataTable({
            "language": {
                "url": "//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json"
            },
            "order": [[2, "desc"]],
            "responsive": true,
            "autoWidth": false,
            "pageLength": 10,
            "destroy": true,
            "retrieve": true,
            "dom": '<"row"<"col-sm-12 col-md-6"l><"col-sm-12 col-md-6"f>>rt<"row"<"col-sm-12 col-md-5"i><"col-sm-12 col-md-7"p>>',
            "drawCallback": function (settings) {
                const nuevosTooltips = document.querySelectorAll('[data-bs-toggle="tooltip"]:not(.bs-tooltip-initialized)');
                nuevosTooltips.forEach(el => {
                    new bootstrap.Tooltip(el);
                    el.classList.add('bs-tooltip-initialized');
                });
            }
        });
    } catch (error) {
        console.error('Error al inicializar DataTables:', error);
    }
}

// IMPLEMENTACIÓN DE DRAG AND DROP PARA SUBIDA DE ARCHIVOS
function inicializarDragAndDrop() {
    const areasUpload = document.querySelectorAll('.upload-area');
    areasUpload.forEach(area => {
        area.addEventListener('dragover', function (e) {
            e.preventDefault();
            this.classList.add('dragover');
        });

        area.addEventListener('dragleave', function (e) {
            e.preventDefault();
            this.classList.remove('dragover');
        });

        area.addEventListener('drop', function (e) {
            e.preventDefault();
            this.classList.remove('dragover');
            const files = e.dataTransfer.files;
            if (files.length > 0) {
                const input = this.querySelector('input[type="file"]');
                if (input) {
                    input.files = files;
                    const event = new Event('change', { bubbles: true });
                    input.dispatchEvent(event);
                }
            }
        });
    });
}

// CONFIGURACIÓN DE VALIDACIONES DE FORMULARIOS
function configurarValidaciones() {
    configurarFormularioGastos();
    configurarFormularioInforme();
}

// VALIDACIÓN DEL FORMULARIO DE GASTOS INDIVIDUALES
function configurarFormularioGastos() {
    const formGasto = document.getElementById('formGasto');
    if (!formGasto) return;

    formGasto.addEventListener('submit', function (e) {
        if (!validarFormularioGasto()) {
            e.preventDefault();
            return;
        }

        deshabilitarBotones(this, '<i class="fas fa-spinner fa-spin me-1"></i> Guardando...');
    });

    const fechaGastoInput = formGasto.querySelector('input[name="FechaGasto"]');
    if (fechaGastoInput) {
        fechaGastoInput.addEventListener('change', function () {
            this.setCustomValidity(this.value ? '' : 'Seleccione una fecha para el gasto.');
        });
    }

    const archivoPDF = formGasto.querySelector('input[name="ArchivoPDF"]');
    const archivoXML = formGasto.querySelector('input[name="ArchivoXML"]');

    if (archivoPDF) {
        archivoPDF.addEventListener('change', function () {
            validarArchivo(this, 5, 'PDF');
        });
    }

    if (archivoXML) {
        archivoXML.addEventListener('change', function () {
            if (validarArchivo(this, 2, 'XML')) {
                validarXML(this);
            } else {
                const validationResult = document.getElementById('xmlValidationResult');
                if (validationResult) validationResult.innerHTML = '';
            }
        });
    }
}

// VALIDACIÓN GENERAL DEL FORMULARIO DE GASTO
function validarFormularioGasto() {
    const archivoXML = document.querySelector('input[name="ArchivoXML"]');
    const validationResult = document.getElementById('xmlValidationResult');

    if (!archivoXML || !archivoXML.files[0]) {
        mostrarAlertaXMLRequerido();
        return false;
    }

    if (validationResult && validationResult.querySelector('.alert-comprobacion-danger')) {
        mostrarAlertaXMLInvalido();
        return false;
    }

    const formGasto = document.getElementById('formGasto');
    if (!formGasto.checkValidity()) {
        formGasto.classList.add('was-validated');
        return false;
    }

    return true;
}

// CONFIGURACIÓN DEL FORMULARIO DE INFORME CON ENVÍO ASÍNCRONO
function configurarFormularioInforme() {
    const formInforme = document.getElementById('formInforme');
    if (!formInforme) return;

    formInforme.addEventListener('submit', async function (e) {
        e.preventDefault();

        if (!validarFormularioInforme()) {
            return;
        }

        const submitBtn = formInforme.querySelector('button[type="submit"]');
        const originalText = submitBtn.innerHTML;
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Guardando...';

        try {
            const formData = new FormData(formInforme);
            const data = Object.fromEntries(formData.entries());

            const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

            const response = await fetch(formInforme.action, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(data)
            });

            const result = await response.json();

            if (result.success) {
                Swal.fire({
                    title: 'Informe Guardado',
                    html: `
                        <div class="text-center">
                            <div class="mb-3">
                                <i class="fas fa-check-circle fa-3x" style="color: var(--viamtek-green);"></i>
                            </div>
                            <p>El informe de actividades y resultados se ha guardado correctamente.</p>
                            <div class="alert-comprobacion-success mt-3 p-2" style="text-align: left;">
                                <strong>Estado:</strong> Informe completado<br>
                                <strong>Próximo paso:</strong> Enviar a Finanzas
                            </div>
                        </div>
                    `,
                    icon: 'success',
                    confirmButtonText: 'Aceptar',
                    background: 'var(--card-bg)',
                    color: 'var(--primary-text)',
                    customClass: { popup: 'sweetalert-dark' },
                    buttonsStyling: false,
                    confirmButtonColor: 'var(--viamtek-green)',
                    timer: 4000,
                    timerProgressBar: true
                });

                const btnEnviarFinanzas = document.getElementById('btnEnviarFinanzas');
                if (btnEnviarFinanzas && btnEnviarFinanzas.disabled) {
                    btnEnviarFinanzas.disabled = false;
                }
            } else {
                Swal.fire({
                    title: 'Error',
                    text: result.message || 'Error al guardar el informe',
                    icon: 'error',
                    confirmButtonText: 'Reintentar',
                    background: 'var(--card-bg)',
                    color: 'var(--primary-text)',
                    customClass: { popup: 'sweetalert-dark' }
                });
            }
        } catch (error) {
            Swal.fire({
                title: 'Error de conexión',
                text: 'No se pudo conectar con el servidor. Verifica tu conexión a internet.',
                icon: 'error',
                confirmButtonText: 'Entendido',
                background: 'var(--card-bg)',
                color: 'var(--primary-text)',
                customClass: { popup: 'sweetalert-dark' }
            });
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalText;
        }
    });
}

// VALIDACIÓN DE CAMPOS DEL FORMULARIO DE INFORME
function validarFormularioInforme() {
    const descripcion = document.getElementById('DescripcionActividades');
    const resultados = document.getElementById('ResultadosViaje');
    let errores = false;

    if (descripcion.value.length < 50) {
        descripcion.setCustomValidity('La descripción debe tener al menos 50 caracteres.');
        descripcion.reportValidity();
        errores = true;
    } else {
        descripcion.setCustomValidity('');
    }

    if (resultados.value.length < 30) {
        resultados.setCustomValidity('Los resultados deben tener al menos 30 caracteres.');
        resultados.reportValidity();
        errores = true;
    } else {
        resultados.setCustomValidity('');
    }

    const formInforme = document.getElementById('formInforme');
    if (!formInforme.checkValidity()) {
        formInforme.classList.add('was-validated');
        errores = true;
    }

    return !errores;
}

// DESHABILITACIÓN DE BOTONES DURANTE PROCESOS DE GUARDADO
function deshabilitarBotones(formulario, texto) {
    const botones = formulario.querySelectorAll('button[type="submit"]');
    botones.forEach(boton => {
        boton.disabled = true;
        boton.innerHTML = texto;
    });
}

// VALIDACIÓN DE ARCHIVOS POR TAMAÑO Y TIPO
function validarArchivo(input, maxSizeMB, tipo) {
    if (input.files.length === 0) return false;
    const file = input.files[0];
    const fileSize = file.size / 1024 / 1024;

    if (fileSize > maxSizeMB) {
        mostrarMensaje('error', `El archivo ${tipo} excede el tamaño máximo de ${maxSizeMB}MB`);
        input.value = '';
        return false;
    }

    const extensiones = {
        'PDF': ['.pdf', 'application/pdf'],
        'XML': ['.xml', 'text/xml', 'application/xml']
    };

    const [extension, tipoMIME] = extensiones[tipo];
    const tieneExtension = file.name.toLowerCase().endsWith(extension);
    const tieneTipo = file.type.includes(tipoMIME.split('/')[1]);

    if (!tieneExtension && !tieneTipo) {
        mostrarMensaje('error', `Solo se permiten archivos ${tipo}`);
        input.value = '';
        return false;
    }

    return true;
}

// VALIDACIÓN DE ESTRUCTURA XML Y CONTENIDO CFDI
function validarXML(input) {
    const file = input.files[0];
    if (!file) return;

    if (file.size > 2 * 1024 * 1024) {
        mostrarAlertaXMLInvalido('El archivo XML excede el tamaño máximo de 2MB');
        input.value = '';
        return;
    }

    const reader = new FileReader();
    reader.onload = function (e) {
        const parser = new DOMParser();
        const xmlDoc = parser.parseFromString(e.target.result, "text/xml");
        if (xmlDoc.getElementsByTagName("parsererror").length > 0) {
            mostrarAlertaXMLInvalido('El archivo XML no es válido o está mal formado');
            return;
        }
        const resultado = validarEstructuraCFDI(xmlDoc);
        mostrarResultadoValidacionXML(resultado);
    };
    reader.onerror = function () {
        mostrarAlertaXMLInvalido('Error al leer el archivo XML');
    };
    reader.readAsText(file);
}

// VALIDACIÓN DETALLADA DE ESTRUCTURA CFDI 3.3/4.0
function validarEstructuraCFDI(xmlDoc) {
    const resultados = { valido: true, errores: [], datos: {} };

    try {
        const receptor = xmlDoc.getElementsByTagName("cfdi:Receptor")[0] || xmlDoc.getElementsByTagName("Receptor")[0];
        if (receptor) {
            const rfcReceptor = receptor.getAttribute("Rfc") || "";
            const nombreReceptor = receptor.getAttribute("Nombre") || "";
            if (rfcReceptor && rfcReceptor !== "CSO141105V43") {
                resultados.errores.push(`RFC Receptor incorrecto: ${rfcReceptor}. Esperado: CSO141105V43`);
                resultados.valido = false;
            }
            if (nombreReceptor && nombreReceptor !== "CUVITEK SOFTWARE") {
                resultados.errores.push(`Nombre Receptor incorrecto: ${nombreReceptor}. Esperado: CUVITEK SOFTWARE`);
                resultados.valido = false;
            }
            resultados.datos.receptor = { rfc: rfcReceptor, nombre: nombreReceptor };
        } else {
            resultados.errores.push("No se encontró datos del Receptor");
            resultados.valido = false;
        }

        const comprobante = xmlDoc.documentElement;
        const version = comprobante.getAttribute("Version") || "";
        const tipoComprobante = comprobante.getAttribute("TipoDeComprobante") || "";
        const total = comprobante.getAttribute("Total") || "0";
        const fecha = comprobante.getAttribute("Fecha") || new Date().toISOString();

        if (version && version !== "4.0" && version !== "3.3") {
            resultados.errores.push(`Versión CFDI no compatible: ${version}. Requerida: 4.0 o 3.3`);
            resultados.valido = false;
        }

        if (tipoComprobante && tipoComprobante !== "I" && tipoComprobante !== "E") {
            resultados.errores.push(`Tipo de comprobante incorrecto: ${tipoComprobante}. Debe ser Ingreso (I) o Egreso (E)`);
            resultados.valido = false;
        }

        resultados.datos.principal = {
            version: version,
            tipoComprobante: tipoComprobante,
            total: parseFloat(total) || 0,
            fecha: new Date(fecha)
        };

        const emisor = xmlDoc.getElementsByTagName("cfdi:Emisor")[0] || xmlDoc.getElementsByTagName("Emisor")[0];
        if (emisor) {
            resultados.datos.emisor = {
                rfc: emisor.getAttribute("Rfc") || "",
                nombre: emisor.getAttribute("Nombre") || "",
                regimenFiscal: emisor.getAttribute("RegimenFiscal") || ""
            };
        } else {
            resultados.errores.push("No se encontró datos del Emisor");
            resultados.valido = false;
        }

        const timbre = xmlDoc.getElementsByTagName("tfd:TimbreFiscalDigital")[0] ||
            xmlDoc.getElementsByTagName("TimbreFiscalDigital")[0];
        if (timbre) {
            resultados.datos.uuid = timbre.getAttribute("UUID") || "";
            resultados.datos.fechaTimbrado = timbre.getAttribute("FechaTimbrado") || "";
            if (!resultados.datos.uuid) {
                resultados.errores.push("UUID no encontrado en el timbre fiscal");
                resultados.valido = false;
            }
        } else {
            resultados.errores.push("No se encontró Timbre Fiscal Digital");
            resultados.valido = false;
        }

        const conceptos = xmlDoc.getElementsByTagName("cfdi:Concepto") || xmlDoc.getElementsByTagName("Concepto");
        if (conceptos && conceptos.length > 0) {
            resultados.datos.conceptos = [];
            for (let i = 0; i < conceptos.length; i++) {
                const concepto = conceptos[i];
                resultados.datos.conceptos.push({
                    descripcion: concepto.getAttribute("Descripcion") || "",
                    importe: parseFloat(concepto.getAttribute("Importe") || "0"),
                    valorUnitario: parseFloat(concepto.getAttribute("ValorUnitario") || "0"),
                    claveProdServ: concepto.getAttribute("ClaveProdServ") || "",
                    cantidad: concepto.getAttribute("Cantidad") || "1"
                });
            }
        } else {
            resultados.errores.push("No se encontraron conceptos en el CFDI");
            resultados.valido = false;
        }
    } catch (error) {
        resultados.errores.push(`Error al procesar XML: ${error.message}`);
        resultados.valido = false;
    }

    return resultados;
}

// MOSTRAR RESULTADO DE VALIDACIÓN XML EN INTERFAZ
function mostrarResultadoValidacionXML(resultado) {
    const validationResult = document.getElementById('xmlValidationResult');
    if (!validationResult) return;

    if (resultado.valido) {
        validationResult.innerHTML = `
            <div class="alert-comprobacion-success">
                <i class="fas fa-check-circle"></i> <strong>XML VÁLIDO</strong>
                <div class="mt-2 small">
                    <div><strong>Emisor:</strong> ${resultado.datos.emisor.nombre} (${resultado.datos.emisor.rfc})</div>
                    <div><strong>Total:</strong> $${resultado.datos.principal.total.toFixed(2)}</div>
                    <div><strong>Fecha:</strong> ${resultado.datos.principal.fecha.toLocaleDateString()}</div>
                </div>
            </div>
        `;

        autocompletarFormularioDesdeXML(resultado.datos);

        mostrarAlerta('success', 'XML Validado',
            `El archivo XML ha sido validado correctamente.\nEmisor: ${resultado.datos.emisor.nombre}\nTotal: $${resultado.datos.principal.total.toFixed(2)}`);
    } else {
        let erroresHTML = '';
        resultado.errores.forEach(error => erroresHTML += `<li>${error}</li>`);

        validationResult.innerHTML = `
            <div class="alert-comprobacion-danger">
                <i class="fas fa-times-circle"></i> <strong>XML INVÁLIDO</strong>
                <ul class="mt-2 small">${erroresHTML}</ul>
            </div>
        `;

        mostrarAlerta('error', 'Error en XML', 'El archivo XML no pasó la validación. Por favor, sube un archivo XML válido del SAT.');
    }
}

// AUTOCOMPLETAR FORMULARIO CON DATOS DEL XML VALIDADO
function autocompletarFormularioDesdeXML(datosXML) {
    const campos = {
        'Concepto': datosXML.conceptos?.[0]?.descripcion || 'Gasto por servicios',
        'FechaGasto': datosXML.principal.fecha ? new Date(datosXML.principal.fecha).toISOString().split('T')[0] : '',
        'Monto': datosXML.principal.total ? datosXML.principal.total.toFixed(2) : '',
        'Proveedor': datosXML.emisor.nombre || ''
    };

    Object.entries(campos).forEach(([nombre, valor]) => {
        const input = document.querySelector(`input[name="${nombre}"]`);
        if (input && valor) input.value = valor;
    });
}

// ALERTA PARA XML REQUERIDO NO SUBIDO
function mostrarAlertaXMLRequerido() {
    const formGasto = document.getElementById('formGasto');
    if (formGasto) restablecerBotones(formGasto);

    Swal.fire({
        title: 'XML Requerido',
        html: `
            <div class="text-center">
                <div class="mb-3">
                    <i class="fas fa-file-code fa-3x text-warning"></i>
                </div>
                <p>Es <strong>obligatorio</strong> subir el archivo XML de la factura para validar con el SAT.</p>
            </div>
        `,
        icon: 'warning',
        confirmButtonText: 'Entendido',
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' }
    });
}

// ALERTA PARA XML INVÁLIDO
function mostrarAlertaXMLInvalido(mensaje = 'El archivo XML no es válido') {
    const formGasto = document.getElementById('formGasto');
    if (formGasto) restablecerBotones(formGasto);

    Swal.fire({
        title: 'XML Inválido',
        text: mensaje,
        icon: 'error',
        confirmButtonText: 'Corregir',
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' }
    });
}

// CONFIRMACIÓN PARA ELIMINAR GASTO ESPECÍFICO
function mostrarConfirmacionEliminarGasto(gastoId, concepto, monto) {
    Swal.fire({
        title: '¿Eliminar Gasto?',
        html: `
            <div class="text-center">
                <div class="mb-3">
                    <i class="fas fa-trash-alt fa-3x text-danger"></i>
                </div>
                <p>¿Estás seguro de eliminar el siguiente gasto?</p>
                <div class="alert-comprobacion-info" style="text-align: left; padding: 1rem; margin: 1rem 0;">
                    <strong>Concepto:</strong> ${concepto}<br>
                    <strong>Monto:</strong> <span class="text-success">${monto}</span>
                </div>
            </div>
        `,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#e74c3c',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Sí, eliminar',
        cancelButtonText: 'Cancelar',
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' }
    }).then((result) => {
        if (result.isConfirmed) {
            eliminarGasto(gastoId);
        }
    });
}

// ELIMINACIÓN DE GASTO VÍA FETCH API
function eliminarGasto(gastoId) {
    Swal.fire({
        title: 'Eliminando gasto...',
        allowEscapeKey: false,
        allowOutsideClick: false,
        showConfirmButton: false,
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' },
        didOpen: () => Swal.showLoading()
    });

    const formData = new FormData();
    formData.append('id', gastoId);

    fetch('/Comprobaciones/EliminarGasto', {
        method: 'POST',
        body: formData,
        headers: {
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        }
    })
        .then(response => {
            if (!response.ok) throw new Error('Error en la respuesta del servidor');
            return response.json();
        })
        .then(data => {
            Swal.close();
            if (data.success) {
                const fila = document.getElementById(`gasto-${gastoId}`);
                if (fila) {
                    fila.remove();
                    actualizarTotalComprobacion();
                    mostrarAlerta('success', '¡Eliminado!', 'El gasto ha sido eliminado correctamente', true);
                }
            } else {
                throw new Error(data.message || 'Error al eliminar el gasto');
            }
        })
        .catch(error => {
            mostrarAlerta('error', 'Error', 'No se pudo eliminar el gasto: ' + error.message);
        });
}

// CONFIRMACIÓN PARA ENVÍO A FINANZAS
function mostrarConfirmacionEnviarFinanzas(estadoGastos) {
    const totalComprobado = document.getElementById('totalComprobadoHeader')?.textContent || '$0.00';

    Swal.fire({
        title: '¿Enviar Comprobación a Finanzas?',
        html: `
            <div class="text-center">
                <div class="mb-3">
                    <i class="fas fa-paper-plane fa-3x" style="color: var(--viamtek-green);"></i>
                </div>
                <p>¿Estás seguro de enviar la comprobación completa a Finanzas?</p>
                <div class="alert-comprobacion-info" style="text-align: left; padding: 1rem; margin: 1rem 0;">
                    <strong>Resumen Final:</strong><br>
                    • Total de gastos: <strong>${estadoGastos.total}</strong><br>
                    • Con archivos adjuntos: <strong style="color: ${estadoGastos.todosTienenXML ? 'var(--viamtek-green)' : '#e74c3c'};">${estadoGastos.conXML}</strong><br>
                    • Total comprobado: <strong style="color: var(--viamtek-green);">${totalComprobado}</strong>
                </div>
            </div>
        `,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: 'var(--viamtek-green)',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Sí, enviar a Finanzas',
        cancelButtonText: 'Cancelar',
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' }
    }).then((result) => {
        if (result.isConfirmed) {
            enviarFinanzas();
        }
    });
}

// PROCESO DE ENVÍO DE COMPROBACIÓN A FINANZAS
function enviarFinanzas() {
    Swal.fire({
        title: 'Enviando a Finanzas...',
        html: `
            <div class="text-center">
                <div class="mb-3">
                    <i class="fas fa-spinner fa-spin fa-2x" style="color: var(--viamtek-green);"></i>
                </div>
                <p>Procesando tu comprobación de gastos</p>
            </div>
        `,
        allowEscapeKey: false,
        allowOutsideClick: false,
        showConfirmButton: false,
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' }
    });

    setTimeout(() => {
        const formEnviarFinanzas = document.getElementById('formEnviarFinanzas');
        if (formEnviarFinanzas) {
            formEnviarFinanzas.submit();
        } else {
            Swal.close();
            mostrarAlerta('error', 'Error', 'No se pudo encontrar el formulario para enviar a finanzas');
        }
    }, 1500);
}

// RESTABLECIMIENTO DE BOTONES DESPUÉS DE ERROR
function restablecerBotones(formulario) {
    const botones = formulario.querySelectorAll('button[type="submit"]');
    botones.forEach(boton => {
        boton.disabled = false;
        boton.innerHTML = '<i class="fas fa-save me-1"></i> Guardar Gasto';
    });
}

// VERIFICACIÓN DE ARCHIVOS ADJUNTOS EN GASTOS
function verificarGastosConXML() {
    const filasGastos = document.querySelectorAll('#tablaGastos tbody tr');
    let gastosConXML = 0;
    let totalGastos = filasGastos.length;

    filasGastos.forEach(fila => {
        const tieneArchivos = fila.querySelector('a[href*=".xml"], a[href*=".pdf"]');
        if (tieneArchivos) gastosConXML++;
    });

    return {
        total: totalGastos,
        conXML: gastosConXML,
        todosTienenXML: totalGastos > 0 && gastosConXML === totalGastos
    };
}

// MOSTRAR MENSAJES DE ALERTA GENÉRICOS
function mostrarMensaje(tipo, mensaje) {
    if (typeof Swal !== 'undefined') {
        const config = {
            title: tipo === 'success' ? '¡Éxito!' : 'Error',
            text: mensaje,
            icon: tipo,
            confirmButtonText: 'Aceptar',
            background: 'var(--card-bg)',
            color: 'var(--primary-text)',
            customClass: { popup: 'sweetalert-dark' }
        };

        if (tipo === 'success') {
            config.timer = 3000;
            config.showConfirmButton = false;
        }

        Swal.fire(config);
    } else {
        alert(mensaje);
    }
}

// VERIFICACIÓN DE FECHA LÍMITE DE COMPROBACIÓN
function verificarFechaLimite() {
    try {
        const fechaLimite = new Date('@Model.FechaLimiteComprobacion.ToString("yyyy-MM-dd")');
        const hoy = new Date();
        const diasRestantes = Math.ceil((fechaLimite - hoy) / (1000 * 60 * 60 * 24));
        if (diasRestantes <= 3 && diasRestantes > 0) {
            mostrarAlertaFechaLimite(diasRestantes);
        }
    } catch (error) {
        // Silenciar error si no hay fecha límite
    }
}

// ALERTA DE FECHA LÍMITE PRÓXIMA
function mostrarAlertaFechaLimite(dias) {
    const alerta = document.createElement('div');
    alerta.className = 'alert alert-warning alert-dismissible fade show';
    alerta.innerHTML = `
        <i class="fas fa-exclamation-triangle me-2"></i>
        <strong>¡Atención!</strong> Te quedan <strong>${dias} días</strong> para completar la comprobación de gastos.
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    const container = document.querySelector('.comprobacion-container');
    if (container) container.insertBefore(alerta, container.firstChild);
}

// INICIALIZACIÓN DE ALERTAS DE GUARDADO DESDE SESSIONSTORAGE
function inicializarAlertasGuardado() {
    mostrarAlertaGuardado('gasto');
    mostrarAlertaGuardado('informe');
}

// MOSTRAR ALERTAS DE GUARDADO EXITOSO
function mostrarAlertaGuardado(tipo) {
    const alertas = {
        'gasto': { flag: 'mostrarAlertaGastoGuardado' },
        'informe': { flag: 'mostrarAlertaInformeGuardado' }
    };
    const config = alertas[tipo];
    if (!config) return;

    const shouldShowAlert = sessionStorage.getItem(config.flag);
    if (shouldShowAlert === 'true') {
        mostrarAlerta('success', `${tipo === 'gasto' ? 'Gasto' : 'Informe'} Guardado`,
            `El ${tipo} se ha guardado correctamente`, true);
        sessionStorage.removeItem(config.flag);
    }
}

// FUNCIÓN GENÉRICA PARA MOSTRAR ALERTAS SWEETALERT
function mostrarAlerta(icono, titulo, mensaje, toast = false) {
    const config = {
        title: titulo,
        text: mensaje,
        icon: icono,
        background: 'var(--card-bg)',
        color: 'var(--primary-text)',
        customClass: { popup: 'sweetalert-dark' }
    };

    if (toast) {
        config.timer = 3000;
        config.showConfirmButton = false;
        config.toast = true;
        config.position = 'top-end';
    } else {
        config.confirmButtonText = 'Aceptar';
    }

    Swal.fire(config);
}

// ACTUALIZACIÓN DEL TOTAL COMPROBADO EN TIEMPO REAL
function actualizarTotalComprobacion() {
    let total = 0;

    if ($.fn.DataTable.isDataTable('#tablaGastos')) {
        const table = $('#tablaGastos').DataTable();
        table.rows().every(function () {
            const montoTexto = this.cell(3).data();
            if (montoTexto) {
                const monto = parseFloat(montoTexto.replace(/[^0-9.-]+/g, ""));
                if (!isNaN(monto)) {
                    total += monto;
                }
            }
        });
    } else {
        document.querySelectorAll('#tablaGastos tbody tr').forEach(fila => {
            const montoTexto = fila.cells[3]?.textContent;
            if (montoTexto) {
                const monto = parseFloat(montoTexto.replace(/[^0-9.-]+/g, ""));
                if (!isNaN(monto)) {
                    total += monto;
                }
            }
        });
    }

    const elementosTotal = document.querySelectorAll('#totalComprobado, #totalComprobadoHeader');
    elementosTotal.forEach(elemento => {
        if (elemento) elemento.textContent = formatearMoneda(total);
    });
}

// FORMATEO DE MONEDA EN PESOS MEXICANOS
function formatearMoneda(monto) {
    return new Intl.NumberFormat('es-MX', {
        style: 'currency',
        currency: 'MXN'
    }).format(monto);
}

// REINICIALIZACIÓN DE DATATABLES
function reinicializarDataTables() {
    const tablaGastos = $('#tablaGastos');

    if (!tablaGastos.length) return;

    if ($.fn.DataTable.isDataTable('#tablaGastos')) {
        try {
            tablaGastos.DataTable().destroy();
            tablaGastos.empty();
        } catch (error) {
            console.warn('Error al destruir DataTable existente:', error);
        }
    }

    setTimeout(() => {
        inicializarDataTablesComprobaciones();
    }, 100);
}

// EXPORTACIÓN DE FUNCIONES AL ÁMBITO GLOBAL PARA REUTILIZACIÓN
window.mostrarConfirmacionEliminarGasto = mostrarConfirmacionEliminarGasto;
window.mostrarConfirmacionEnviarFinanzas = mostrarConfirmacionEnviarFinanzas;
window.eliminarGasto = eliminarGasto;
window.actualizarTotalComprobacion = actualizarTotalComprobacion;
window.validarXML = validarXML;
window.autocompletarFormularioDesdeXML = autocompletarFormularioDesdeXML;
window.mostrarAlertaGuardado = mostrarAlertaGuardado;
window.inicializarAlertasGuardado = inicializarAlertasGuardado;
window.inicializarDataTablesComprobaciones = inicializarDataTablesComprobaciones;