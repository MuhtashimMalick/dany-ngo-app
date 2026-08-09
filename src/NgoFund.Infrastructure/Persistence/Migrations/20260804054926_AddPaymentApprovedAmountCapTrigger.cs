using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Hand-written SQL (the EF Core fluent API cannot express triggers) — defence in depth,
    /// modelled on <c>fn_enforce_zakat_eligibility</c>/<c>trg_applications_enforce_zakat_eligibility</c>,
    /// for the payment invariant in  rule 4 (<c>SUM(completed payments) &lt;= approved_amount</c>).
    /// <see cref="Services.PaymentService"/> already enforces this in application code under a row
    /// lock; this trigger rejects it at the database level too, so a bug or a direct/raw write to
    /// <c>payments</c> can never overshoot an application's approved amount — including when
    /// <c>approved_amount</c> is <c>null</c>, the latent hole D3 also closes in application code.
    /// </summary>
    public partial class AddPaymentApprovedAmountCapTrigger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_enforce_payment_approved_amount_cap() RETURNS trigger AS $$
                DECLARE
                    v_approved_amount numeric(18,2);
                    v_total_completed numeric(18,2);
                BEGIN
                    -- Voided payments don't draw against the cap.
                    IF NEW.status <> 'Completed' THEN
                        RETURN NEW;
                    END IF;

                    SELECT approved_amount INTO v_approved_amount
                    FROM applications WHERE id = NEW.application_id;

                    IF v_approved_amount IS NULL THEN
                        RAISE EXCEPTION
                            'Application % has no approved amount set and cannot accept payments',
                            NEW.application_id
                            USING ERRCODE = 'check_violation';
                    END IF;

                    SELECT COALESCE(SUM(amount), 0) INTO v_total_completed
                    FROM payments
                    WHERE application_id = NEW.application_id
                      AND status = 'Completed'
                      AND id <> NEW.id;

                    v_total_completed := v_total_completed + NEW.amount;

                    IF v_total_completed > v_approved_amount THEN
                        RAISE EXCEPTION
                            'Payment of % would bring total completed payments for application % to %, exceeding the approved amount of %',
                            NEW.amount, NEW.application_id, v_total_completed, v_approved_amount
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_payments_enforce_approved_amount_cap
                BEFORE INSERT OR UPDATE OF amount, status, application_id ON payments
                FOR EACH ROW EXECUTE FUNCTION fn_enforce_payment_approved_amount_cap();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_payments_enforce_approved_amount_cap ON payments;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_enforce_payment_approved_amount_cap();");
        }
    }
}
