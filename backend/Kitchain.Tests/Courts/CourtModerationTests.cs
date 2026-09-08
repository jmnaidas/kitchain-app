using Kitchain.Domain.Courts;

namespace Kitchain.Tests.Courts;

public sealed class CourtModerationTests
{
    [Fact]
    public void Decisions_are_final_and_review_timestamps_cannot_move_backwards()
    {
        var submittedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var submission = new CourtSubmission(Guid.NewGuid(), "Venue", "Address", "City", 2,
            IndoorOutdoorType.Mixed, BookingMethod.WalkIn, submittedAt);
        Assert.Throws<ArgumentException>(() => submission.Approve(submittedAt.AddMinutes(-1)));
        Assert.Equal(CourtSubmissionStatus.Pending, submission.Status);
        submission.Reject(submittedAt.AddHours(1));
        Assert.Equal(CourtSubmissionStatus.Rejected, submission.Status);
        Assert.Equal(submittedAt.AddHours(1), submission.UpdatedAt);
        Assert.Throws<InvalidOperationException>(() => submission.Approve(submittedAt.AddHours(2)));
        Assert.Throws<InvalidOperationException>(() => submission.Reject(submittedAt.AddHours(2)));
    }
}
