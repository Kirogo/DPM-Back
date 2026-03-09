// Migrations/20260304065931_AddCommentsNavigationToChecklist.cs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace geoback.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentsNavigationToChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SAFE MIGRATION - Only creates Comments table if it doesn't exist
            // Preserves all existing data
            
            migrationBuilder.Sql(@"
                -- Create Comments table if it doesn't exist
                CREATE TABLE IF NOT EXISTS `Comments` (
                    `Id` char(36) NOT NULL,
                    `ReportId` char(36) NOT NULL,
                    `UserId` char(36) NOT NULL,
                    `UserName` longtext NOT NULL,
                    `UserRole` longtext NOT NULL,
                    `Text` longtext NOT NULL,
                    `IsInternal` tinyint(1) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_Comments_ReportId` (`ReportId`),
                    KEY `IX_Comments_UserId` (`UserId`),
                    KEY `IX_Comments_CreatedAt` (`CreatedAt`),
                    CONSTRAINT `FK_Comments_Checklists_ReportId` FOREIGN KEY (`ReportId`) REFERENCES `Checklists` (`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            // Add indexes if they don't exist (safe)
            migrationBuilder.Sql(@"
                -- Add index on ReportId if it doesn't exist
                SET @dbname = DATABASE();
                SET @exist = (SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = @dbname AND table_name = 'Comments' AND index_name = 'IX_Comments_ReportId');
                SET @sql = IF(@exist = 0, 
                    'CREATE INDEX IX_Comments_ReportId ON Comments (ReportId)',
                    'SELECT ""Index IX_Comments_ReportId already exists""'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                -- Add index on UserId if it doesn't exist
                SET @exist = (SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = @dbname AND table_name = 'Comments' AND index_name = 'IX_Comments_UserId');
                SET @sql = IF(@exist = 0, 
                    'CREATE INDEX IX_Comments_UserId ON Comments (UserId)',
                    'SELECT ""Index IX_Comments_UserId already exists""'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                -- Add index on CreatedAt if it doesn't exist
                SET @exist = (SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = @dbname AND table_name = 'Comments' AND index_name = 'IX_Comments_CreatedAt');
                SET @sql = IF(@exist = 0, 
                    'CREATE INDEX IX_Comments_CreatedAt ON Comments (CreatedAt)',
                    'SELECT ""Index IX_Comments_CreatedAt already exists""'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Add foreign key if it doesn't exist
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @exist = (SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE 
                    WHERE CONSTRAINT_SCHEMA = @dbname 
                    AND TABLE_NAME = 'Comments' 
                    AND CONSTRAINT_NAME = 'FK_Comments_Checklists_ReportId'
                    AND REFERENCED_TABLE_NAME = 'Checklists');
                
                SET @sql = IF(@exist = 0, 
                    'ALTER TABLE Comments ADD CONSTRAINT FK_Comments_Checklists_ReportId FOREIGN KEY (ReportId) REFERENCES Checklists (Id) ON DELETE CASCADE',
                    'SELECT ""Foreign key FK_Comments_Checklists_ReportId already exists""'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SAFE DOWN MIGRATION - Don't drop tables with data
            // Just remove the foreign key if it exists (but preserve the table)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @exist = (SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE 
                    WHERE CONSTRAINT_SCHEMA = @dbname 
                    AND TABLE_NAME = 'Comments' 
                    AND CONSTRAINT_NAME = 'FK_Comments_Checklists_ReportId');
                
                SET @sql = IF(@exist > 0, 
                    'ALTER TABLE Comments DROP FOREIGN KEY FK_Comments_Checklists_ReportId',
                    'SELECT ""Foreign key does not exist""'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Drop indexes (these are safe to drop)
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Comments_ReportId ON Comments");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Comments_UserId ON Comments");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Comments_CreatedAt ON Comments");
            
            // Note: We don't drop the Comments table to preserve data
            migrationBuilder.Sql("SELECT 'Comments table preserved to keep existing data' as message;");
        }
    }
}