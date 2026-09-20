# Amazon ZL Remote — versión independiente

Esta carpeta contiene una implementación nueva de Amazon ZL Remote que **no importa ni enlaza código de RustDesk**.

## Objetivo de la primera versión

- Cliente Windows compatible con Windows 7 SP1, Windows 10 y Windows 11 mediante .NET Framework 4.8.
- El mismo ejecutable puede recibir o iniciar una sesión.
- Cada equipo muestra un código de 9 dígitos.
- La persona frente al equipo remoto debe **aceptar visiblemente** cada sesión.
- Pantalla, teclado y mouse a través de un relay propio.
- Sin servicio oculto, sin instalación persistente y sin acceso desatendido en esta versión.
- El relay es nuestro y puede ejecutarse en Windows o Linux.

## Arquitectura

`client/` contiene el programa Windows.
`server/` contiene el relay independiente.
`.github/workflows/build-independent.yml` genera los binarios.

Los clientes realizan conexiones salientes al relay, por lo que los equipos controlados no necesitan abrir puertos. Para usarlo por Internet, el relay sí debe estar en una dirección pública y, para producción, debe usarse TLS.

## Prueba local

1. Inicie el relay en una PC:
   `AmazonZLRelayServer --insecure-dev --port 21120`
2. En dos PCs de la misma red, abra Amazon ZL Remote.
3. Configure la IP de la PC que ejecuta el relay y el puerto 21120, sin TLS.
4. Conecte ambos clientes al servidor.
5. Escriba el código del equipo remoto y pulse **Conectar**.
6. La PC remota mostrará una solicitud que debe aceptar.

## Internet

Para Internet se requiere alojar el relay en un servidor accesible públicamente. La versión de producción debe usar un certificado TLS válido. El relay admite un archivo PFX:

`AmazonZLRelayServer --port 443 --pfx certificado.pfx --password CLAVE`

La compilación no incorpora contraseñas ni certificados.

## Seguridad de diseño

Esta versión exige consentimiento visible por sesión. No incluye funciones para ocultar la sesión, eludir avisos de Windows, capturar credenciales ni instalar persistencia silenciosa.
