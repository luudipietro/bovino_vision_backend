using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BovinoVision.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VencimientoProcesamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "vence_procesamiento",
                table: "capturas",
                type: "timestamp with time zone",
                nullable: true);

            // Captures already processing kept their lease deadline in proximo_intento.
            migrationBuilder.Sql(
                "UPDATE capturas SET vence_procesamiento = proximo_intento, proximo_intento = NULL WHERE estado = 'procesando'");

            migrationBuilder.CreateIndex(
                name: "ix_capturas_estado_vence_procesamiento",
                table: "capturas",
                columns: new[] { "estado", "vence_procesamiento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_capturas_estado_vence_procesamiento",
                table: "capturas");

            migrationBuilder.Sql(
                "UPDATE capturas SET proximo_intento = vence_procesamiento WHERE estado = 'procesando'");

            migrationBuilder.DropColumn(
                name: "vence_procesamiento",
                table: "capturas");
        }
    }
}
