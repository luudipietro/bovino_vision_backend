using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BovinoVision.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "establecimientos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ubicacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_establecimientos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "razas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_razas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lotes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_establecimiento = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gmd_objetivo = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotes", x => x.id);
                    table.ForeignKey(
                        name: "fk_lotes_establecimientos_id_establecimiento",
                        column: x => x.id_establecimiento,
                        principalTable: "establecimientos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_establecimiento = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_roles_establecimientos_id_establecimiento",
                        column: x => x.id_establecimiento,
                        principalTable: "establecimientos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "animales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_establecimiento = table.Column<Guid>(type: "uuid", nullable: false),
                    identificador = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    id_raza = table.Column<Guid>(type: "uuid", nullable: true),
                    sexo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    fecha_nacimiento = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_animales", x => x.id);
                    table.ForeignKey(
                        name: "fk_animales_establecimientos_id_establecimiento",
                        column: x => x.id_establecimiento,
                        principalTable: "establecimientos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_animales_razas_id_raza",
                        column: x => x.id_raza,
                        principalTable: "razas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_establecimiento = table.Column<Guid>(type: "uuid", nullable: false),
                    id_rol = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    resumen_contrasena = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    intentos_fallidos = table.Column<int>(type: "integer", nullable: false),
                    bloqueado_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuarios_establecimientos_id_establecimiento",
                        column: x => x.id_establecimiento,
                        principalTable: "establecimientos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuarios_roles_id_rol",
                        column: x => x.id_rol,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historial_lotes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_animal = table.Column<Guid>(type: "uuid", nullable: false),
                    id_lote = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    vigente_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vigente_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historial_lotes", x => x.id);
                    table.ForeignKey(
                        name: "fk_historial_lotes_animales_id_animal",
                        column: x => x.id_animal,
                        principalTable: "animales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_lotes_lotes_id_lote",
                        column: x => x.id_lote,
                        principalTable: "lotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_historial_lotes_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sesiones_captura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sesiones_captura", x => x.id);
                    table.ForeignKey(
                        name: "fk_sesiones_captura_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_animal = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    id_sesion = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_captura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo_fallo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    proximo_intento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_revision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capturas", x => x.id);
                    table.ForeignKey(
                        name: "fk_capturas_animales_id_animal",
                        column: x => x.id_animal,
                        principalTable: "animales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capturas_sesiones_captura_id_sesion",
                        column: x => x.id_sesion,
                        principalTable: "sesiones_captura",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capturas_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "imagenes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_captura = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia_imagen = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    huella_verificacion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tipo_contenido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imagenes", x => x.id);
                    table.ForeignKey(
                        name: "fk_imagenes_capturas_id_captura",
                        column: x => x.id_captura,
                        principalTable: "capturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eventos_pesaje",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_animal = table.Column<Guid>(type: "uuid", nullable: false),
                    id_captura = table.Column<Guid>(type: "uuid", nullable: false),
                    id_imagen_utilizada = table.Column<Guid>(type: "uuid", nullable: false),
                    id_historial_lote = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_pesaje = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    peso_kg = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    fecha_procesamiento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version_modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_pesaje", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventos_pesaje_animales_id_animal",
                        column: x => x.id_animal,
                        principalTable: "animales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_pesaje_capturas_id_captura",
                        column: x => x.id_captura,
                        principalTable: "capturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_pesaje_historial_lotes_id_historial_lote",
                        column: x => x.id_historial_lote,
                        principalTable: "historial_lotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_pesaje_imagenes_id_imagen_utilizada",
                        column: x => x.id_imagen_utilizada,
                        principalTable: "imagenes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alertas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_evento = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    prioridad = table.Column<int>(type: "integer", nullable: false),
                    variacion = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    gmd_esperada = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    version_regla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fecha_generacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alertas", x => x.id);
                    table.ForeignKey(
                        name: "fk_alertas_eventos_pesaje_id_evento",
                        column: x => x.id_evento,
                        principalTable: "eventos_pesaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "establecimientos",
                columns: new[] { "id", "nombre", "ubicacion" },
                values: new object[] { new Guid("01920000-0000-7000-8000-000000000001"), "Establecimiento principal", null });

            migrationBuilder.InsertData(
                table: "razas",
                columns: new[] { "id", "nombre" },
                values: new object[,]
                {
                    { new Guid("01920000-0000-7000-8000-000000000101"), "Aberdeen Angus" },
                    { new Guid("01920000-0000-7000-8000-000000000102"), "Hereford" },
                    { new Guid("01920000-0000-7000-8000-000000000103"), "Brangus" },
                    { new Guid("01920000-0000-7000-8000-000000000104"), "Braford" },
                    { new Guid("01920000-0000-7000-8000-000000000105"), "Shorthorn" },
                    { new Guid("01920000-0000-7000-8000-000000000106"), "Holando Argentino" },
                    { new Guid("01920000-0000-7000-8000-000000000107"), "Cruza" }
                });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "id_establecimiento", "nombre" },
                values: new object[] { new Guid("01920000-0000-7000-8000-000000000002"), new Guid("01920000-0000-7000-8000-000000000001"), "Administrador" });

            migrationBuilder.InsertData(
                table: "usuarios",
                columns: new[] { "id", "activo", "bloqueado_hasta", "correo", "id_establecimiento", "id_rol", "intentos_fallidos", "nombre", "resumen_contrasena" },
                values: new object[] { new Guid("01920000-0000-7000-8000-000000000003"), true, null, "sistema@bovinovision.local", new Guid("01920000-0000-7000-8000-000000000001"), new Guid("01920000-0000-7000-8000-000000000002"), 0, "Sistema", "!" });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_estado_prioridad",
                table: "alertas",
                columns: new[] { "estado", "prioridad" });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_id_evento",
                table: "alertas",
                column: "id_evento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_animales_id_establecimiento_identificador",
                table: "animales",
                columns: new[] { "id_establecimiento", "identificador" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_animales_id_raza",
                table: "animales",
                column: "id_raza");

            migrationBuilder.CreateIndex(
                name: "ix_capturas_estado_proximo_intento",
                table: "capturas",
                columns: new[] { "estado", "proximo_intento" });

            migrationBuilder.CreateIndex(
                name: "ix_capturas_id_animal",
                table: "capturas",
                column: "id_animal");

            migrationBuilder.CreateIndex(
                name: "ix_capturas_id_sesion",
                table: "capturas",
                column: "id_sesion");

            migrationBuilder.CreateIndex(
                name: "ix_capturas_id_usuario",
                table: "capturas",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_pesaje_id_animal_fecha_pesaje",
                table: "eventos_pesaje",
                columns: new[] { "id_animal", "fecha_pesaje" });

            migrationBuilder.CreateIndex(
                name: "ix_eventos_pesaje_id_captura",
                table: "eventos_pesaje",
                column: "id_captura",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_pesaje_id_historial_lote",
                table: "eventos_pesaje",
                column: "id_historial_lote");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_pesaje_id_imagen_utilizada",
                table: "eventos_pesaje",
                column: "id_imagen_utilizada",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_historial_lotes_id_animal",
                table: "historial_lotes",
                column: "id_animal",
                unique: true,
                filter: "vigente_hasta IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_historial_lotes_id_lote_vigente_hasta",
                table: "historial_lotes",
                columns: new[] { "id_lote", "vigente_hasta" });

            migrationBuilder.CreateIndex(
                name: "ix_historial_lotes_id_usuario",
                table: "historial_lotes",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "ix_imagenes_id_captura",
                table: "imagenes",
                column: "id_captura");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_id_establecimiento_codigo",
                table: "lotes",
                columns: new[] { "id_establecimiento", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_razas_nombre",
                table: "razas",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_id_establecimiento",
                table: "roles",
                column: "id_establecimiento");

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_captura_id_usuario",
                table: "sesiones_captura",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_correo",
                table: "usuarios",
                column: "correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_id_establecimiento",
                table: "usuarios",
                column: "id_establecimiento");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_id_rol",
                table: "usuarios",
                column: "id_rol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "eventos_pesaje");

            migrationBuilder.DropTable(
                name: "historial_lotes");

            migrationBuilder.DropTable(
                name: "imagenes");

            migrationBuilder.DropTable(
                name: "lotes");

            migrationBuilder.DropTable(
                name: "capturas");

            migrationBuilder.DropTable(
                name: "animales");

            migrationBuilder.DropTable(
                name: "sesiones_captura");

            migrationBuilder.DropTable(
                name: "razas");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "establecimientos");
        }
    }
}
