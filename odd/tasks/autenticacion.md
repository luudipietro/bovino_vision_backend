# Feature: autenticacion

Objetivo: inicio y cierre de sesión, bloqueo por intentos, usuarios, roles y permisos (CU-01 a CU-08) en el backend .NET y en el frontend, según el diseño aprobado en `docs/autenticacion.md`. Ramas `feat/autenticacion` en el backend y en el frontend, ambas desde `manu`; un commit por unidad, sin push, PR ni merge. Entrega: rama de feature con commits por unidad; el usuario decide cómo dividir los PR.
Verificación: backend con `dotnet build`, `dotnet format --verify-no-changes` y `dotnet test --solution BovinoVision.slnx` (Testcontainers); frontend con `npm run test`, `typecheck`, `lint` y `build`. Sin recorridos en el navegador (los hace el usuario).

## Specs

- **S1. Pedido.** Usuario: "si, apruebo el diseño. empezá a implementar." El diseño aprobado es `docs/autenticacion.md` (decisiones D1 a D9, permisos y roles iniciales, reglas de inicio de sesión, API, modelo de datos, frontend y pruebas). Antes, el usuario había aprobado dos paquetes ("apruebo esos dos paquetes para la auth con Identity"); el diseño aprobado no los usa (D5).
- **S2. Inicio de sesión (CU-01).** `POST /auth/sesion` con `{ "correo", "contrasena" }`. Respuestas según la tabla del diseño: 401 "Usuario o contraseña incorrectos.", 429 con `Retry-After` y "El usuario está bloqueado por intentos fallidos. Volvé a intentar en N minutos." tras 5 intentos fallidos consecutivos (bloqueo de 5 minutos), 403 "No es posible acceder con esta cuenta. Contactá a un administrador." para una cuenta dada de baja con contraseña correcta, y 200 con la cookie de sesión y el cuerpo `Sesion`.
- **S3. Sesión (D1, D2, CU-02).** Token aleatorio de 256 bits; en la base solo su huella SHA-256; duración configurable (`Autenticacion:DuracionSesionHoras`, 12 por defecto). Cookie `HttpOnly`, `Secure`, `SameSite=Strict`, nombre `bovinovision_sesion`. `GET /auth/sesion` devuelve `Sesion`; `DELETE /auth/sesion` revoca la sesión, borra la cookie y responde 204. Cada request valida sesión y usuario activo, y carga rol y permisos actuales: los cambios se aplican en el request siguiente. Dar de baja o cambiar la contraseña revoca todas las sesiones del usuario.
- **S4. Permisos (D6, D7).** Códigos: `capturas_registrar`, `rodeo_gestionar`, `rodeo_consultar`, `analitica_consultar`, `usuarios_gestionar`, `roles_gestionar`, `auditoria_consultar`. Roles iniciales y permiso por endpoint según el diseño: `/health` y `POST /auth/sesion` anónimos; `/razas` y `/auth/sesion` (GET, DELETE) para cualquier autenticado; lecturas de trazabilidad (animales, lotes, historial de lotes, historial de pesajes y alertas de un animal) con `rodeo_consultar`; altas y cambios de animales y lotes con `rodeo_gestionar`; sesiones de captura, capturas y pesajes con `capturas_registrar`; `/alertas`, `/panel` y `/reportes` con `analitica_consultar`. Sin sesión: 401; sin permiso: 403. Ninguna operación deja el sistema sin un usuario activo con `roles_gestionar`.
- **S5. Usuarios y roles (CU-03 a CU-08).** Endpoints de la tabla del diseño, con estos cuerpos (JSON en snake_case):
  - `Sesion`: `{ "usuario": Usuario, "permisos": string[], "expira_en": fecha-hora }`.
  - `Usuario`: `{ "id", "nombre", "correo", "id_rol", "rol", "activo", "bloqueado_hasta" }`.
  - `Rol`: `{ "id", "nombre", "permisos": string[], "cantidad_usuarios" }`. `Permiso`: `{ "codigo", "descripcion" }`.
  - Entradas: `NuevoUsuario { nombre, correo, contrasena, id_rol }`; `CambiosUsuario { nombre?, correo?, contrasena? }` (null no cambia); `CambioRol { id_rol }`; `PermisosRol { permisos: string[] }`.
  - Errores: 409 "Ya existe un usuario con el correo X" (CU-03 3a, CU-04 3a); 422 "El usuario ya tiene ese rol" (CU-07 2a); 422 "No puede quedar el sistema sin un administrador" (CU-05 5a, CU-07 3a, CU-08 4a); contraseña de menos de 8 caracteres, 422 "La contraseña debe tener al menos 8 caracteres".
- **S6. Primer administrador y semilla (D8, D9).** `Autenticacion:AdministradorInicial:Correo`, `Nombre` y `Contrasena` crean el administrador al arrancar si no existe un usuario con ese correo. Migración nueva con `rol_permisos`, `sesiones_usuario`, roles Productor Ganadero y Operario de Campo, permisos iniciales, y usuario Sistema inactivo. Sin secretos en el repositorio.
- **S7. Frontend.** `/login` fuera del layout; consulta de `GET /auth/sesion` al cargar; un 401 lleva al login; todas las llamadas con `credentials: "include"`. Menú y acciones según permisos (Captura: `capturas_registrar`; Rodeo y Lotes: `rodeo_consultar`, y sus altas y ediciones: `rodeo_gestionar`; Panel y Alertas: `analitica_consultar`; Usuarios: `usuarios_gestionar`; Roles: `roles_gestionar`). Pantalla de inicio según el primer permiso de esta lista: `capturas_registrar` → `/captura`, `analitica_consultar` → `/panel`, `rodeo_consultar` → `/animales`, `usuarios_gestionar` → `/usuarios`, `roles_gestionar` → `/roles`. Pie del menú con el nombre real y "Cerrar sesión". Pantallas Usuarios (filtro por rol, alta, edición, cambio de rol, baja con confirmación) y Roles (casillas de permisos).

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
| --- | --- | --- | --- | --- |
| T1 | S2, S4, S5 | inline | pendiente | Dominio: `Permiso`, permisos del `Rol`, bloqueo y estado del `Usuario`, regla del último administrador. |
| T2 | S3, S6 | inline | pendiente | Persistencia: `SesionUsuario`, configuraciones, migración con semilla, adaptador de contraseñas, administrador inicial. |
| T3 | S2, S3, S4 | inline | pendiente | HTTP: endpoints de sesión, manejador de autenticación por cookie, `IContextoUsuario` desde la sesión, permisos por endpoint, CORS con credenciales, tests existentes autenticados. |
| T4 | S5 | inline | pendiente | Endpoints de usuarios, roles y permisos. |
| T5 | S7 | delegada (paralelo con T1-T4) | pendiente | Frontend: login, sesión, menú por permisos, pantallas Usuarios y Roles. |
| T6 | S1-S7 | inline + verificador | pendiente | Documentación y verificación independiente (riesgo 2: seguridad; riesgo 1: migración). |

Ruta: el backend (T1-T4) va inline porque cada tarea depende de la anterior. T5 se delega en paralelo: es otra unidad de comportamiento, en otro repositorio, con superficie disjunta y varios archivos, y el contrato queda fijado en S2-S5.

## Log

- **L1 (usuario, 2026-10-20):** "ahora avancemos con la autenticación. apruebo esos dos paquetes para la auth con Identity, empeza con el odd"
- **L2 (usuario):** "recomendas hacer un sdd o con un odd es suficiente para lo de auth?" Respuesta: ODD con una propuesta de diseño previa (`docs/autenticacion.md`).
- **L3 (usuario):** "si, apruebo el diseño. empezá a implementar. en el frontend mergea la rama de la correccion y la rama de pantallas-trazabilidad a la rama manu y dejame trabajando ahi" Frontend: `manu` avanzó por fast-forward a `8f053ee`.
- **L4 (punto de partida):** `Usuario` ya tiene `IntentosFallidos`, `BloqueadoHasta` y `Activo`; `Rol` no tiene permisos; `IContextoUsuario` devuelve el usuario Sistema de la semilla. `Infraestructura` referencia `Microsoft.AspNetCore.App`, así que `PasswordHasher<T>` está disponible sin paquetes. `DELETE /capturas/{id}` está en `feat/descartar-captura`, todavía no en `manu`.
