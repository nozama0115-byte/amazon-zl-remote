# Amazon ZL Remote

Cliente de acceso remoto para **Windows x64**, personalizado para Amazon Zona Libre S.A. y basado en RustDesk.

## Enfoque de seguridad

Este proyecto está pensado para soporte remoto legítimo: la sesión es visible y el usuario remoto debe autorizar el acceso. No incluye mecanismos de acceso oculto ni de evasión del consentimiento.

## Base reproducible

La compilación usa exactamente el commit de RustDesk:

`97811acbddf9d1e12c640bff99ea0c731b031291`

Los cambios propios están en `overlay/`. El flujo de GitHub Actions descarga esa base, aplica el overlay y genera el ejecutable portable.

## Compilación

Al subir este contenido a la rama `main`, GitHub Actions inicia automáticamente **Build Amazon ZL Remote Windows x64**. También puede iniciarse manualmente desde la pestaña **Actions**.

El resultado se publica como el artefacto:

`Amazon-ZL-Remote-Windows-x64`

Dentro quedan:

- `Amazon-ZL-Remote-Windows-x64.exe`
- `SHA256.txt`

## Licencia

RustDesk se distribuye bajo AGPL-3.0. Este repositorio conserva la licencia y hace reproducible la combinación del código base exacto con las modificaciones de Amazon ZL Remote.
