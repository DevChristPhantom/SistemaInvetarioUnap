using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisLabTopo.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IntentosFallidos = table.Column<int>(type: "INTEGER", nullable: false),
                    HoraBloqueoUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigEntries",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "TEXT", nullable: false),
                    Valor = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigEntries", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "Equipos",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Denominacion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Modelo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Marca = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Serie = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Disponible = table.Column<bool>(type: "INTEGER", nullable: false),
                    Observacion = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipos", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "Prestamos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Docente = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Curso = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Semestre = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NombreEstudiante = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CodigoEstudiante = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FechaPrestamo = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaDevolucion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prestamos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DetallesPrestamo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PrestamoId = table.Column<string>(type: "TEXT", nullable: false),
                    EquipoCodigo = table.Column<string>(type: "TEXT", nullable: false),
                    ObservacionItem = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Devuelto = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesPrestamo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesPrestamo_Equipos_EquipoCodigo",
                        column: x => x.EquipoCodigo,
                        principalTable: "Equipos",
                        principalColumn: "Codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallesPrestamo_Prestamos_PrestamoId",
                        column: x => x.PrestamoId,
                        principalTable: "Prestamos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppState",
                columns: new[] { "Id", "HoraBloqueoUtc", "IntentosFallidos" },
                values: new object[] { 1, null, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPrestamo_EquipoCodigo",
                table: "DetallesPrestamo",
                column: "EquipoCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPrestamo_PrestamoId",
                table: "DetallesPrestamo",
                column: "PrestamoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppState");

            migrationBuilder.DropTable(
                name: "ConfigEntries");

            migrationBuilder.DropTable(
                name: "DetallesPrestamo");

            migrationBuilder.DropTable(
                name: "Equipos");

            migrationBuilder.DropTable(
                name: "Prestamos");
        }
    }
}
