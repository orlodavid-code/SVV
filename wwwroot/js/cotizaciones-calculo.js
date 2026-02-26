// cotizaciones-calculo.js - Sistema de Cálculo Automático de Cotizaciones
// Responsable: Cálculo automático de costos de viaje basado en rutas y parámetros

class CalculoAutomaticoManager {
    constructor() {
        this.apiBase = '/api/CotizacionesApi';
        this.isCalculating = false;
        this.solicitudId = document.getElementById('SolicitudViajeId')?.value;
        this.init();
    }

    // INICIALIZACIÓN PRINCIPAL DEL MANAGER
    init() {
        this.configurarEventos();
        this.verificarCalculoPrevioso();
    }

    // CONFIGURACIÓN DE EVENTOS DE INTERFAZ
    configurarEventos() {
        const btnCalcularAuto = document.getElementById('btnCalcularAuto');
        if (btnCalcularAuto) {
            btnCalcularAuto.addEventListener('click', () => this.calcularAutomatico());
        }

        const switchCalculo = document.getElementById('usarCalculoAutomatico');
        if (switchCalculo) {
            switchCalculo.addEventListener('change', (e) => {
                const usarCalculo = e.target.checked;
                this.toggleCalculoAutomatico(usarCalculo);
                document.getElementById('UsarCalculoAutomatico').value = usarCalculo;
            });
        }

        const btnLimpiar = document.getElementById('btnLimpiarCalculo');
        if (btnLimpiar) {
            btnLimpiar.addEventListener('click', () => this.limpiarCalculo());
        }
    }

    // CÁLCULO AUTOMÁTICO PRINCIPAL - LLAMADA A API
    async calcularAutomatico() {
        if (!this.solicitudId || this.isCalculating) return;

        this.isCalculating = true;
        this.mostrarEstadoCarga(true);

        try {
            const url = `${this.apiBase}/calcular-auto/${this.solicitudId}`;

            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                }
            });

            if (!response.ok) {
                throw new Error(`Error HTTP: ${response.status}`);
            }

            const respuesta = await response.json();

            if (respuesta.success) {
                await this.aplicarResultadoCalculo(respuesta);
                this.mostrarMensaje('Cálculo automático completado', 'success');
            } else {
                this.mostrarErrores(respuesta.errors || [respuesta.message]);
                this.mostrarMensaje('Error en cálculo automático', 'error');
            }
        } catch (error) {
            this.mostrarMensaje(`Error: ${error.message}`, 'error');
        } finally {
            this.isCalculating = false;
            this.mostrarEstadoCarga(false);
        }
    }

    // APLICACIÓN DE RESULTADOS DEL CÁLCULO A LA INTERFAZ
    async aplicarResultadoCalculo(resultado) {
        if (!resultado || !resultado.data) return;

        const data = resultado.data;
        this.mostrarResultadosCalculo(data);
        await this.aplicarPreciosPorPersona(data);
        this.recalcularTotales();
    }

    // DECODIFICACIÓN DE TEXTO HTML PARA MANEJO DE CARACTERES ESPECIALES
    decodificarHTML(texto) {
        if (!texto) return '';

        const txt = document.createElement("textarea");
        txt.innerHTML = texto;
        return txt.value;
    }

    // NORMALIZACIÓN DE TEXTO PARA COMPARACIONES (QUITA ACENTOS)
    normalizarTexto(texto) {
        if (!texto) return '';

        return texto.toLowerCase()
            .normalize("NFD").replace(/[\u0300-\u036f]/g, "")
            .replace(/[^a-z0-9 ]/g, '')
            .trim();
    }

    // DETECCIÓN DE MEDIO DE TRANSPORTE COMO VEHÍCULO
    esVehiculo(medioNormalizado) {
        const vehiculos = [
            'vehiculo utilitario',
            'vehiculo personal',
            'vehiculo',
            'vehiculo propio',
            'carro particular',
            'vehiculo particular',
            'utilitario'
        ];
        const palabras = medioNormalizado.split(' ');
        return palabras.some(palabra => vehiculos.includes(palabra));
    }

    // APLICACIÓN DE PRECIOS POR CONCEPTO Y PERSONA
    async aplicarPreciosPorPersona(resultado) {
        const numeroPersonas = parseInt(document.getElementById('NumeroPersonas')?.value) || 1;

        const medioTrasladoElement = document.getElementById('MedioTraslado');
        let medioTraslado = '';
        if (medioTrasladoElement) {
            medioTraslado = this.decodificarHTML(medioTrasladoElement.value);
        }

        const medioNormalizado = this.normalizarTexto(medioTraslado);
        const esVehiculo = this.esVehiculo(medioNormalizado);

        const totalTransporte = resultado.TotalTransporte || 0;
        const totalGasolina = resultado.TotalGasolina || 0;
        const totalUberTaxi = resultado.TotalUberTaxi || 0;
        const totalCasetas = resultado.TotalCasetas || 0;
        const totalHospedaje = resultado.TotalHospedaje || 0;
        const totalAlimentos = resultado.TotalAlimentos || 0;

        // TRANSPORTE - APLICACIÓN CONDICIONAL SI ES VEHÍCULO
        if (resultado.DetalleTransporte && resultado.DetalleTransporte.length > 0) {
            await this.aplicarDetalleConcepto('transporte', resultado.DetalleTransporte);
        } else if (totalTransporte > 0) {
            if (!esVehiculo) {
                await this.aplicarPreciosConcepto('transporte', totalTransporte, numeroPersonas, 'Transporte');
            }
        }

        // GASOLINA - APLICACIÓN INDIVIDUAL O DETALLADA
        if (resultado.DetalleGasolina && resultado.DetalleGasolina.length > 0) {
            await this.aplicarDetalleConcepto('gasolina', resultado.DetalleGasolina);
        } else if (totalGasolina > 0) {
            await this.aplicarPrecioIndividual('gasolina', totalGasolina, 'Gasolina');
        }

        // UBER/TAXI - APLICACIÓN POR PERSONA
        if (resultado.DetalleUberTaxi && resultado.DetalleUberTaxi.length > 0) {
            await this.aplicarDetalleConcepto('ubertaxi', resultado.DetalleUberTaxi);
        } else if (totalUberTaxi > 0) {
            await this.aplicarPreciosConcepto('ubertaxi', totalUberTaxi, numeroPersonas, 'UBER/TAXI');
        }

        // CASETAS - APLICACIÓN INDIVIDUAL O DETALLADA
        if (resultado.DetalleCasetas && resultado.DetalleCasetas.length > 0) {
            await this.aplicarDetalleConcepto('casetas', resultado.DetalleCasetas);
        } else if (totalCasetas > 0) {
            await this.aplicarPrecioIndividual('casetas', totalCasetas, 'Casetas');
        }

        // HOSPEDAJE - APLICACIÓN POR PERSONA
        if (resultado.DetalleHospedaje && resultado.DetalleHospedaje.length > 0) {
            await this.aplicarDetalleConcepto('hospedaje', resultado.DetalleHospedaje);
        } else if (totalHospedaje > 0) {
            await this.aplicarPreciosConcepto('hospedaje', totalHospedaje, numeroPersonas, 'Hospedaje');
        }

        // ALIMENTOS - APLICACIÓN ESPECÍFICA CON DETALLE POR PERSONA
        if (totalAlimentos > 0) {
            if (resultado.DetalleAlimentos && resultado.DetalleAlimentos.length > 0) {
                await this.aplicarDetalleConcepto('alimentos', resultado.DetalleAlimentos);
            } else {
                const precioPorPersona = totalAlimentos / numeroPersonas;
                const detalles = [];

                for (let i = 0; i < numeroPersonas; i++) {
                    detalles.push({
                        Precio: precioPorPersona,
                        Descripcion: `Alimentos Persona ${i + 1}`
                    });
                }

                await this.aplicarDetalleConcepto('alimentos', detalles);
            }
        }

        // ACTUALIZACIÓN DE DISTANCIA CALCULADA
        if (resultado.DistanciaCalculada) {
            this.actualizarDistancia(resultado.DistanciaCalculada);
        }
    }

    // APLICACIÓN DE DETALLE DE CONCEPTO CON MÚLTIPLES PRECIOS
    async aplicarDetalleConcepto(tipo, detalles) {
        const cuerpoId = `${tipo}-body`;
        const tbody = document.getElementById(cuerpoId);
        const cantidadInput = document.querySelector(`.cantidad-input[data-concepto="${tipo}"]`);

        if (!tbody || !cantidadInput) return;

        tbody.innerHTML = '';

        let index = 1;
        for (const detalle of detalles) {
            const nuevaFila = this.crearFilaPrecio(tipo, index, detalle.Precio, detalle.Descripcion || '');
            tbody.appendChild(nuevaFila);
            index++;
        }

        cantidadInput.value = detalles.length;

        const campoCantidadHidden = document.getElementById(`${this.capitalizeFirst(tipo)}Cantidad`);
        if (campoCantidadHidden) {
            campoCantidadHidden.value = detalles.length;
        }

        const subtotal = detalles.reduce((sum, detalle) => sum + (detalle.Precio || 0), 0);
        this.actualizarSubtotal(tipo, subtotal);
        this.recalcularTotales();
    }

    // APLICACIÓN DE PRECIO INDIVIDUAL PARA CONCEPTOS ÚNICOS
    async aplicarPrecioIndividual(tipo, precio, descripcion) {
        const cuerpoId = `${tipo}-body`;
        const tbody = document.getElementById(cuerpoId);
        const cantidadInput = document.querySelector(`.cantidad-input[data-concepto="${tipo}"]`);

        if (!tbody || !cantidadInput) return;

        tbody.innerHTML = '';
        cantidadInput.value = 1;

        const campoCantidadHidden = document.getElementById(`${this.capitalizeFirst(tipo)}Cantidad`);
        if (campoCantidadHidden) {
            campoCantidadHidden.value = 1;
        }

        const nuevaFila = this.crearFilaPrecio(tipo, 1, precio, descripcion);
        tbody.appendChild(nuevaFila);

        this.actualizarSubtotal(tipo, precio);
        this.recalcularTotales();
    }

    // APLICACIÓN DE PRECIOS POR PERSONA PARA CONCEPTOS MULTIPLES
    async aplicarPreciosConcepto(tipo, precioTotal, numeroPersonas, nombreConcepto) {
        const cuerpoId = `${tipo}-body`;
        const tbody = document.getElementById(cuerpoId);
        const cantidadInput = document.querySelector(`.cantidad-input[data-concepto="${tipo}"]`);

        if (!tbody || !cantidadInput) return;

        tbody.innerHTML = '';

        const precioPorPersona = precioTotal / numeroPersonas;

        for (let i = 0; i < numeroPersonas; i++) {
            const nuevaFila = this.crearFilaPrecio(
                tipo,
                i + 1,
                precioPorPersona,
                `${nombreConcepto} - Persona ${i + 1}`
            );
            tbody.appendChild(nuevaFila);
        }

        cantidadInput.value = numeroPersonas;

        const campoCantidadHidden = document.getElementById(`${this.capitalizeFirst(tipo)}Cantidad`);
        if (campoCantidadHidden) {
            campoCantidadHidden.value = numeroPersonas;
        }

        this.actualizarSubtotal(tipo, precioTotal);
        this.recalcularTotales();
    }

    // CREACIÓN DE FILA DE PRECIO EN TABLA
    crearFilaPrecio(concepto, numero, precio, descripcion) {
        const tr = document.createElement('tr');
        tr.className = 'fila-precio';

        const tbody = document.getElementById(`${concepto}-body`);
        const indice = tbody ? tbody.querySelectorAll('tr.fila-precio').length : numero - 1;
        const nombrePropiedad = this.capitalizeFirst(concepto) + 'Precios';

        tr.innerHTML = `
        <td class="numero-fila">${numero}</td>
        <td>
            <div class="input-group input-group-sm">
                <span class="input-group-text">$</span>
                <input class="form-control precio-input"
                       type="number"
                       step="0.01"
                       min="0"
                       value="${precio.toFixed(2)}"
                       data-concepto="${concepto}"
                       name="${nombrePropiedad}[${indice}].Precio"
                       oninput="recalcularTotales()" />
            </div>
        </td>
        <td>
            <input class="form-control form-control-sm descripcion-input"
                   placeholder="Descripción"
                   type="text"
                   name="${nombrePropiedad}[${indice}].Descripcion"
                   value="${descripcion}" />
        </td>
        <td>
            <button type="button" class="btn btn-danger btn-sm btn-eliminar-precio"
                    onclick="eliminarFila(this)">
                <i class="fas fa-trash"></i>
            </button>
        </td>
    `;

        return tr;
    }

    // ACTUALIZACIÓN DE SUBTOTAL POR CONCEPTO
    actualizarSubtotal(concepto, total) {
        const subtotalElement = document.querySelector(`.subtotal[data-concepto="${concepto}"]`);
        if (subtotalElement) {
            subtotalElement.textContent = total.toFixed(2);
        }

        const campoTotal = document.getElementById(`Total${this.capitalizeFirst(concepto)}`);
        if (campoTotal) {
            campoTotal.value = total;
        }
    }

    // ACTUALIZACIÓN DE DISTANCIA CALCULADA EN INTERFAZ
    actualizarDistancia(distancia) {
        const distanciaSpan = document.getElementById('distanciaCalculada');
        if (distanciaSpan) {
            const distanciaNum = parseFloat(distancia) || 0;
            distanciaSpan.innerHTML = `<strong>${distanciaNum.toFixed(0)} km</strong> (${distanciaNum.toFixed(2)} km)`;

            const campoDistancia = document.getElementById('DistanciaCalculada');
            if (campoDistancia) {
                campoDistancia.value = distancia;
            }
        }
    }

    // VISUALIZACIÓN DE RESULTADOS DE CÁLCULO
    mostrarResultadosCalculo(data) {
        const resultadosDiv = document.getElementById('calculosResults');
        const alertasDiv = document.getElementById('alertasCalculo');
        const erroresDiv = document.getElementById('erroresCalculo');

        if (alertasDiv && data.Alertas && data.Alertas.length > 0) {
            alertasDiv.innerHTML = '';
            data.Alertas.forEach(alerta => {
                const alertaDiv = document.createElement('div');
                alertaDiv.className = 'alerta-item';
                alertaDiv.innerHTML = `<i class="fas fa-exclamation-triangle"></i> ${alerta}`;
                alertasDiv.appendChild(alertaDiv);
            });
        }

        if (erroresDiv && data.Errores && data.Errores.length > 0) {
            erroresDiv.innerHTML = '';
            data.Errores.forEach(error => {
                const errorDiv = document.createElement('div');
                errorDiv.className = 'error-item';
                errorDiv.innerHTML = `<i class="fas fa-times-circle"></i> ${error}`;
                erroresDiv.appendChild(errorDiv);
            });
        }

        if (data.DistanciaCalculada) {
            this.actualizarDistancia(data.DistanciaCalculada);
        }

        if (resultadosDiv) {
            resultadosDiv.style.display = 'block';
        }
    }

    // ACTIVACIÓN/DESACTIVACIÓN DE CÁLCULO AUTOMÁTICO
    toggleCalculoAutomatico(activado) {
        const btnCalcularAuto = document.getElementById('btnCalcularAuto');
        if (btnCalcularAuto) {
            btnCalcularAuto.disabled = !activado;
        }

        if (!activado) {
            this.limpiarCalculo();
        }
    }

    // LIMPIEZA DE RESULTADOS DE CÁLCULO
    limpiarCalculo() {
        const resultadosDiv = document.getElementById('calculosResults');
        if (resultadosDiv) {
            resultadosDiv.style.display = 'none';
        }

        const alertasDiv = document.getElementById('alertasCalculo');
        if (alertasDiv) {
            alertasDiv.innerHTML = '';
        }

        const erroresDiv = document.getElementById('erroresCalculo');
        if (erroresDiv) {
            erroresDiv.innerHTML = '';
        }

        const distanciaSpan = document.getElementById('distanciaCalculada');
        if (distanciaSpan) {
            distanciaSpan.textContent = 'Distancia no calculada';
        }

        const campoDistancia = document.getElementById('DistanciaCalculada');
        if (campoDistancia) {
            campoDistancia.value = '';
        }

        this.resetearPrecios();
        this.mostrarMensaje('Cálculo automático limpiado', 'info');
    }

    // RESETEO DE PRECIOS A VALORES POR DEFECTO
    resetearPrecios() {
        const conceptos = ['transporte', 'gasolina', 'ubertaxi', 'casetas', 'hospedaje', 'alimentos'];

        conceptos.forEach(concepto => {
            const cuerpoId = `${concepto}-body`;
            const tbody = document.getElementById(cuerpoId);
            const cantidadInput = document.querySelector(`.cantidad-input[data-concepto="${concepto}"]`);

            if (!tbody || !cantidadInput) return;

            cantidadInput.value = 1;
            tbody.innerHTML = '';

            const nuevaFila = this.crearFilaPrecio(concepto, 1, 0, '');
            tbody.appendChild(nuevaFila);

            this.actualizarSubtotal(concepto, 0);
        });

        this.recalcularTotales();
    }

    // VERIFICACIÓN DE CÁLCULO PREVIO AL CARGAR PÁGINA
    verificarCalculoPrevioso() {
        const distanciaCalculada = document.getElementById('DistanciaCalculada')?.value;
        const calculoRealizado = document.getElementById('CalculoRealizado')?.value;

        if (distanciaCalculada) {
            this.actualizarDistancia(distanciaCalculada);

            const resultadosDiv = document.getElementById('calculosResults');
            if (resultadosDiv) {
                resultadosDiv.style.display = 'block';
            }
        }
    }

    // VISUALIZACIÓN DE ESTADO DE CARGA DURANTE CÁLCULO
    mostrarEstadoCarga(mostrar) {
        const btnCalcularAuto = document.getElementById('btnCalcularAuto');
        if (btnCalcularAuto) {
            if (mostrar) {
                btnCalcularAuto.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Calculando...';
                btnCalcularAuto.disabled = true;
            } else {
                btnCalcularAuto.innerHTML = '<i class="fas fa-calculator me-1"></i> Calcular Automáticamente';
                btnCalcularAuto.disabled = !document.getElementById('usarCalculoAutomatico')?.checked;
            }
        }
    }

    // VISUALIZACIÓN DE ERRORES DE CÁLCULO
    mostrarErrores(errores) {
        if (!errores || errores.length === 0) return;

        const erroresDiv = document.getElementById('erroresCalculo');
        if (!erroresDiv) return;

        erroresDiv.innerHTML = '';
        errores.forEach(error => {
            const errorDiv = document.createElement('div');
            errorDiv.className = 'error-item';
            errorDiv.innerHTML = `<i class="fas fa-times-circle"></i> ${error}`;
            erroresDiv.appendChild(errorDiv);
        });

        const resultadosDiv = document.getElementById('calculosResults');
        if (resultadosDiv) {
            resultadosDiv.style.display = 'block';
        }
    }

    // MOSTRAR MENSAJES DE ESTADO AL USUARIO
    mostrarMensaje(mensaje, tipo = 'info') {
        const toast = document.getElementById('apiStatus');
        const mensajeElement = document.getElementById('apiStatusMessage');

        if (toast && mensajeElement) {
            mensajeElement.textContent = mensaje;
            toast.style.display = 'block';

            setTimeout(() => {
                toast.style.display = 'none';
            }, 5000);
        }
    }

    // RECÁLCULO DE TOTALES GENERALES
    recalcularTotales() {
        const conceptos = ['transporte', 'gasolina', 'ubertaxi', 'casetas', 'hospedaje', 'alimentos'];
        let totalGeneral = 0;

        conceptos.forEach(concepto => {
            const inputs = document.querySelectorAll(`.precio-input[data-concepto="${concepto}"]`);
            let subtotal = 0;

            inputs.forEach(input => {
                subtotal += parseFloat(input.value) || 0;
            });

            const subtotalElement = document.querySelector(`.subtotal[data-concepto="${concepto}"]`);
            if (subtotalElement) {
                subtotalElement.textContent = subtotal.toFixed(2);
            }

            const totalHidden = document.getElementById(`Total${this.capitalizeFirst(concepto)}`);
            if (totalHidden) {
                totalHidden.value = subtotal;
            }

            totalGeneral += subtotal;
        });

        const totalDisplay = document.getElementById('total-display');
        if (totalDisplay) {
            totalDisplay.textContent = '$' + totalGeneral.toFixed(2);
        }

        const totalGeneralInput = document.getElementById('TotalGeneral');
        if (totalGeneralInput) {
            totalGeneralInput.value = totalGeneral;
        }
    }

    // CAPITALIZACIÓN DE PRIMER LETRA PARA NOMBRES DE PROPIEDADES
    capitalizeFirst(string) {
        return string.charAt(0).toUpperCase() + string.slice(1);
    }
}

// VARIABLE GLOBAL PARA INSTANCIA DEL MANAGER
let calculoManager = null;

// INICIALIZACIÓN GLOBAL DEL SISTEMA
document.addEventListener('DOMContentLoaded', function () {
    try {
        calculoManager = new CalculoAutomaticoManager();
        window.calculoManager = calculoManager;

        // FUNCIÓN GLOBAL PARA RECALCULAR TOTALES
        window.recalcularTotales = function () {
            if (calculoManager) {
                calculoManager.recalcularTotales();
            }
        };

        // FUNCIÓN GLOBAL PARA ELIMINAR FILAS DE PRECIO
        window.eliminarFila = function (boton) {
            const fila = boton.closest('tr.fila-precio');
            const precioInput = fila.querySelector('.precio-input');
            if (!precioInput) return;

            const concepto = precioInput.dataset.concepto;
            const cantidadInput = document.querySelector(`.cantidad-input[data-concepto="${concepto}"]`);

            if (cantidadInput && parseInt(cantidadInput.value) > 1) {
                fila.remove();
                const nuevaCantidad = parseInt(cantidadInput.value) - 1;
                cantidadInput.value = nuevaCantidad;

                if (calculoManager) {
                    calculoManager.actualizarCampoCantidadHidden(concepto, nuevaCantidad);
                }

                const tbody = document.getElementById(`${concepto}-body`);
                const filas = tbody.querySelectorAll('tr.fila-precio');
                filas.forEach((fila, index) => {
                    fila.querySelector('.numero-fila').textContent = index + 1;
                });

                recalcularTotales();
            }
        };

        // FUNCIÓN GLOBAL PARA INVOCAR CÁLCULO AUTOMÁTICO
        window.calcularAutomatico = function () {
            if (calculoManager) {
                calculoManager.calcularAutomatico();
            }
        };

        // FUNCIÓN GLOBAL PARA LIMPIAR CÁLCULOS
        window.limpiarCalculoAutomatico = function () {
            if (calculoManager) {
                calculoManager.limpiarCalculo();
            }
        };

        const switchCalculo = document.getElementById('usarCalculoAutomatico');
        const btnCalcularAuto = document.getElementById('btnCalcularAuto');

        if (switchCalculo && btnCalcularAuto) {
            btnCalcularAuto.disabled = !switchCalculo.checked;
        }

    } catch (error) {
        console.error('Error al inicializar cálculo automático:', error);
    }
});