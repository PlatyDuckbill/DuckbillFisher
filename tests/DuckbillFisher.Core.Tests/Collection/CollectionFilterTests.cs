using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class CollectionFilterTests
{
    [Theory]
    [InlineData(CollectionStatus.Unknown)]
    [InlineData(CollectionStatus.Uncaught)]
    [InlineData(CollectionStatus.Caught)]
    public void ClearingOverrideRestoresObservedState(CollectionStatus observed)
    {
        Assert.Equal(CollectionStatus.Caught, CollectionFilter.Resolve(observed, true));
        Assert.Equal(CollectionStatus.Uncaught, CollectionFilter.Resolve(observed, false));
        Assert.Equal(observed, CollectionFilter.Resolve(observed, null));
    }

    [Theory]
    [InlineData(CollectionStatus.Unknown, false)]
    [InlineData(CollectionStatus.Uncaught, true)]
    [InlineData(CollectionStatus.Caught, false)]
    public void UncaughtOnlyRequiresConfirmedCollectionState(CollectionStatus status, bool expected)
    {
        Assert.Equal(expected, CollectionFilter.Matches(status, CollectionVisibility.UncaughtOnly));
    }
}
