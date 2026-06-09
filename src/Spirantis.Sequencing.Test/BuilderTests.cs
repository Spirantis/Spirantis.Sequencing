namespace Spirantis.Sequencing.Test;

/// <summary>Tests for builder-level behavior independent of any single sequence run.</summary>
public class BuilderTests
{
    [Fact]
    public void Build_WithoutAnyFunction_Throws()
    {
        var builder = SequenceBuilder.Create<DefaultSequenceContext, TestSequenceData>();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
}
