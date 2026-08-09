using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Common;
using NgoFund.Contracts.Donors;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class DonorService(AppDbContext dbContext, INumberGenerator numberGenerator) : IDonorService
{
    public async Task<PagedResult<DonorDto>> GetDonorsAsync(PagedQuery query, bool? activeOnly, CancellationToken cancellationToken)
    {
        var donorsQuery = dbContext.Donors.AsNoTracking().AsQueryable();

        if (activeOnly is true)
        {
            donorsQuery = donorsQuery.Where(d => d.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            donorsQuery = donorsQuery.Where(d =>
                EF.Functions.ILike(d.FullName, pattern) ||
                EF.Functions.ILike(d.DonorCode, pattern) ||
                (d.Cnic != null && EF.Functions.ILike(d.Cnic, pattern)) ||
                (d.MembershipNumber != null && EF.Functions.ILike(d.MembershipNumber, pattern)) ||
                (d.Phone != null && EF.Functions.ILike(d.Phone, pattern)));
        }

        var totalCount = await donorsQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var entities = await donorsQuery
            .OrderBy(d => d.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DonorDto>(entities.Select(Map).ToList(), totalCount, page, pageSize);
    }

    public async Task<DonorDto> GetDonorByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Donors.FindAsync([id], cancellationToken) ?? throw new EntityNotFoundException("Donor", id);
        return Map(entity);
    }

    public async Task<DonorDto> CreateAsync(CreateDonorRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueFieldsAsync(request.Cnic, request.MembershipNumber, existingDonorId: null, cancellationToken);

        var donorCode = await numberGenerator.NextAsync("Donor", cancellationToken);

        var entity = new Donor
        {
            DonorCode = donorCode,
            FullName = request.FullName,
            DonorType = Enum.Parse<DonorType>(request.DonorType),
            Cnic = request.Cnic,
            Ntn = request.Ntn,
            MembershipNumber = request.MembershipNumber,
            Phone = request.Phone,
            AlternatePhone = request.AlternatePhone,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            IsAnonymous = request.IsAnonymous,
            IsActive = true,
            Notes = request.Notes,
        };

        dbContext.Donors.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<DonorDto> UpdateAsync(Guid id, UpdateDonorRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Donors.FindAsync([id], cancellationToken) ?? throw new EntityNotFoundException("Donor", id);

        await EnsureUniqueFieldsAsync(request.Cnic, request.MembershipNumber, existingDonorId: id, cancellationToken);

        entity.FullName = request.FullName;
        entity.DonorType = Enum.Parse<DonorType>(request.DonorType);
        entity.Cnic = request.Cnic;
        entity.Ntn = request.Ntn;
        entity.MembershipNumber = request.MembershipNumber;
        entity.Phone = request.Phone;
        entity.AlternatePhone = request.AlternatePhone;
        entity.Email = request.Email;
        entity.Address = request.Address;
        entity.City = request.City;
        entity.Country = request.Country;
        entity.IsAnonymous = request.IsAnonymous;
        entity.IsActive = request.IsActive;
        entity.Notes = request.Notes;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Donors.FindAsync([id], cancellationToken) ?? throw new EntityNotFoundException("Donor", id);
        entity.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueFieldsAsync(string? cnic, string? membershipNumber, Guid? existingDonorId, CancellationToken cancellationToken)
    {
        if (cnic is not null &&
            await dbContext.Donors.AnyAsync(d => d.Cnic == cnic && d.Id != existingDonorId, cancellationToken))
        {
            throw new DuplicateFieldException("donor", "CNIC", cnic);
        }

        if (membershipNumber is not null &&
            await dbContext.Donors.AnyAsync(d => d.MembershipNumber == membershipNumber && d.Id != existingDonorId, cancellationToken))
        {
            throw new DuplicateFieldException("donor", "membership number", membershipNumber);
        }
    }

    private static DonorDto Map(Donor d) => new(
        d.Id, d.DonorCode, d.FullName, d.DonorType.ToString(), d.Cnic, d.Ntn, d.MembershipNumber,
        d.Phone, d.AlternatePhone, d.Email, d.Address, d.City, d.Country, d.IsAnonymous, d.IsActive, d.Notes);
}
