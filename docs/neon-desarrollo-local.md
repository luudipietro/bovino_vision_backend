# Ejecutar la API local contra Neon develop

La API usa el soporte estándar de User Secrets de ASP.NET Core en `Development`, con ID `bovinovision-api-neon-local`. Este flujo se limita a la rama Neon **`develop`**, base **`neondb`**: no autoriza cambios en `production`, en el esquema administrado `neon_auth` ni importaciones de datos Python.

## Inicio local

1. Antes de arrancar, confirmar que la clave `ConnectionStrings:BovinoVision` de ese ID contiene una conexión Npgsql **directa** a `develop` / `neondb`, con TLS según los parámetros de Neon, no el endpoint con pooler. La obtención y escritura de credenciales requiere autorización específica; conservar las demás claves existentes. No incluir la cadena en Git, documentación, chat ni salidas de diagnóstico.
2. Revisar posibles sobrescrituras: `ConnectionStrings__BovinoVision` en el entorno y argumentos de línea de comandos tienen prioridad sobre User Secrets. No imprimir sus valores. Verificar también que no se haya deshabilitado `BaseDeDatos__AplicarMigracionesAlIniciar`.
3. Desde la raíz del backend, con el SDK de `global.json`, ejecutar:

   ```bash
   dotnet run --project src/BovinoVision.Api
   ```

   El perfil local activa `Development`. El arranque aplica las migraciones EF existentes y su semilla antes de atender solicitudes; revisar y autorizar el destino antes de ejecutarlo. No hace falta iniciar el PostgreSQL local de Compose para esta alternativa.
4. Consultar `http://localhost:8000/health` y `http://localhost:8000/razas`. Salud devuelve `{"status":"ok"}`, pero por sí sola no comprueba la conexión; el catálogo de razas sí requiere acceso a la base. Confirmar además el historial EF en `develop` mediante una verificación autorizada.

## Imágenes en Neon Object Storage

Las fotos de las capturas se guardan en el bucket privado **`uploads`** de la rama **`develop`** (proyecto `hidden-thunder-12545101`). Así todas las PCs que usan la misma base ven las mismas fotos. Sin esta configuración la API guarda en `src/BovinoVision.Api/uploads/` de la PC local, y las demás PCs muestran esas fotos rotas.

Pasos para configurar otra PC. Un agente de IA puede ejecutarlos en orden desde la raíz del backend, con Git Bash:

1. **Requisitos (los hace la persona).** Neon CLI 8.8 o posterior, con sesión iniciada (`neon auth`) en una cuenta que pertenezca a la organización del proyecto. Comprobarlo con:

   ```bash
   neon bucket list --project-id hidden-thunder-12545101 --branch develop
   ```

   Tiene que listar `uploads  private`. Si falla por permisos, la persona tiene que pedir acceso a la organización: el agente no debe intentar otra vía.

2. **Bajar las credenciales a un directorio temporal fuera del repositorio.** Cada `pull` genera una credencial nueva para la rama.

   ```bash
   TMP_NEON=$(mktemp -d)
   (cd "$TMP_NEON" && neon env pull --project-id hidden-thunder-12545101 --branch develop -s object-storage --file neon-s3.env)
   ```

3. **Cargarlas en User Secrets y borrar el archivo temporal.** No imprimir los valores.

   ```bash
   v() { grep -E "^$1=" "$TMP_NEON/neon-s3.env" | cut -d= -f2- | tr -d '"\r'; }
   P=src/BovinoVision.Api
   dotnet user-secrets set "Almacenamiento:S3:Bucket" uploads --project $P
   dotnet user-secrets set "Almacenamiento:S3:UrlServicio" "$(v AWS_ENDPOINT_URL_S3)" --project $P
   dotnet user-secrets set "Almacenamiento:S3:Region" "$(v AWS_REGION)" --project $P
   dotnet user-secrets set "Almacenamiento:S3:IdClaveAcceso" "$(v AWS_ACCESS_KEY_ID)" --project $P
   dotnet user-secrets set "Almacenamiento:S3:ClaveSecreta" "$(v AWS_SECRET_ACCESS_KEY)" --project $P
   rm -rf "$TMP_NEON"
   dotnet user-secrets list --project $P | grep -oE "^Almacenamiento:S3:[A-Za-z]+"
   ```

   El último comando muestra solo los nombres de las cinco claves: `Bucket`, `UrlServicio`, `Region`, `IdClaveAcceso` y `ClaveSecreta`. Si falta alguna con `Bucket` cargado, la API no arranca y avisa con un error de validación de `Almacenamiento:S3`.

4. **Subir las fotos que quedaron en el disco local.** Se suben con su mismo nombre, que es la referencia que guarda la base. Se omiten las que ya están en el bucket y no se borra nada local.

   ```bash
   cd src/BovinoVision.Api/uploads 2>/dev/null && {
     existentes=$(neon bucket object list uploads --project-id hidden-thunder-12545101 --branch develop)
     for f in *; do
       echo "$existentes" | grep -q "$f" && continue
       neon bucket object put "uploads/$f" --file "$f" --project-id hidden-thunder-12545101 --branch develop >/dev/null || echo "falló $f"
     done
     cd - >/dev/null
   }
   ```

   Si no existe `src/BovinoVision.Api/uploads/`, no hay nada que subir.

5. **Reiniciar la API** (`dotnet run --project src/BovinoVision.Api`) y abrir en el frontend una sesión de captura que antes mostraba fotos rotas: ahora tienen que verse.

Límites: este flujo solo usa la rama `develop`, nunca `production`. No se escriben credenciales en el repositorio, en `appsettings` ni en la salida del chat, y no se borra el directorio `uploads/` local. Los tests no usan el bucket: siguen con el almacenamiento local.

## Comprobación realizada

La configuración local quedó cargada y se comprobó contra PostgreSQL **18.6** en `develop` / `neondb`:

- Historial EF con `20261020003057_Inicial` y **13 tablas en `public`**, incluido el historial de migraciones.
- Semilla con **7 razas**, un establecimiento, un usuario Sistema y un rol; sin animales, capturas, eventos de pesaje ni alertas creados por esta comprobación.
- Las **9 tablas administradas de `neon_auth`** conservaron sus nombres; no se hicieron operaciones contra `production`.
- API real: `GET /health` devolvió 200 y cuerpo exacto `{"status":"ok"}`; `GET /razas` devolvió 200 y 7 entradas. El verificador independiente repitió estos GET sin acceder a credenciales ni ejecutar SQL remoto.
- **168/168 tests** correctos en Testcontainers, compilación sin advertencias y formato limpio, también en la verificación independiente. Esos tests siguen usando su PostgreSQL descartable, no Neon.

El servidor temporal de prueba en el puerto 8010 fue detenido después de la comprobación. Para trabajar normalmente, usar el comando anterior en el puerto local 8000. La evidencia remota se obtuvo por MCP del orquestador; el verificador independiente comprobó el código, la suite aislada y las respuestas HTTP. El detalle está en [el seguimiento de esta configuración](../odd/tasks/neon-desarrollo-local.md). Los arranques posteriores dependen de que se conserven el secreto local y el destino autorizado.

## Configuración y límites

- **Precedencia:** `appsettings.json` → archivo del entorno → User Secrets (solo Development) → variables de entorno → argumentos; la última fuente prevalece. `WebApplication.CreateBuilder` ya carga los secretos, sin proveedores personalizados. `dotnet run` no carga `.env` automáticamente.
- **Almacenamiento:** User Secrets queda fuera del repositorio, pero guarda **texto plano, no cifrado**. Es una herramienta local de desarrollo, no un almacén de producción. No modificar `appsettings` para incorporar credenciales.
- **EF en tiempo de diseño:** `FabricaDbContextDeDiseno` solo lee `ConnectionStrings__BovinoVision` o la conexión local por defecto. No carga estos User Secrets; por tanto, no asumir que `dotnet ef database update` apunta a Neon. Este flujo usa el arranque de la API para las migraciones existentes, sin cambiar la fábrica.
- **Tests aislados:** los fixtures mantienen `Testing` y su propia conexión PostgreSQL en Testcontainers. `dotnet test --solution BovinoVision.slnx` requiere Docker y no debe usar Neon; este cambio no modifica fixtures ni sus bases desechables.

Volver a [Configuración del README](../README.md#configuración) para las demás opciones de la API.
