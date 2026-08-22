using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryApplicationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_documents_exactly_one_owner",
                table: "documents");

            // NOTE: intentionally omitting a rescaffolded `ALTER TABLE fund_transactions ALTER
            // COLUMN reference_type TYPE character varying(30)` here. The column is already
            // varchar(30) in the live schema (set by the hand-written AddLoanTriggersAndViews
            // migration), but that migration's own Designer.cs snapshot was never refreshed to
            // reflect it, so `dotnet ef migrations add` keeps rescaffolding this as a spurious
            // diff. Re-running it fails with "cannot alter type of a column used by a view or
            // rule" against vw_fund_balances. Pre-existing repo drift, not introduced by this
            // migration — see docs/schema.md if this needs fixing at the source.

            migrationBuilder.AddColumn<Guid>(
                name: "application_guarantor_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "declaration_accepted_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "declared_business_address",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "declared_earning_members",
                table: "applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "declared_house_status",
                table: "applications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "declared_household_size",
                table: "applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "declared_monthly_income",
                table: "applications",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "declared_residential_address",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_form_reference",
                table: "applications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "intake_channel",
                table: "applications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "InApp");

            migrationBuilder.AddColumn<Guid>(
                name: "signature_document_id",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signed_by_name",
                table: "applications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "submitted_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "terms_accepted_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terms_version",
                table: "applications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "requires_guarantors",
                table: "application_categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "terms_text",
                table: "application_categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "terms_version",
                table: "application_categories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ancestral_village",
                table: "applicants",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "father_membership_number",
                table: "applicants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grandfather_name",
                table: "applicants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "surname",
                table: "applicants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whatsapp_number",
                table: "applicants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "application_guarantors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    membership_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    father_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    grandfather_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    surname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cnic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    residential_address = table.Column<string>(type: "text", nullable: true),
                    business_address = table.Column<string>(type: "text", nullable: true),
                    business_nature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone_home = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    phone_office = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    phone_mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    declaration_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    signed_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_guarantors", x => x.id);
                    table.CheckConstraint("ck_application_guarantors_sequence_positive", "sequence_no > 0");
                    table.ForeignKey(
                        name: "fk_application_guarantors_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "business_loan_application_details",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paper_form_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    business_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    education = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    skill = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    experience = table.Column<string>(type: "text", nullable: true),
                    other_income_sources = table.Column<string>(type: "text", nullable: true),
                    total_monthly_expenses = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    proposed_business_description = table.Column<string>(type: "text", nullable: false),
                    proposed_business_location = table.Column<string>(type: "text", nullable: true),
                    capital_required = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    capital_already_available = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    has_prior_business_experience = table.Column<bool>(type: "boolean", nullable: false),
                    prior_business_details = table.Column<string>(type: "text", nullable: true),
                    emergency_contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    emergency_contact_cnic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_loan_application_details", x => x.application_id);
                    table.CheckConstraint("ck_business_loan_details_capital_available_nonneg", "capital_already_available >= 0");
                    table.CheckConstraint("ck_business_loan_details_capital_required_nonneg", "capital_required >= 0");
                    table.CheckConstraint("ck_business_loan_details_expenses_nonneg", "total_monthly_expenses >= 0");
                    table.ForeignKey(
                        name: "fk_business_loan_application_details_applications_application_",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "housing_application_details",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_age = table.Column<int>(type: "integer", nullable: true),
                    current_house_value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    monthly_rent = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    advance_paid = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    years_at_current_address = table.Column<int>(type: "integer", nullable: true),
                    previous_residential_address = table.Column<string>(type: "text", nullable: true),
                    received_assistance_before = table.Column<bool>(type: "boolean", nullable: false),
                    previous_assistance_details = table.Column<string>(type: "text", nullable: true),
                    receives_marriage_assistance = table.Column<bool>(type: "boolean", nullable: false),
                    receives_education_assistance = table.Column<bool>(type: "boolean", nullable: false),
                    receives_medical_assistance = table.Column<bool>(type: "boolean", nullable: false),
                    receives_widow_assistance = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_housing_application_details", x => x.application_id);
                    table.ForeignKey(
                        name: "fk_housing_application_details_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "marriage_application_details",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_relationship_to_bride = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bride_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    bride_father_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bride_family_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bride_cnic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bride_marital_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bride_previous_husband_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    bride_jamaat = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    bride_prior_trust_assistance = table.Column<string>(type: "text", nullable: true),
                    groom_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    groom_father_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    groom_grandfather_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    groom_jamaat = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    groom_marital_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    groom_previous_wife_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    groom_address = table.Column<string>(type: "text", nullable: true),
                    groom_mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    groom_business_address = table.Column<string>(type: "text", nullable: true),
                    nikah_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rukhsati_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marriage_application_details", x => x.application_id);
                    table.CheckConstraint("ck_marriage_application_details_rukhsati_after_nikah", "rukhsati_date IS NULL OR nikah_date IS NULL OR rukhsati_date >= nikah_date");
                    table.ForeignKey(
                        name: "fk_marriage_application_details_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "application_categories",
                keyColumn: "id",
                keyValue: new Guid("20b2ef82-a783-0ce4-c3d7-b668207696b6"),
                columns: new[] { "requires_guarantors", "terms_text", "terms_version" },
                values: new object[] { 0, null, null });

            migrationBuilder.UpdateData(
                table: "application_categories",
                keyColumn: "id",
                keyValue: new Guid("2ccde3bf-3225-d02c-5590-cf448f4a0051"),
                columns: new[] { "requires_guarantors", "terms_text", "terms_version" },
                values: new object[] { 2, "شرائط و ضوابط\n\n1. آپ اپنے کاروبار کی تفصیل بتا کر ٹرسٹ سے قابلِ واپسی قرض لے سکتے ہیں۔ درخواست صحیح اور مکمل بھر کر ضروری دستاویزات کے ساتھ جمع کروائیں؛ نامکمل درخواست منظور نہیں کی جائے گی۔\nYou may apply for a repayable loan from the Trust by describing your business. The application must be complete and accurate, with required documents attached; incomplete applications will not be approved.\n\n2. قرض کی وصولی کی تاریخ سے ماہوار اقساط پیشگی چیک کے ذریعے وصول کی جائیں گی، اس لیے درخواست گزار کا بینک اکاؤنٹ ہونا لازمی ہے۔\nMonthly installments will be collected via post-dated cheque from the date of loan disbursement, so the applicant must have a bank account.\n\n3. درخواست گزار ہر تین ماہ بعد اپنے کاروبار کی تفصیلی رپورٹ خود کمیٹی کے روبرو پیش کرے گا۔\nThe applicant must present a detailed report of their business in person before the committee every three months.\n\n4. کاروبار کی ناکامی یا دھوکہ دہی کی صورت میں کمیٹی باقی رقم یکمشت وصول کرنے کا فیصلہ کر سکتی ہے۔\nIn case of business failure or fraud, the committee may decide to recover the remaining amount in a lump sum.\n\n5. اگر درخواست گزار نے بینک یا کسی اور ادارے سے قرض لیا ہے تو اس کی تفصیل درخواست میں درج کرنا لازمی ہے۔\nIf the applicant has an existing loan from a bank or any other institution, its details must be disclosed in the application.\n\n6. درخواست ایک مرتبہ مسترد ہونے کی صورت میں چھ ماہ تک دوبارہ جمع نہیں کروائی جا سکے گی۔\nA rejected application cannot be resubmitted for six months.\n\n7. دونوں ضامن افراد کا اے-زی ڈینی ویلفیئر ٹرسٹ کا ممبر اور کاروباری شخصیت ہونا لازمی ہے۔ درخواست گزار کسی وجہ سے ادائیگی میں ناکام ہو تو دونوں ضامن باقی رقم روزگار اسکیم کمیٹی کو واپس ادا کریں گے۔\nBoth guarantors must be A.Z Dany Welfare Trust members and business persons. If the applicant defaults for any reason, both guarantors will repay the remaining amount to the Rozgar Scheme Committee.\n\n8. تمام عہدے داران، مبلغ، ورکنگ کمیٹی اور نامزد اراکین درخواست فارم پر ضامن نہیں بن سکتے۔\nAll office bearers, missionaries, working committee members, and nominated members cannot act as guarantors.\n\n9. روزگار اسکیم کمیٹی کو یہ اختیار حاصل ہے کہ وہ کوئی وجہ بتائے بغیر کسی بھی درخواست کو منظور یا مسترد کر دے؛ اس کا فیصلہ حتمی اور آخری ہو گا۔\nThe Rozgar Scheme Committee has the authority to approve or reject any application without stating a reason; its decision is final.", "1.0" });

            migrationBuilder.UpdateData(
                table: "application_categories",
                keyColumn: "id",
                keyValue: new Guid("5ff221ba-81fd-4406-c688-a3c674de461f"),
                columns: new[] { "requires_guarantors", "terms_text", "terms_version" },
                values: new object[] { 0, "اہم ہدایات\n\n1. درخواست کے ساتھ جماعت کے ممبر شپ کارڈ کی فوٹو کاپی اور اصل شادی کارڈ منسلک کرنا ضروری ہے۔\nA photocopy of the Jamaat membership card and the original marriage card must be attached with the application.\n\n2. درخواست فارم صرف متعلقہ دولہا/دلہن کے نام سے وصول کیا جائے گا۔\nThe application form will only be accepted in the name of the concerned bride/groom.\n\n3. کمیٹی کو کسی بھی درخواست کی تفصیلی انکوائری کا حق حاصل ہے۔\nThe committee reserves the right to conduct a detailed inquiry into any application.\n\n4. فارم کے تمام اندراجات پُر کرنا ضروری ہے، نامکمل فارم مسترد کر دیا جائے گا۔\nAll entries in the form must be completed; an incomplete form will be rejected.\n\n5. غلط معلومات فراہم کرنے کی صورت میں فارم مسترد کر دیا جائے گا۔\nThe form will be rejected if incorrect information is provided.\n\n6. دلہن کے شناختی کارڈ یا فارم \"ب\" کی کاپی لازمی طور پر منسلک کریں، ورنہ فارم مسترد کر دیا جائے گا۔\nA copy of the bride's CNIC or Form-B must be attached, otherwise the form will be rejected.\n\n7. درخواست فارم شادی سے کم از کم ایک ماہ قبل جمع کروانا لازمی ہے، جس کے ساتھ اصل شادی کارڈ، شناختی کارڈ (دولہا/دلہن) کی کاپی، اور جماعت کی ممبر شپ کی کاپی منسلک کریں۔\nThe application must be submitted at least one month before the wedding, together with the original marriage card, a copy of CNIC (groom/bride), and a copy of the Jamaat membership card.", "1.0" });

            migrationBuilder.UpdateData(
                table: "application_categories",
                keyColumn: "id",
                keyValue: new Guid("67769b37-5a89-ca89-35c3-83b71b644283"),
                columns: new[] { "requires_guarantors", "terms_text", "terms_version" },
                values: new object[] { 0, null, null });

            migrationBuilder.UpdateData(
                table: "application_categories",
                keyColumn: "id",
                keyValue: new Guid("9ee8c9a2-70f3-538e-f223-f669aa195adf"),
                columns: new[] { "requires_guarantors", "terms_text", "terms_version" },
                values: new object[] { 0, null, null });

            migrationBuilder.UpdateData(
                table: "application_categories",
                keyColumn: "id",
                keyValue: new Guid("dc44b36f-32d6-7a7c-4e60-48e145f0e774"),
                columns: new[] { "requires_guarantors", "terms_text", "terms_version" },
                values: new object[] { 0, "مکان مدد کے قوانین و ضوابط\n\n1. مکان مدد کی درخواست کی ادائیگی زکوٰۃ فنڈ سے کی جائے گی۔\nPayment for housing assistance will be made from the Zakat Fund.\n\n2. درخواست گزار کا جماعت کا فعال ممبر ہونا ضروری ہے۔\nThe applicant must be an active Jamaat member.\n\n3. درخواست کے ساتھ قومی شناختی کارڈ اور جماعت کے ممبر شپ کارڈ کی کاپیاں منسلک کرنا لازمی ہے۔\nCopies of CNIC and Jamaat membership card must be attached with the application.\n\n4. عورت اپنے شوہر کی موجودگی میں اپنے شوہر کے نام سے درخواست جمع کروانے کی پابند ہو گی۔\nA woman must submit the application in her husband's name and in his presence.\n\n5. درخواست کے ساتھ گزشتہ تین ماہ کے بلوں (بجلی، گیس، فون، پانی) اور کرایہ کی رسیدوں کی فوٹو کاپیاں منسلک کرنا لازمی ہے۔\nPhotocopies of the last three months' utility bills (electricity, gas, phone, water) and rent receipts must be attached.\n\n6. مکان مدد کمیٹی جب بھی درخواست گزار کو بلائے، اسے حاضر ہو کر تمام تفصیلات سے آگاہ کرنا ہو گا؛ ضرورت پڑنے پر موجودہ رہائش کا معائنہ اور جانچ پڑتال کروانے کا بھی پابند ہو گا۔\nWhenever called by the Housing Assistance Committee, the applicant must appear and provide full details, and must allow inspection of the current residence if required.\n\n7. مکان کا کرایہ، بجلی، گیس، پانی، ٹیکس اور دیگر اخراجات وقت پر ادا کرنے کا پابند ہو گا۔\nThe applicant must pay house rent, electricity, gas, water, tax and other expenses on time.\n\n8. درخواست منظور یا مسترد کرنے کا مکمل اختیار مجلسِ عامہ کے پاس ہے، درخواست گزار کسی قسم کی مداخلت نہیں کرے گا۔\nFull authority to approve or reject the application rests with the General Council; the applicant will not interfere in any way.\n\n9. نامکمل یا غلط بیانی کی صورت میں درخواست فارم مسترد ہو جائے گا۔\nAn incomplete application or false statement will result in rejection of the form.", "1.0" });

            migrationBuilder.UpdateData(
                table: "application_categories",
                keyColumn: "id",
                keyValue: new Guid("e9c9608a-c26e-2132-b11d-ef39ff4f3000"),
                columns: new[] { "requires_guarantors", "terms_text", "terms_version" },
                values: new object[] { 0, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_documents_application_guarantor_id",
                table: "documents",
                column: "application_guarantor_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_documents_exactly_one_owner",
                table: "documents",
                sql: "num_nonnulls(applicant_id, application_id, donation_id, payment_id, application_guarantor_id) = 1");

            migrationBuilder.CreateIndex(
                name: "ix_applications_external_form_reference",
                table: "applications",
                column: "external_form_reference",
                unique: true,
                filter: "external_form_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_applications_signature_document_id",
                table: "applications",
                column: "signature_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_guarantors_application_id_sequence_no",
                table: "application_guarantors",
                columns: new[] { "application_id", "sequence_no" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_application_guarantors_cnic",
                table: "application_guarantors",
                column: "cnic");

            migrationBuilder.CreateIndex(
                name: "ix_marriage_application_details_bride_cnic",
                table: "marriage_application_details",
                column: "bride_cnic");

            migrationBuilder.AddForeignKey(
                name: "fk_applications_documents_signature_document_id",
                table: "applications",
                column: "signature_document_id",
                principalTable: "documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_documents_application_guarantors_application_guarantor_id",
                table: "documents",
                column: "application_guarantor_id",
                principalTable: "application_guarantors",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_applications_documents_signature_document_id",
                table: "applications");

            migrationBuilder.DropForeignKey(
                name: "fk_documents_application_guarantors_application_guarantor_id",
                table: "documents");

            migrationBuilder.DropTable(
                name: "application_guarantors");

            migrationBuilder.DropTable(
                name: "business_loan_application_details");

            migrationBuilder.DropTable(
                name: "housing_application_details");

            migrationBuilder.DropTable(
                name: "marriage_application_details");

            migrationBuilder.DropIndex(
                name: "ix_documents_application_guarantor_id",
                table: "documents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_documents_exactly_one_owner",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_applications_external_form_reference",
                table: "applications");

            migrationBuilder.DropIndex(
                name: "ix_applications_signature_document_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "application_guarantor_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "declaration_accepted_at",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "declared_business_address",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "declared_earning_members",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "declared_house_status",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "declared_household_size",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "declared_monthly_income",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "declared_residential_address",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "external_form_reference",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "intake_channel",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "signature_document_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "signed_by_name",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "terms_accepted_at",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "terms_version",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "requires_guarantors",
                table: "application_categories");

            migrationBuilder.DropColumn(
                name: "terms_text",
                table: "application_categories");

            migrationBuilder.DropColumn(
                name: "terms_version",
                table: "application_categories");

            migrationBuilder.DropColumn(
                name: "ancestral_village",
                table: "applicants");

            migrationBuilder.DropColumn(
                name: "father_membership_number",
                table: "applicants");

            migrationBuilder.DropColumn(
                name: "grandfather_name",
                table: "applicants");

            migrationBuilder.DropColumn(
                name: "surname",
                table: "applicants");

            migrationBuilder.DropColumn(
                name: "whatsapp_number",
                table: "applicants");

            // See matching NOTE in Up() — reference_type intentionally stays varchar(30) here too;
            // narrowing it back would hit the same view-dependency failure and would be wrong
            // regardless, since AddLoanTriggersAndViews widened it permanently for later migrations.

            migrationBuilder.AddCheckConstraint(
                name: "ck_documents_exactly_one_owner",
                table: "documents",
                sql: "num_nonnulls(applicant_id, application_id, donation_id, payment_id) = 1");
        }
    }
}
