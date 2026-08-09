using Microsoft.EntityFrameworkCore;
using NgoFund.Application.Abstractions;
using NgoFund.Contracts.Applicants;
using NgoFund.Contracts.Common;
using NgoFund.Domain.Entities;
using NgoFund.Domain.Enums;
using NgoFund.Domain.Exceptions;
using NgoFund.Infrastructure.Persistence;

namespace NgoFund.Infrastructure.Services;

public class ApplicantService(AppDbContext dbContext) : IApplicantService
{
    public async Task<PagedResult<ApplicantDto>> GetApplicantsAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        var applicantsQuery = dbContext.Applicants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            applicantsQuery = applicantsQuery.Where(a =>
                EF.Functions.ILike(a.FullName, pattern) ||
                EF.Functions.ILike(a.Cnic, pattern) ||
                (a.MembershipNumber != null && EF.Functions.ILike(a.MembershipNumber, pattern)) ||
                (a.Phone != null && EF.Functions.ILike(a.Phone, pattern)));
        }

        var totalCount = await applicantsQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var entities = await applicantsQuery
            .OrderBy(a => a.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ApplicantDto>(entities.Select(Map).ToList(), totalCount, page, pageSize);
    }

    public async Task<ApplicantDto> GetApplicantByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applicants.FindAsync([id], cancellationToken) ?? throw new EntityNotFoundException("Applicant", id);
        return Map(entity);
    }

    public async Task<ApplicantDto> CreateAsync(CreateApplicantRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueAsync(request.Cnic, request.MembershipNumber, existingApplicantId: null, cancellationToken);

        var entity = new Applicant
        {
            MembershipNumber = request.MembershipNumber,
            FullName = request.FullName,
            FatherOrHusbandName = request.FatherOrHusbandName,
            Cnic = request.Cnic,
            Gender = Enum.Parse<Gender>(request.Gender),
            DateOfBirth = request.DateOfBirth,
            MaritalStatus = request.MaritalStatus is null ? null : Enum.Parse<MaritalStatus>(request.MaritalStatus),
            Phone = request.Phone,
            AlternatePhone = request.AlternatePhone,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            District = request.District,
            Province = request.Province,
            Occupation = request.Occupation,
            MonthlyIncome = request.MonthlyIncome,
            DependentsCount = request.DependentsCount,
            HouseholdSize = request.HouseholdSize,
            Notes = request.Notes,
        };

        dbContext.Applicants.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<ApplicantDto> UpdateAsync(Guid id, UpdateApplicantRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applicants.FindAsync([id], cancellationToken) ?? throw new EntityNotFoundException("Applicant", id);

        await EnsureUniqueAsync(request.Cnic, request.MembershipNumber, existingApplicantId: id, cancellationToken);

        entity.MembershipNumber = request.MembershipNumber;
        entity.FullName = request.FullName;
        entity.FatherOrHusbandName = request.FatherOrHusbandName;
        entity.Cnic = request.Cnic;
        entity.Gender = Enum.Parse<Gender>(request.Gender);
        entity.DateOfBirth = request.DateOfBirth;
        entity.MaritalStatus = request.MaritalStatus is null ? null : Enum.Parse<MaritalStatus>(request.MaritalStatus);
        entity.Phone = request.Phone;
        entity.AlternatePhone = request.AlternatePhone;
        entity.Email = request.Email;
        entity.Address = request.Address;
        entity.City = request.City;
        entity.District = request.District;
        entity.Province = request.Province;
        entity.Occupation = request.Occupation;
        entity.MonthlyIncome = request.MonthlyIncome;
        entity.DependentsCount = request.DependentsCount;
        entity.HouseholdSize = request.HouseholdSize;
        entity.IsBlacklisted = request.IsBlacklisted;
        entity.BlacklistReason = request.BlacklistReason;
        entity.Notes = request.Notes;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applicants.FindAsync([id], cancellationToken) ?? throw new EntityNotFoundException("Applicant", id);

        // Removing a soft-deletable entity is converted into a soft delete by
        // AuditSaveChangesInterceptor — this is not a hard delete.
        dbContext.Applicants.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetProfilePhotoAsync(Guid applicantId, Guid documentId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Applicants.FindAsync([applicantId], cancellationToken) ?? throw new EntityNotFoundException("Applicant", applicantId);

        if (!await dbContext.Documents.AnyAsync(d => d.Id == documentId, cancellationToken))
        {
            throw new EntityNotFoundException("Document", documentId);
        }

        entity.PhotoDocumentId = documentId;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueAsync(string cnic, string? membershipNumber, Guid? existingApplicantId, CancellationToken cancellationToken)
    {
        if (await dbContext.Applicants.AnyAsync(a => a.Cnic == cnic && a.Id != existingApplicantId, cancellationToken))
        {
            throw new DuplicateFieldException("applicant", "CNIC", cnic);
        }

        if (membershipNumber is not null &&
            await dbContext.Applicants.AnyAsync(a => a.MembershipNumber == membershipNumber && a.Id != existingApplicantId, cancellationToken))
        {
            throw new DuplicateFieldException("applicant", "membership number", membershipNumber);
        }
    }

    private static ApplicantDto Map(Applicant a) => new(
        a.Id, a.MembershipNumber, a.FullName, a.FatherOrHusbandName, a.Cnic, a.Gender.ToString(),
        a.DateOfBirth, a.MaritalStatus?.ToString(), a.Phone, a.AlternatePhone, a.Email, a.Address,
        a.City, a.District, a.Province, a.Occupation, a.MonthlyIncome, a.DependentsCount, a.HouseholdSize,
        a.PhotoDocumentId, a.IsBlacklisted, a.BlacklistReason, a.Notes);
}
