using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class UpdateBackgroundTaskStateKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"BackgroundTaskStates\" SET \"Key\" = replace(\"Key\", 'Bearcat.Application.BackgroundTasks.', 'Bearcat.BackgroundTasks.');"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"BackgroundTaskStates\" SET \"Key\" = replace(\"Key\", 'Bearcat.BackgroundTasks.', 'Bearcat.Application.BackgroundTasks.');"
            );
        }
    }
}
