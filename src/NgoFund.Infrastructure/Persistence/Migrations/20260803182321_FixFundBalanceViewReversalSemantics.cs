using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fixes <c>vw_fund_balances</c>, which aggregated <c>fund_transactions</c> by
    /// <c>direction</c> alone with zero awareness of <c>reference_type</c>. A voided donation's
    /// <c>DonationReversal</c> Debit was being counted as a real disbursement in
    /// <c>total_utilized</c> (and its original Credit was never netted out of
    /// <c>total_collected</c>); symmetrically a voided payment's <c>PaymentReversal</c> Credit was
    /// inflating <c>total_collected</c> as if it were a fresh donation. <c>total_collected</c> and
    /// <c>total_utilized</c> now exclude/net the reversal rows so a void contributes net zero to
    /// both, using exclusion-based filters rather than enumerating reference types so a future
    /// reference type still falls into collected/utilized by direction alone. <c>balance</c> is
    /// unchanged — it stays the exhaustive, reference-type-blind <c>SUM(Credit) - SUM(Debit)</c>.
    /// </summary>
    public partial class FixFundBalanceViewReversalSemantics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_fund_balances;");

            migrationBuilder.Sql("""
                CREATE VIEW vw_fund_balances AS
                SELECT
                    fc.id AS fund_category_id,
                    fc.code,
                    fc.name,
                    fc.is_zakat,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit' AND ft.reference_type <> 'PaymentReversal'), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit' AND ft.reference_type = 'DonationReversal'), 0)
                    )::numeric(18,2) AS total_collected,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit' AND ft.reference_type <> 'DonationReversal'), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit' AND ft.reference_type = 'PaymentReversal'), 0)
                    )::numeric(18,2) AS total_utilized,
                    (COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit'), 0)
                     - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit'), 0)
                    )::numeric(18,2) AS balance
                FROM fund_categories fc
                LEFT JOIN fund_transactions ft ON ft.fund_category_id = fc.id
                GROUP BY fc.id, fc.code, fc.name, fc.is_zakat;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_fund_balances;");

            migrationBuilder.Sql("""
                CREATE VIEW vw_fund_balances AS
                SELECT
                    fc.id AS fund_category_id,
                    fc.code,
                    fc.name,
                    fc.is_zakat,
                    COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit'), 0) AS total_collected,
                    COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit'), 0) AS total_utilized,
                    COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Credit'), 0)
                        - COALESCE(SUM(ft.amount) FILTER (WHERE ft.direction = 'Debit'), 0) AS balance
                FROM fund_categories fc
                LEFT JOIN fund_transactions ft ON ft.fund_category_id = fc.id
                GROUP BY fc.id, fc.code, fc.name, fc.is_zakat;
                """);
        }
    }
}
