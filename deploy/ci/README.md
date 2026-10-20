# Hangfire: release y despliegue

`release/v1.0.0` despliega Hangfire en el droplet Nextcloud mediante
`.github/workflows/droplet-release.yml`. Cada push compila .NET 10, ejecuta las
pruebas y empaqueta la publicación sin appsettings ni secretos. Los PR ejecutan
CI en GitHub; no acceden al droplet.

El job de despliegue se ejecuta en el runner dedicado `nextcloud-hangfire-release`
(etiqueta `poliedro-droplet-hangfire`), con el usuario sin privilegios `ci-hangfire`.
El entorno GitHub `droplet` permite solamente `release/v1.0.0`. La conexión
SSH es local al droplet, valida la clave del host y usa un comando forzado que
solo puede actualizar Hangfire. No se abrió SSH a direcciones de GitHub.

El receptor verifica el SHA256 y el commit, bloquea despliegues simultáneos de
Billing/Hangfire, conserva la imagen anterior y revierte si fallan las comprobaciones.
La configuración y los secretos existentes permanecen en `/opt/poliedro-billing`.
No se recrean MySQL, Nextcloud ni el proxy.

La documentación y el receptor compartido se mantienen en
[Billing, deploy/ci](https://github.com/PoliedroSoftware/backend-api-billing/tree/release/v1.0.0/deploy/ci).
Los cambios del receptor/infraestructura requieren mantenimiento administrativo;
no se ejecutan archivos de control incluidos en un artefacto de CI.

Ver el resultado del último despliegue en
`/opt/poliedro-billing/ci-state/hangfire.json`, el panel público en
https://hangfire.holdinglalabranza.com/hangfire y los detalles privados en
`/opt/poliedro-billing/releases/hangfire/`.

Se conserva `Hangfire__DashboardReadOnly=true`, conexión MySQL con
`Allow User Variables=True` y control de elegibilidad/licencia mediante Billing.
El panel no permite ejecutar ni eliminar trabajos. La programación automática
mantiene la autorización por cliente y licencia; no se hacen emisiones fiscales
como prueba del pipeline.

El workflow AWS heredado excluye esta rama. Los otros destinos y la rama
predeterminada no se cambiaron. Los paquetes/imágenes anteriores se conservan
en el droplet para reversión; supervisar su retención y espacio disponible.
