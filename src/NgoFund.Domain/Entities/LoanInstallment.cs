using NgoFund.Domain.Common;

namespace NgoFund.Domain.Entities;

/// <summary>
/// One scheduled installment line of a <see cref="LoanAgreement"/>. Per D6, this table stores
/// only the schedule (<see cref="AmountDue"/>, <see cref="DueDate"/>) — there is deliberately no
/// <c>amount_paid</c>/<c>status</c> column. What's been allocated and each line's derived status
/// are computed at read time by <see cref="Loans.LoanScheduleCalculator.Allocate"/>, never stored.
/// Never edited after creation — no <see cref="IUpdateAuditable"/>.
/// </summary>
public class LoanInstallment : BaseEntity
{
    public Guid LoanAgreementId { get; set; }
    public LoanAgreement LoanAgreement { get; set; } = null!;

    public int SequenceNumber { get; set; }

    public decimal AmountDue { get; set; }

    public DateOnly DueDate { get; set; }
}
