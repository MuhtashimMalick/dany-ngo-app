using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Donations;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

/// <summary>
/// Owns the only two ways money moves into the fund ledger from the donor side: recording a
/// donation (Credit) and voiding one (Debit reversal). Both happen in the same DB transaction as
/// the donation row itself and the number-sequence increment, so a failure never leaves the
/// ledger out of sync with the donation record.
/// </summary>
public class DonationService(AppDbContext dbContext, INumberGenerator numberGenerator, ICurrentUserService currentUser) : IDonationService
{
    public async Task<PagedResult<DonationDto>> GetDonationsAsync(PagedQuery query, Guid? donorId, CancellationToken cancellationToken)
    {
        var donationsQuery = dbContext.Donations
            .AsNoTracking()
            .Include(d => d.Donor)
            .Include(d => d.FundCategory)
            .AsQueryable();

        if (donorId is not null)
        {
            donationsQuery = donationsQuery.Where(d => d.DonorId == donorId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            donationsQuery = donationsQuery.Where(d =>
                EF.Functions.ILike(d.DonationNumber, $"%{term}%") ||
                EF.Functions.ILike(d.Donor.FullName, $"%{term}%") ||
                (d.ReceiptReference != null && EF.Functions.ILike(d.ReceiptReference, $"%{term}%")));
        }

        var totalCount = await donationsQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var entities = await donationsQuery
            .OrderByDescending(d => d.DonationDate)
            .ThenByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DonationDto>(entities.Select(Map).ToList(), totalCount, page, pageSize);
    }

    public async Task<DonationDto> GetDonationByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Donations
            .AsNoTracking()
            .Include(d => d.Donor)
            .Include(d => d.FundCategory)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Donation", id);

        return Map(entity);
    }

    public async Task<DonationDto> CreateAsync(CreateDonationRequest request, CancellationToken cancellationToken)
    {
        var donor = await dbContext.Donors.FindAsync([request.DonorId], cancellationToken)
            ?? throw new EntityNotFoundException("Donor", request.DonorId);

        var fund = await dbContext.FundCategories.FindAsync([request.FundCategoryId], cancellationToken)
            ?? throw new EntityNotFoundException("FundCategory", request.FundCategoryId);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var donationNumber = await numberGenerator.NextAsync("Donation", cancellationToken);

        var donation = new Donation
        {
            DonationNumber = donationNumber,
            DonorId = donor.Id,
            FundCategoryId = fund.Id,
            Amount = request.Amount,
            DonationDate = request.DonationDate,
            PaymentMethod = Enum.Parse<PaymentMethod>(request.PaymentMethod),
            ReceiptReference = request.ReceiptReference,
            BankName = request.BankName,
            InstrumentNumber = request.InstrumentNumber,
            Notes = request.Notes,
        };
        dbContext.Donations.Add(donation);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.FundTransactions.Add(new FundTransaction
        {
            FundCategoryId = fund.Id,
            Direction = TransactionDirection.Credit,
            Amount = donation.Amount,
            TransactionDate = donation.DonationDate,
            ReferenceType = TransactionReferenceType.Donation,
            ReferenceId = donation.Id,
            Description = $"Donation {donation.DonationNumber} from {donor.FullName}",
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        donation.Donor = donor;
        donation.FundCategory = fund;
        return Map(donation);
    }

    public async Task VoidAsync(Guid id, VoidDonationRequest request, CancellationToken cancellationToken)
    {
        var donation = await dbContext.Donations.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Donation", id);

        if (donation.Status == DonationStatus.Voided)
        {
            throw new AlreadyVoidedException("donation");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        donation.Status = DonationStatus.Voided;
        donation.VoidedAt = DateTimeOffset.UtcNow;
        donation.VoidedBy = currentUser.UserId;
        donation.VoidReason = request.Reason;

        dbContext.FundTransactions.Add(new FundTransaction
        {
            FundCategoryId = donation.FundCategoryId,
            Direction = TransactionDirection.Debit,
            Amount = donation.Amount,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReferenceType = TransactionReferenceType.DonationReversal,
            ReferenceId = donation.Id,
            Description = $"Void of donation {donation.DonationNumber}: {request.Reason}",
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static DonationDto Map(Donation d) => new(
        d.Id, d.DonationNumber, d.DonorId, d.Donor.FullName, d.FundCategoryId, d.FundCategory.Name,
        d.Amount, d.DonationDate, d.PaymentMethod.ToString(), d.ReceiptReference, d.BankName,
        d.InstrumentNumber, d.Notes, d.Status.ToString(), d.VoidedAt, d.VoidReason);
}
