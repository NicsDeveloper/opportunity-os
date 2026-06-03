using OpportunityOS.Application.Discovery;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class FeedbackLearningServiceTests
{
    private readonly FeedbackLearningService _svc = new();
    private static readonly Guid CoA = Guid.NewGuid();
    private static readonly Guid CoB = Guid.NewGuid();

    [Fact]
    public void NoFeedback_NoPenalty_NoHide()
    {
        var model = _svc.Build(System.Array.Empty<DislikedJob>());
        var job = new JobSignal(CoA, new[] { ".NET" }, "Dev .NET", "");
        Assert.Equal(0, _svc.Penalty(model, job));
        Assert.False(_svc.ShouldHide(model, job));
    }

    [Fact]
    public void RepeatedlyRejectedCompany_IsHidden()
    {
        var disliked = new[]
        {
            new DislikedJob(CoA, new[] { "php" }, "Dev PHP", null),
            new DislikedJob(CoA, new[] { "php" }, "Dev PHP II", null),
            new DislikedJob(CoA, new[] { "php" }, "Dev PHP III", null),
        };
        var model = _svc.Build(disliked);
        // Same company, 3 rejections -> hard hide.
        Assert.True(_svc.ShouldHide(model, new JobSignal(CoA, new[] { ".NET" }, "Dev .NET", "")));
        // A different company is not hidden by this signal.
        Assert.False(_svc.ShouldHide(model, new JobSignal(CoB, new[] { ".NET" }, "Dev .NET", "")));
    }

    [Fact]
    public void LearnsFromReasonWords_PenalizesSimilarJobs()
    {
        // User rejects a job and types the reason "presencial".
        var model = _svc.Build(new[] { new DislikedJob(CoA, System.Array.Empty<string>(), "Dev .NET", "presencial") });
        var onsite = new JobSignal(CoB, new[] { ".NET" }, "Dev .NET", "Vaga presencial em SP");
        var remote = new JobSignal(CoB, new[] { ".NET" }, "Dev .NET", "100% remoto");
        Assert.True(_svc.Penalty(model, onsite) > 0);
        Assert.Equal(0, _svc.Penalty(model, remote));
    }

    [Fact]
    public void Reason_IsAccentInsensitive()
    {
        var model = _svc.Build(new[] { new DislikedJob(CoA, System.Array.Empty<string>(), null, "júnior") });
        // "junior" (no accent) in the candidate still matches the accented reason word.
        Assert.True(_svc.Penalty(model, new JobSignal(CoB, System.Array.Empty<string>(), "Vaga junior", "")) > 0);
    }

    [Fact]
    public void DislikedSkill_AddsPenalty()
    {
        var model = _svc.Build(new[] { new DislikedJob(CoA, new[] { "Delphi" }, "Dev Delphi", null) });
        var delphi = new JobSignal(CoB, new[] { "Delphi", "SQL" }, "Dev", "");
        var dotnet = new JobSignal(CoB, new[] { ".NET", "C#" }, "Dev", "");
        Assert.True(_svc.Penalty(model, delphi) > _svc.Penalty(model, dotnet));
    }
}
