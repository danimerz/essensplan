using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Essensplan.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiUserSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent SQL – safe to re-run if migration partially applied before
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_WeekPlans_StartDate` ON `WeekPlans`");
            migrationBuilder.Sql("ALTER TABLE `WeekPlans` ADD COLUMN IF NOT EXISTS `HouseholdId` int NOT NULL DEFAULT 0");
            migrationBuilder.Sql("ALTER TABLE `Recipes`   ADD COLUMN IF NOT EXISTS `HouseholdId` int NOT NULL DEFAULT 0");
            migrationBuilder.Sql("ALTER TABLE `Menus`     ADD COLUMN IF NOT EXISTS `HouseholdId` int NOT NULL DEFAULT 0");

            // ---- Tables (CREATE TABLE IF NOT EXISTS) ----
            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `AspNetRoles` (
                    `Id` varchar(255) NOT NULL,
                    `Name` varchar(256) NULL,
                    `NormalizedName` varchar(256) NULL,
                    `ConcurrencyStamp` longtext NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `AspNetUsers` (
                    `Id` varchar(255) NOT NULL,
                    `DisplayName` longtext NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `UserName` varchar(256) NULL,
                    `NormalizedUserName` varchar(256) NULL,
                    `Email` varchar(256) NULL,
                    `NormalizedEmail` varchar(256) NULL,
                    `EmailConfirmed` tinyint(1) NOT NULL,
                    `PasswordHash` longtext NULL,
                    `SecurityStamp` longtext NULL,
                    `ConcurrencyStamp` longtext NULL,
                    `PhoneNumber` longtext NULL,
                    `PhoneNumberConfirmed` tinyint(1) NOT NULL,
                    `TwoFactorEnabled` tinyint(1) NOT NULL,
                    `LockoutEnd` datetime(6) NULL,
                    `LockoutEnabled` tinyint(1) NOT NULL,
                    `AccessFailedCount` int NOT NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `Households` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Name` longtext NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `AspNetRoleClaims` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `RoleId` varchar(255) NOT NULL,
                    `ClaimType` longtext NULL,
                    `ClaimValue` longtext NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_AspNetRoleClaims_AspNetRoles_RoleId`
                        FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `AspNetUserClaims` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `UserId` varchar(255) NOT NULL,
                    `ClaimType` longtext NULL,
                    `ClaimValue` longtext NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_AspNetUserClaims_AspNetUsers_UserId`
                        FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `AspNetUserLogins` (
                    `LoginProvider` varchar(255) NOT NULL,
                    `ProviderKey` varchar(255) NOT NULL,
                    `ProviderDisplayName` longtext NULL,
                    `UserId` varchar(255) NOT NULL,
                    PRIMARY KEY (`LoginProvider`, `ProviderKey`),
                    CONSTRAINT `FK_AspNetUserLogins_AspNetUsers_UserId`
                        FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `AspNetUserRoles` (
                    `UserId` varchar(255) NOT NULL,
                    `RoleId` varchar(255) NOT NULL,
                    PRIMARY KEY (`UserId`, `RoleId`),
                    CONSTRAINT `FK_AspNetUserRoles_AspNetRoles_RoleId`
                        FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_AspNetUserRoles_AspNetUsers_UserId`
                        FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `AspNetUserTokens` (
                    `UserId` varchar(255) NOT NULL,
                    `LoginProvider` varchar(255) NOT NULL,
                    `Name` varchar(255) NOT NULL,
                    `Value` longtext NULL,
                    PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
                    CONSTRAINT `FK_AspNetUserTokens_AspNetUsers_UserId`
                        FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `RecipeRatings` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `RecipeId` int NOT NULL,
                    `UserId` varchar(255) NOT NULL,
                    `HouseholdId` int NOT NULL,
                    `Stars` int NOT NULL,
                    `Comment` varchar(1000) NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_RecipeRatings_AspNetUsers_UserId`
                        FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_RecipeRatings_Recipes_RecipeId`
                        FOREIGN KEY (`RecipeId`) REFERENCES `Recipes` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4");

            migrationBuilder.Sql(
                @"CREATE TABLE IF NOT EXISTS `HouseholdMemberships` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `HouseholdId` int NOT NULL,
                    `UserId` varchar(255) NOT NULL,
                    `Role` int NOT NULL,
                    `JoinedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_HouseholdMemberships_AspNetUsers_UserId`
                        FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_HouseholdMemberships_Households_HouseholdId`
                        FOREIGN KEY (`HouseholdId`) REFERENCES `Households` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4");

            // ---- Daten-Migration: bestehende Daten dem Standard-Haushalt zuweisen ----
            migrationBuilder.Sql("INSERT IGNORE INTO `Households` (`Name`, `CreatedAt`) VALUES ('Familie Merz', UTC_TIMESTAMP())");
            migrationBuilder.Sql("UPDATE `Menus`     SET `HouseholdId` = 1 WHERE `HouseholdId` = 0");
            migrationBuilder.Sql("UPDATE `Recipes`   SET `HouseholdId` = 1 WHERE `HouseholdId` = 0");
            migrationBuilder.Sql("UPDATE `WeekPlans` SET `HouseholdId` = 1 WHERE `HouseholdId` = 0");

            // ---- FK-Constraints auf bestehenden Tabellen ----
            migrationBuilder.AddForeignKey(
                name: "FK_Menus_Households_HouseholdId",
                table: "Menus",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Households_HouseholdId",
                table: "Recipes",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WeekPlans_Households_HouseholdId",
                table: "WeekPlans",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ---- Indexes (IF NOT EXISTS) ----
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_WeekPlans_HouseholdId_StartDate` ON `WeekPlans` (`HouseholdId`, `StartDate`)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_Recipes_HouseholdId` ON `Recipes` (`HouseholdId`)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_Menus_HouseholdId` ON `Menus` (`HouseholdId`)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_AspNetRoleClaims_RoleId` ON `AspNetRoleClaims` (`RoleId`)");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `RoleNameIndex` ON `AspNetRoles` (`NormalizedName`)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_AspNetUserClaims_UserId` ON `AspNetUserClaims` (`UserId`)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_AspNetUserLogins_UserId` ON `AspNetUserLogins` (`UserId`)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_AspNetUserRoles_RoleId` ON `AspNetUserRoles` (`RoleId`)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `EmailIndex` ON `AspNetUsers` (`NormalizedEmail`)");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `UserNameIndex` ON `AspNetUsers` (`NormalizedUserName`)");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_HouseholdMemberships_HouseholdId_UserId` ON `HouseholdMemberships` (`HouseholdId`, `UserId`)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_HouseholdMemberships_UserId` ON `HouseholdMemberships` (`UserId`)");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_RecipeRatings_RecipeId_UserId` ON `RecipeRatings` (`RecipeId`, `UserId`)");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_RecipeRatings_UserId` ON `RecipeRatings` (`UserId`)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Menus_Households_HouseholdId",
                table: "Menus");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Households_HouseholdId",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_WeekPlans_Households_HouseholdId",
                table: "WeekPlans");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "HouseholdMemberships");

            migrationBuilder.DropTable(
                name: "RecipeRatings");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Households");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_WeekPlans_HouseholdId_StartDate",
                table: "WeekPlans");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_HouseholdId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Menus_HouseholdId",
                table: "Menus");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "WeekPlans");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Menus");

            migrationBuilder.CreateIndex(
                name: "IX_WeekPlans_StartDate",
                table: "WeekPlans",
                column: "StartDate",
                unique: true);
        }
    }
}
