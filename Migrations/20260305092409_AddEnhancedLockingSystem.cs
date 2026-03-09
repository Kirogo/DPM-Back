using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace geoback.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancedLockingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The tables already exist from the SQL script
            // We just need to update the EF model to recognize them
            
            migrationBuilder.Sql(@"
                -- Check if LockSessionId column exists in Checklists
                SET @dbname = DATABASE();
                SET @colExists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = @dbname 
                    AND TABLE_NAME = 'Checklists' 
                    AND COLUMN_NAME = 'LockSessionId');
                
                SET @sql = IF(@colExists = 0, 
                    'ALTER TABLE Checklists ADD COLUMN LockSessionId VARCHAR(100) NULL AFTER LockedByUserRole',
                    'SELECT ""Column LockSessionId already exists""');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                
                -- Check if LockHeartbeat column exists
                SET @colExists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = @dbname 
                    AND TABLE_NAME = 'Checklists' 
                    AND COLUMN_NAME = 'LockHeartbeat');
                
                SET @sql = IF(@colExists = 0, 
                    'ALTER TABLE Checklists ADD COLUMN LockHeartbeat DATETIME NULL AFTER LockSessionId',
                    'SELECT ""Column LockHeartbeat already exists""');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                
                -- Check if LockExpiresAt column exists
                SET @colExists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = @dbname 
                    AND TABLE_NAME = 'Checklists' 
                    AND COLUMN_NAME = 'LockExpiresAt');
                
                SET @sql = IF(@colExists = 0, 
                    'ALTER TABLE Checklists ADD COLUMN LockExpiresAt DATETIME NULL AFTER LockHeartbeat',
                    'SELECT ""Column LockExpiresAt already exists""');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                
                -- Create ReportLocks table if it doesn't exist
                CREATE TABLE IF NOT EXISTS ReportLocks (
                    Id CHAR(36) NOT NULL,
                    ReportId CHAR(36) NOT NULL,
                    UserId CHAR(36) NOT NULL,
                    UserEmail VARCHAR(255) NOT NULL,
                    UserName VARCHAR(255) NOT NULL,
                    UserRole VARCHAR(50) NOT NULL,
                    SessionId VARCHAR(100) NOT NULL,
                    LockedAt DATETIME NOT NULL,
                    LastHeartbeat DATETIME NOT NULL,
                    ExpiresAt DATETIME NOT NULL,
                    IsActive BOOLEAN DEFAULT TRUE,
                    Source VARCHAR(50) DEFAULT 'web',
                    PRIMARY KEY (Id),
                    INDEX idx_report (ReportId),
                    INDEX idx_user (UserId),
                    INDEX idx_session (SessionId),
                    INDEX idx_expires (ExpiresAt),
                    INDEX idx_active (IsActive)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                
                -- Create UserActiveLocks table if it doesn't exist
                CREATE TABLE IF NOT EXISTS UserActiveLocks (
                    UserId CHAR(36) NOT NULL,
                    ReportId CHAR(36) NOT NULL,
                    SessionId VARCHAR(100) NOT NULL,
                    LockedAt DATETIME NOT NULL,
                    LastHeartbeat DATETIME NOT NULL,
                    ExpiresAt DATETIME NOT NULL,
                    PRIMARY KEY (UserId),
                    INDEX idx_user_report (UserId, ReportId),
                    INDEX idx_expires (ExpiresAt)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                
                -- Add foreign keys if they don't exist
                SET @fkExists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                    WHERE CONSTRAINT_SCHEMA = @dbname 
                    AND TABLE_NAME = 'ReportLocks' 
                    AND CONSTRAINT_NAME = 'FK_ReportLocks_Checklists_ReportId');
                
                SET @sql = IF(@fkExists = 0, 
                    'ALTER TABLE ReportLocks ADD CONSTRAINT FK_ReportLocks_Checklists_ReportId FOREIGN KEY (ReportId) REFERENCES Checklists(Id) ON DELETE CASCADE',
                    'SELECT ""Foreign key already exists""');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                
                SET @fkExists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                    WHERE CONSTRAINT_SCHEMA = @dbname 
                    AND TABLE_NAME = 'UserActiveLocks' 
                    AND CONSTRAINT_NAME = 'FK_UserActiveLocks_Checklists_ReportId');
                
                SET @sql = IF(@fkExists = 0, 
                    'ALTER TABLE UserActiveLocks ADD CONSTRAINT FK_UserActiveLocks_Checklists_ReportId FOREIGN KEY (ReportId) REFERENCES Checklists(Id) ON DELETE CASCADE',
                    'SELECT ""Foreign key already exists""');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This is intentionally empty to prevent accidental data loss
            // If you need to roll back, handle manually
            migrationBuilder.Sql("SELECT 'Rollback not supported - handle manually' as message;");
        }
    }
}