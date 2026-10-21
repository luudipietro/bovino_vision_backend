using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BovinoVision.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Autenticacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "permisos",
                table: "roles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateTable(
                name: "sesiones_usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<Guid>(type: "uuid", nullable: false),
                    huella = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revocada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sesiones_usuario", x => x.id);
                    table.ForeignKey(
                        name: "fk_sesiones_usuario_usuarios_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("01920000-0000-7000-8000-000000000002"),
                column: "permisos",
                value: new[] { "usuarios_gestionar", "roles_gestionar", "auditoria_consultar" });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "id_establecimiento", "nombre", "permisos" },
                values: new object[,]
                {
                    { new Guid("01920000-0000-7000-8000-000000000004"), new Guid("01920000-0000-7000-8000-000000000001"), "Productor Ganadero", new[] { "rodeo_gestionar", "rodeo_consultar", "analitica_consultar" } },
                    { new Guid("01920000-0000-7000-8000-000000000005"), new Guid("01920000-0000-7000-8000-000000000001"), "Operario de Campo", new[] { "capturas_registrar", "rodeo_gestionar", "rodeo_consultar" } }
                });

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("01920000-0000-7000-8000-000000000003"),
                column: "activo",
                value: false);

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_usuario_huella",
                table: "sesiones_usuario",
                column: "huella",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_usuario_id_usuario",
                table: "sesiones_usuario",
                column: "id_usuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sesiones_usuario");

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("01920000-0000-7000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("01920000-0000-7000-8000-000000000005"));

            migrationBuilder.DropColumn(
                name: "permisos",
                table: "roles");

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "id",
                keyValue: new Guid("01920000-0000-7000-8000-000000000003"),
                column: "activo",
                value: true);
        }
    }
}
