# Autenticación, roles y permisos (propuesta de diseño)

**Estado:** aprobado e implementado (seguimiento en `odd/tasks/autenticacion.md`). Cubre los casos de uso CU-01 a CU-08 de la ERS (módulo Usuarios) en el backend .NET y en el frontend. La auditoría (CU-30 y CU-31) queda para una etapa siguiente, aunque los permisos y las sesiones ya la contemplan.

**En una línea:** sesiones guardadas en la base con un token opaco en una cookie `HttpOnly`, contraseñas con el algoritmo de ASP.NET Core Identity, y permisos por rol leídos en cada request. No hace falta ningún paquete NuGet nuevo.

## Decisiones

| # | Tema | Decisión | Por qué |
|---|------|----------|---------|
| D1 | Sesión | Token aleatorio de 256 bits. En la base se guarda solo su huella SHA-256 (tabla `sesiones_usuario`). Duración absoluta configurable, 12 h por defecto (una jornada de campo). | La ERS pide que cerrar sesión "invalide el token" (CU-02) y que un cambio de rol se aplique "en su siguiente interacción" (CU-07). Un JWT sigue siendo válido hasta que vence; una sesión en la base se revoca en el acto. |
| D2 | Transporte del token | Cookie `HttpOnly`, `Secure` y `SameSite=Strict`. El frontend llama con `credentials: "include"` y CORS permite credenciales solo para los orígenes configurados. | El JavaScript de la página no puede leer el token, así que un XSS no lo roba. `SameSite=Strict` evita CSRF desde otros sitios. `localhost:5173` y `localhost:8000` son el mismo sitio, así que funciona en desarrollo sin proxy. |
| D3 | Contraseñas | `PasswordHasher<T>` de ASP.NET Core Identity (PBKDF2), que ya viene en el framework compartido. Mínimo 8 caracteres. | Algoritmo estándar y mantenido, sin escribir criptografía propia. |
| D4 | Cuánto de Identity | No se usa `UserManager` ni el almacenamiento de Identity en EF (`AspNetUsers` y las demás tablas). `Usuario` y `Rol` siguen siendo entidades del dominio. | Esas tablas duplicarían `usuarios` y `roles`, que ya están referenciadas por capturas y sesiones de captura. El bloqueo por intentos ya está previsto en `Usuario` (`IntentosFallidos`, `BloqueadoHasta`). |
| D5 | Paquetes | Ninguno nuevo. `Microsoft.AspNetCore.Identity.EntityFrameworkCore` y `Microsoft.AspNetCore.Authentication.JwtBearer` estaban aprobados, pero con D1 y D4 no hacen falta. | Menos dependencias y menos superficie de configuración (no hay clave de firma JWT que custodiar). |
| D6 | Permisos | Catálogo fijo en el código (ver tabla siguiente). Cada rol tiene un conjunto de permisos editable por el administrador (CU-08), guardado en la columna `roles.permisos` (`text[]`). | Las funcionalidades las define el sistema; lo configurable es qué rol accede a cada una, que es lo que pide CU-08. |
| D7 | "Administrador" | Un usuario activo cuyo rol tiene el permiso `roles_gestionar`. Ninguna operación puede dejar el sistema sin al menos uno. | Es la regla común a CU-05 (5a), CU-07 (3a) y CU-08 (4a). |
| D8 | Primer administrador | Al arrancar, si se configuran `Autenticacion:AdministradorInicial:Correo`, `Nombre` y `Contrasena` y no existe un usuario con ese correo, se lo crea con el rol Administrador. Los valores van en User Secrets o en variables de entorno, nunca en el repositorio. | Evita credenciales escritas en el código y deja un camino reproducible para cada entorno. |
| D9 | Usuario "Sistema" de la semilla | Pasa a inactivo (no puede iniciar sesión ni cuenta como administrador). Se conserva porque es dueño de los datos cargados antes de la autenticación. | No se pierde la autoría de lo ya registrado. |

## Permisos y roles iniciales

Los roles iniciales siguen los actores que la ERS asigna a cada caso de uso. Como CU-08 permite editarlos, son solo el punto de partida.

| Permiso | Casos de uso | Administrador | Productor Ganadero | Operario de Campo |
|---------|--------------|:---:|:---:|:---:|
| `capturas_registrar` | CU-09 a CU-11, CU-13 a CU-15 | | | ✓ |
| `rodeo_gestionar` (animales y lotes) | CU-16 a CU-18 | | ✓ | ✓ |
| `rodeo_consultar` | CU-19 a CU-22, CU-24, CU-26 | | ✓ | ✓ |
| `analitica_consultar` (alertas del rodeo, panel, reportes) | CU-27 a CU-29 | | ✓ | |
| `usuarios_gestionar` | CU-03 a CU-07 | ✓ | | |
| `roles_gestionar` | CU-08 | ✓ | | |
| `auditoria_consultar` | CU-31 (etapa siguiente) | ✓ | | |

> **A confirmar:** según la ERS, el Administrador no ve el rodeo y el Productor no registra capturas. En un establecimiento chico la misma persona suele hacer las dos cosas; con CU-08 se le agregan permisos al rol o se le da otro rol a esa persona.

Cualquier usuario autenticado puede consultar el catálogo de razas y su propia sesión. `/health` y el inicio de sesión no requieren autenticación; todo lo demás sí (política por defecto).

## Reglas de inicio de sesión (CU-01)

| Situación | Respuesta | Mensaje (`detail`) |
|-----------|-----------|--------------------|
| Correo o contraseña incorrectos (2a) | 401 | "Usuario o contraseña incorrectos." Suma un intento fallido si el correo existe. |
| 5 intentos fallidos consecutivos (2b) | 429 con `Retry-After` | "El usuario está bloqueado por intentos fallidos. Volvé a intentar en N minutos." Dura 5 minutos; mientras tanto se rechaza incluso la contraseña correcta. |
| Cuenta dada de baja (2c) | 403 | "No es posible acceder con esta cuenta. Contactá a un administrador." Solo se informa si la contraseña es correcta, para no revelar qué correos existen. |
| Éxito | 200 + cookie | Devuelve el usuario con su rol, sus permisos y la pantalla de inicio de su rol. Reinicia el contador de intentos. |

Un inicio exitoso no revela si el correo existía antes. El contador se reinicia con un ingreso correcto o cuando vence el bloqueo.

## Cada request autenticado

1. Se lee la cookie, se calcula la huella y se busca la sesión en una sola consulta (sesión, usuario, rol y permisos).
2. Se rechaza con 401 si la sesión no existe, está revocada o venció, o si el usuario está inactivo.
3. `IContextoUsuario` pasa a salir de esa sesión (usuario y establecimiento reales), en lugar del usuario "Sistema".
4. Cada endpoint exige su permiso; si falta, responde 403.

**Consecuencias:** cambiar el rol o los permisos de un rol se aplica en el siguiente request (CU-07 paso 5, CU-08 paso 5). Dar de baja a un usuario o cambiarle la contraseña revoca todas sus sesiones.

## API

| Endpoint | Permiso | Caso de uso |
|----------|---------|-------------|
| `POST /auth/sesion` `{correo, contrasena}` | anónimo | CU-01 |
| `DELETE /auth/sesion` | autenticado | CU-02 (204, revoca la sesión y borra la cookie) |
| `GET /auth/sesion` | autenticado | Usuario actual, rol y permisos (al recargar la página) |
| `GET /usuarios?id_rol=` | `usuarios_gestionar` | CU-06 |
| `POST /usuarios` | `usuarios_gestionar` | CU-03 (409 si el correo ya existe) |
| `PUT /usuarios/{id}` | `usuarios_gestionar` | CU-04 (nombre, correo, contraseña) |
| `PUT /usuarios/{id}/rol` | `usuarios_gestionar` | CU-07 (422 si es el mismo rol o deja sin administrador) |
| `POST /usuarios/{id}/baja` | `usuarios_gestionar` | CU-05 (baja lógica; 422 si es el último administrador) |
| `GET /roles`, `GET /permisos` | `roles_gestionar` o `usuarios_gestionar` | Listado de roles con sus permisos y catálogo de permisos |
| `PUT /roles/{id}/permisos` | `roles_gestionar` | CU-08 (422 si deja sin administrador) |

Los endpoints actuales de capturas, pesajes, trazabilidad y analítica conservan su contrato y pasan a exigir el permiso de la tabla anterior.

## Modelo de datos (migración nueva)

- `roles.permisos text[]`: los permisos se leen siempre junto con el rol; se guardan como texto en snake_case, como los demás enums. (La propuesta original era una tabla `rol_permisos`; se cambió durante la implementación, ver el Log de ODD.)
- `sesiones_usuario (id, id_usuario, huella, creada_en, expira_en, revocada_en)`: índice único en `huella`.
- Semilla: roles Productor Ganadero y Operario de Campo, más los permisos iniciales de los tres roles. El usuario Sistema pasa a `activo = false`.

En Development, la migración se aplica al arrancar la API. Con los User Secrets de Neon eso ocurre en la rama `develop`, que no tiene datos de negocio.

## Frontend

- Pantalla `/login` fuera del layout. Al cargar la app se consulta `GET /auth/sesion`; un 401 en cualquier llamada lleva al login.
- El menú muestra solo las secciones permitidas. Cada rol entra a su pantalla principal: Operario a Captura, Productor a Panel, Administrador a Usuarios (CU-01 paso 4).
- Pie del menú con el nombre real del usuario y "Cerrar sesión".
- Pantallas nuevas: Usuarios (listado con filtro por rol, alta, edición, cambio de rol y baja con confirmación) y Roles (permisos de cada rol con casillas).

## Pruebas

- **Dominio:** bloqueo a los 5 intentos y desbloqueo a los 5 minutos (con `TimeProvider` controlado), regla del último administrador, permisos del rol.
- **API (Testcontainers):** cada fila de la tabla de inicio de sesión, 401 sin cookie, 403 sin permiso, cierre de sesión que invalida el token, cambio de rol aplicado en el request siguiente, baja que corta las sesiones abiertas.
- **Tests existentes:** la fábrica de tests crea un usuario con todos los permisos e inicia sesión. Los 183 tests actuales siguen probando lo mismo, ahora autenticados.
- **Frontend:** lógica pura en Vitest (permisos por sección, pantalla de inicio por rol). Las pantallas las prueba el equipo en el navegador.

## Fuera de alcance

- Auditoría (CU-30, CU-31), salvo que el permiso ya existe.
- Recuperación de contraseña por correo (la ERS no la pide; el administrador la cambia con CU-04).
- Aviso de pesajes sin sincronizar al cerrar sesión (CU-02 1a), porque todavía no hay modo offline.
- Varios establecimientos: se mantiene el único establecimiento de la semilla.
