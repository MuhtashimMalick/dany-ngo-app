using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestrictWritePermissionsToSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("16fdc1ce-737e-6b2d-8caf-48f83f8ebb6d"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("45d4081f-59fd-5c1a-fb09-a36736653e2e"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4c3276f3-5833-0236-b3db-7e99c5401c05"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5d159e8a-5133-d5a3-7923-1121a45ee583"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("6c8bdf71-5ecf-ee6a-571f-daee60d5e56b"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7021cb7f-0f43-95e5-5078-150bcfbaad27"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8d991300-4f0f-fe89-ae4a-1987455adea6"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d2da11b2-6782-d766-9e3e-abe9ee313a80"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("de5fa8b0-1b3c-5067-2f7c-99f2d3d9849e"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f3a723d1-0567-af8f-2532-45eee1f5c62c"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("16fdc1ce-737e-6b2d-8caf-48f83f8ebb6d"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1ea7ae4c-267b-2665-6d9c-95aef875ea82"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("270b3031-1aa9-3b41-9a31-af214c8a2ce8"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("30e1b3a4-9661-1641-f772-0702f3abfb76"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("3cacce04-c54e-46ba-d65a-d3a1dbbe5439"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("45d4081f-59fd-5c1a-fb09-a36736653e2e"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4c3276f3-5833-0236-b3db-7e99c5401c05"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("523b5077-a6c4-26a6-79ae-1f9f464aac90"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("548b3971-b4da-0e85-b75f-307e14ffc716"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5d159e8a-5133-d5a3-7923-1121a45ee583"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5d67ab96-8af5-7c16-8cfa-03f3dc2c60c8"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("6c8bdf71-5ecf-ee6a-571f-daee60d5e56b"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("6e500c0b-c113-2ebb-3898-187001f164c3"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7021cb7f-0f43-95e5-5078-150bcfbaad27"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8d991300-4f0f-fe89-ae4a-1987455adea6"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9c55f70e-3ba6-0bd3-26d0-264a21b2d7d1"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b57ecef7-0d27-e4c6-3424-fd0363294d43"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ce4b9565-0a40-b007-b000-805fa2c189fd"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d2da11b2-6782-d766-9e3e-abe9ee313a80"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("de5fa8b0-1b3c-5067-2f7c-99f2d3d9849e"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e30c9ea9-40b0-defe-c93c-2312754671f3"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f3a723d1-0567-af8f-2532-45eee1f5c62c"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1ea7ae4c-267b-2665-6d9c-95aef875ea82"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("3cacce04-c54e-46ba-d65a-d3a1dbbe5439"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("523b5077-a6c4-26a6-79ae-1f9f464aac90"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b57ecef7-0d27-e4c6-3424-fd0363294d43"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("1bcca810-9298-8bb5-862d-b870025e4704"),
                column: "description",
                value: "Read-only access to donor, donation, payment and financial report data.");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad"),
                column: "description",
                value: "Read-only access to operational data; cannot manage roles/permissions or system settings.");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c"),
                column: "description",
                value: "Read-only access to applicants and applications.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("16fdc1ce-737e-6b2d-8caf-48f83f8ebb6d"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("45d4081f-59fd-5c1a-fb09-a36736653e2e"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("4c3276f3-5833-0236-b3db-7e99c5401c05"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("5d159e8a-5133-d5a3-7923-1121a45ee583"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("6c8bdf71-5ecf-ee6a-571f-daee60d5e56b"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("7021cb7f-0f43-95e5-5078-150bcfbaad27"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("8d991300-4f0f-fe89-ae4a-1987455adea6"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("d2da11b2-6782-d766-9e3e-abe9ee313a80"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("de5fa8b0-1b3c-5067-2f7c-99f2d3d9849e"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("f3a723d1-0567-af8f-2532-45eee1f5c62c"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("16fdc1ce-737e-6b2d-8caf-48f83f8ebb6d"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("1ea7ae4c-267b-2665-6d9c-95aef875ea82"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("270b3031-1aa9-3b41-9a31-af214c8a2ce8"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("30e1b3a4-9661-1641-f772-0702f3abfb76"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("3cacce04-c54e-46ba-d65a-d3a1dbbe5439"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("45d4081f-59fd-5c1a-fb09-a36736653e2e"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("4c3276f3-5833-0236-b3db-7e99c5401c05"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("523b5077-a6c4-26a6-79ae-1f9f464aac90"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("548b3971-b4da-0e85-b75f-307e14ffc716"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("5d159e8a-5133-d5a3-7923-1121a45ee583"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("5d67ab96-8af5-7c16-8cfa-03f3dc2c60c8"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("6c8bdf71-5ecf-ee6a-571f-daee60d5e56b"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("6e500c0b-c113-2ebb-3898-187001f164c3"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("7021cb7f-0f43-95e5-5078-150bcfbaad27"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("8d991300-4f0f-fe89-ae4a-1987455adea6"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("9c55f70e-3ba6-0bd3-26d0-264a21b2d7d1"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("b57ecef7-0d27-e4c6-3424-fd0363294d43"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("ce4b9565-0a40-b007-b000-805fa2c189fd"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("d2da11b2-6782-d766-9e3e-abe9ee313a80"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("de5fa8b0-1b3c-5067-2f7c-99f2d3d9849e"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("e30c9ea9-40b0-defe-c93c-2312754671f3"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("f3a723d1-0567-af8f-2532-45eee1f5c62c"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("1ea7ae4c-267b-2665-6d9c-95aef875ea82"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("3cacce04-c54e-46ba-d65a-d3a1dbbe5439"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("523b5077-a6c4-26a6-79ae-1f9f464aac90"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("9e35778a-c8e4-5f19-4db3-77be6cb5bae4"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") },
                    { new Guid("b57ecef7-0d27-e4c6-3424-fd0363294d43"), new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c") }
                });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("1bcca810-9298-8bb5-862d-b870025e4704"),
                column: "description",
                value: "Manages donors, donations, payments and financial reports.");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad"),
                column: "description",
                value: "Full operational access; cannot manage roles/permissions or system settings.");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("b12a0841-4618-39f2-05e1-65b6ef22171c"),
                column: "description",
                value: "Creates and edits applicants and applications; no financial or approval access.");
        }
    }
}
