using System.Data;
using Kitchain.Application.Courts;
using Kitchain.Domain.Courts;
using Kitchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kitchain.Infrastructure.Courts;

public sealed class EfCourtSubmissionModeration(KitchainDbContext db) : ICourtSubmissionModeration
{
    public async Task<IReadOnlyList<CourtSubmissionSummary>> ListAsync(CourtSubmissionStatus status, CancellationToken cancellationToken) =>
        await db.Set<CourtSubmission>().AsNoTracking().Where(s => s.Status == status)
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Select(s => new CourtSubmissionSummary(s.Id, s.Name, s.City, s.Address, s.NumberOfCourts,
                s.IndoorOutdoor, s.BookingMethod, s.Status, s.SubmittedAt))
            .ToListAsync(cancellationToken);

    public Task<CourtSubmissionReview?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<CourtSubmission>().AsNoTracking().AsSingleQuery().Where(s => s.Id == id)
            .Select(s => new CourtSubmissionReview(s.Id, s.Name, s.Address, s.City, s.Region,
                s.Latitude, s.Longitude, s.NumberOfCourts, s.IndoorOutdoor, s.Surface, s.OpeningHours,
                s.StartingPrice, s.CurrencyCode, s.PriceUnit,
                s.Amenities.OrderBy(a => a.AmenityCode).Select(a => a.AmenityCode).ToList(),
                s.Phone, s.WebsiteUrl, s.SocialUrl, s.BookingUrl, s.BookingMethod,
                s.Status, s.SubmittedAt, s.UpdatedAt,
                s.Photos.OrderByDescending(p => p.IsPrimary).ThenBy(p => p.DisplayOrder).ThenBy(p => p.Id)
                    .Select(p => new CourtSubmissionPhotoReview(p.Id, p.ImageUrl, p.AltText, p.DisplayOrder,
                        p.IsPrimary, p.CreatedAt)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CourtModerationResult> DecideAsync(Guid id, CourtSubmissionDecision decision, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision));
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Both decisions take the same PostgreSQL row lock. A competing request waits and then
        // sees the committed status, preventing duplicate publication without a schema change.
        var rows = await db.Set<CourtSubmission>()
            .FromSqlInterpolated($"SELECT * FROM \"CourtSubmissions\" WHERE \"Id\" = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var submission = rows.SingleOrDefault();
        if (submission is null) return new(CourtModerationOutcome.NotFound);
        if (submission.Status != CourtSubmissionStatus.Pending) return new(CourtModerationOutcome.Conflict);

        Court? court = null;
        var now = DateTimeOffset.UtcNow;
        if (now < submission.UpdatedAt) now = submission.UpdatedAt;
        if (decision == CourtSubmissionDecision.Approve)
        {
            await db.Entry(submission).Collection(s => s.Amenities).LoadAsync(cancellationToken);
            await db.Entry(submission).Collection(s => s.Photos).LoadAsync(cancellationToken);
            try
            {
                court = submission.Approve(now);
            }
            catch (ArgumentException)
            {
                return new(CourtModerationOutcome.Invalid);
            }
            db.Courts.Add(court);
        }
        else submission.Reject(now);

        // Venue, children and submission status share one save and one explicit transaction.
        // Any exception or cancellation before commit disposes the transaction and rolls it back.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(CourtModerationOutcome.Success, new(submission.Id, court?.Id, submission.Status, submission.UpdatedAt));
    }
}
