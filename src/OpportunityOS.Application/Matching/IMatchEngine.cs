using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Matching;

public interface IMatchEngine
{
    MatchResult Evaluate(CandidateProfile profile, JobPosting job);
}
