# Neon para Pi y Claude Code

El proyecto configura el MCP oficial de Neon con OAuth y reutiliza las skills de Neon ya instaladas. No guarda claves de API en estos archivos, no modifica la configuración global de los agentes y no conecta automáticamente el backend .NET a una base remota.

## Archivos

| Archivo | Uso |
| --- | --- |
| `.mcp.json` | Servidor `Neon` para Claude Code, por HTTP con OAuth |
| `.pi/mcp.json` | El mismo servidor para el soporte MCP nativo de Pi |
| `.pi/settings.json` | Rutas de las nueve skills de Neon existentes en `.claude/skills/` |

Las skills se comparten por referencia: no se copian ni se instalan adaptadores adicionales para Pi. `.claude/` está ignorado por Git, de modo que cada equipo necesita su instalación local de skills. La configuración global existente, incluido `context7` en Pi, se conserva. Las reglas actuales de `.gitignore` también excluyen `.pi/` y `.mcp.json`: estas configuraciones quedan locales y no se fuerza su inclusión en Git.

## Activación por el usuario

### Pi

1. Abrí Pi en la raíz del repositorio y revisá los archivos de configuración antes de otorgar confianza al proyecto. Usá `/trust` para guardar esa decisión.
2. Ejecutá `/reload` para cargar el MCP y las skills del proyecto.
3. Usá `/mcp` y la opción de inicio de sesión de `Neon`, o ejecutá `/mcp login Neon`, y aprobá los permisos que correspondan en el navegador.

Pi guarda las credenciales OAuth fuera del repositorio. No pegues tokens ni contraseñas en los archivos del proyecto o en el chat. Sin confianza, Pi ignora su configuración local; sin OAuth, no se considera comprobada la conexión a Neon.

### Claude Code

Abrí nuevamente el proyecto, revisá la configuración MCP y usá `/mcp` para autenticar `Neon` cuando lo solicite. El archivo del proyecto contiene únicamente la URL oficial: `https://mcp.neon.tech/mcp`.

## Reinstalación de las skills

Para un equipo sin estas skills, la CLI Neon 8.8.0 admite la instalación local de sus skills predeterminadas para Claude Code:

```bash
neon skills --agent claude-code -y
```

Revisá las skills antes de habilitarlas; `.pi/settings.json` enumera explícitamente las nueve que utiliza Pi. No ejecutes instalaciones o actualizaciones globales para reproducir esta configuración de proyecto.

## Alcance de la comprobación

Se validaron el JSON de ambos MCP, las nueve rutas locales de skills y sus metadatos, y la conservación de los nueve hashes existentes en `skills-lock.json`. La activación y la conexión autenticada requieren la confianza y el consentimiento OAuth del usuario; no se aprobaron automáticamente.

Esta configuración no crea proyectos, ramas ni bases en Neon; tampoco ejecuta SQL o migraciones ni modifica la cadena de conexión de la API. El backend y la suite automática mantienen su configuración anterior. Para desarrollo manual contra Neon, el siguiente paso es elegir una base o rama exclusiva y configurar la conexión del backend por separado; no se deben apuntar los tests que limpian tablas a una base compartida.
