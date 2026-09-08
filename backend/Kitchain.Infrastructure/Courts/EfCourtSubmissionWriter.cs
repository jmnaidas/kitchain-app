using Kitchain.Application.Courts;
using Kitchain.Domain.Courts;
using Kitchain.Infrastructure.Persistence;

namespace Kitchain.Infrastructure.Courts;

public sealed class EfCourtSubmissionWriter(KitchainDbContext db) : ICourtSubmissionWriter
{
    public async Task AddAsync(CourtSubmission submission, CancellationToken cancellationToken)
    {
        db.Set<CourtSubmission>().Add(submission);
        await db.SaveChangesAsync(cancellationToken);
    }
}
