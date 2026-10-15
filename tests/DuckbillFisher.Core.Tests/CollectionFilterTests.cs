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
    [InlineData(false, CollectionStatus.Unknown, false)]
    [InlineData(false, CollectionStatus.Uncaught, false)]
    [InlineData(false, CollectionStatus.Caught, false)]
    [InlineData(true, CollectionStatus.Unknown, false)]
    [InlineData(true, CollectionStatus.Uncaught, true)]
    [InlineData(true, CollectionStatus.Caught, false)]
    public void OnlyConfirmedUncaughtFavoritesNotify(bool favorite, CollectionStatus status, bool expected)
    {
        Assert.Equal(expected, CollectionFilter.ShouldNotify(favorite, status));
    }
}
