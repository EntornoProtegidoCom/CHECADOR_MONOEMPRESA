(function () {
    'use strict';

    var raiz = document.getElementById('estaciones');
    if (!raiz) { return; }

    var url = raiz.getAttribute('data-url');
    var intervalo = parseInt(raiz.getAttribute('data-intervalo'), 10) || 30000;
    var cuerpo = document.getElementById('estacionesCuerpo');
    var errorEl = document.getElementById('estacionesError');
    var cargandoEl = document.getElementById('estacionesCargando');
    var actualizadoEl = document.getElementById('estacionesActualizado');
    var vacioEl = document.getElementById('estacionesVacio');
    var tablaEl = document.getElementById('estacionesTablaContenedor');
    var ultimaCorrecta = actualizadoEl ? actualizadoEl.textContent : '';
    var enCurso = false;

    function celda(fila, etiqueta, contenido) {
        var td = document.createElement('td');
        td.setAttribute('data-label', etiqueta);
        if (contenido instanceof Node) { td.appendChild(contenido); } else { td.textContent = contenido == null ? '—' : String(contenido); }
        fila.appendChild(td);
        return td;
    }

    function renderizar(datos) {
        var frag = document.createDocumentFragment();
        datos.filas.forEach(function (f) {
            var tr = document.createElement('tr');
            var fuerte = document.createElement('strong');
            fuerte.textContent = f.equipo;
            celda(tr, 'Equipo', fuerte);

            var badge = document.createElement('span');
            badge.className = 'badge badge-estado bg-' + f.clase;
            badge.textContent = f.etiqueta;
            celda(tr, 'Comunicación', badge);

            var ultimo = f.ultimo ? f.ultimo + ' (hace ' + f.minutos + ' min)' : '—';
            celda(tr, 'Último heartbeat', ultimo);
            celda(tr, 'Estado informado', f.informado);
            celda(tr, 'Empleado', [f.trabajador, f.nombre].filter(function (x) { return x != null && x !== ''; }).join(' ') || '—');
            celda(tr, 'IP / versión', (f.ip || '—') + ' / ' + (f.version || '—')).className = 'small';
            celda(tr, 'Observación', f.observacion || '').className = 'small';
            frag.appendChild(tr);
        });
        cuerpo.replaceChildren(frag);

        Object.keys(datos.resumen).forEach(function (k) {
            var el = raiz.querySelector('[data-resumen="' + k + '"]');
            if (el) { el.textContent = datos.resumen[k]; }
        });

        var hayFilas = datos.filas.length > 0;
        vacioEl.classList.toggle('d-none', hayFilas);
        tablaEl.classList.toggle('d-none', !hayFilas);
        actualizadoEl.textContent = datos.ahora;
        ultimaCorrecta = datos.ahora;
    }

    function mostrarError(mensaje) {
        // Se conservan los últimos datos: un fallo de consulta no implica que las estaciones estén desconectadas.
        errorEl.textContent = mensaje + ' Se muestran los datos de ' + ultimaCorrecta +
            ', que pueden estar desactualizados. Se reintentará automáticamente.';
        errorEl.classList.remove('d-none');
    }

    function actualizar() {
        if (enCurso || document.hidden) { return; }
        enCurso = true;
        cargandoEl.classList.remove('d-none');
        fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin', redirect: 'error' })
            .then(function (r) {
                return r.json().catch(function () { return {}; }).then(function (cuerpoJson) {
                    if (!r.ok) { throw new Error(cuerpoJson.error || 'No fue posible actualizar.'); }
                    return cuerpoJson;
                });
            })
            .then(function (datos) {
                renderizar(datos);
                errorEl.classList.add('d-none');
            })
            .catch(function (e) {
                mostrarError(e && e.message && e.message.indexOf('No fue posible') === 0 ? e.message : 'No fue posible actualizar (sesión expirada o sin conexión).');
            })
            .finally(function () {
                enCurso = false;
                cargandoEl.classList.add('d-none');
            });
    }

    setInterval(actualizar, intervalo);
    document.addEventListener('visibilitychange', function () { if (!document.hidden) { actualizar(); } });
})();
