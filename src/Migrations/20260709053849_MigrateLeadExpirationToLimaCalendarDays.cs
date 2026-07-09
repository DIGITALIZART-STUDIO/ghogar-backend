using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLeadExpirationToLimaCalendarDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Recalcular ExpirationDate al fin del último día válido (Lima): referencia + 7 días calendario
            migrationBuilder.Sql(
                """
                UPDATE "Leads"
                SET "ExpirationDate" = (
                    (
                        (
                            (
                                CASE
                                    WHEN "EntryDate" IS NULL OR "EntryDate" < TIMESTAMP '2000-01-01'
                                        THEN "CreatedAt"
                                    ELSE "EntryDate"
                                END
                            ) AT TIME ZONE 'America/Lima'
                        )::date + 7
                    )::timestamp + time '23:59:59.999'
                ) AT TIME ZONE 'America/Lima';
                """
            );

            // Reactivar leads marcados Expired que aún están dentro del último día válido
            migrationBuilder.Sql(
                """
                UPDATE "Leads" AS l
                SET "Status" = CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM "LeadTasks" t
                        WHERE t."LeadId" = l."Id"
                          AND t."IsActive" = true
                          AND t."IsCompleted" = false
                    ) THEN 2
                    WHEN EXISTS (
                        SELECT 1
                        FROM "LeadTasks" t
                        WHERE t."LeadId" = l."Id"
                    ) THEN 1
                    ELSE 0
                END
                WHERE l."Status" = 5
                  AND l."IsActive" = true
                  AND (NOW() AT TIME ZONE 'America/Lima')::date <= (
                      (
                          (
                              CASE
                                  WHEN l."EntryDate" IS NULL OR l."EntryDate" < TIMESTAMP '2000-01-01'
                                      THEN l."CreatedAt"
                                  ELSE l."EntryDate"
                              END
                          ) AT TIME ZONE 'America/Lima'
                      )::date + 7
                  );
                """
            );

            // Marcar como Expired los leads activos cuyo último día válido ya pasó
            migrationBuilder.Sql(
                """
                UPDATE "Leads" AS l
                SET "Status" = 5,
                    "ModifiedAt" = NOW()
                WHERE l."IsActive" = true
                  AND l."Status" NOT IN (3, 4, 5)
                  AND (NOW() AT TIME ZONE 'America/Lima')::date > (
                      (
                          (
                              CASE
                                  WHEN l."EntryDate" IS NULL OR l."EntryDate" < TIMESTAMP '2000-01-01'
                                      THEN l."CreatedAt"
                                  ELSE l."EntryDate"
                              END
                          ) AT TIME ZONE 'America/Lima'
                      )::date + 7
                  );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Migración de datos no reversible de forma segura
        }
    }
}
