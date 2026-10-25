using System.Text.Json;
using DuckbillFisher.Plugin;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class PreferencesTests
{
    [Fact]
    public void EverySupportedSchemaRetainsExplicitPositiveSettings()
    {
        foreach (var version in Enumerable.Range(1, 19))
        {
            Assert.True(PreferencesMigration.Supports(version));
            var source = new CatalogSettings { FavoritesOnly = true, MinimumRemainingMinutes = 5,
                MinimumUpcomingMinutes = 10, MaximumUpcomingMinutes = 20, Regions = [10], Expansions = [0] };
            Assert.True(source.Matches(PreferencesMigration.Catalog(source, version)));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(18)]
    [InlineData(19)]
    public void MigratesDisabledUpperBoundOnlyForOlderSchemas(int version)
    {
        var source = new CatalogSettings { MinimumRemainingMinutes = 4, MinimumUpcomingMinutes = 0, MaximumUpcomingMinutes = 0,
            Regions = [3, 1, 3], Expansions = [2, 0, 2] };
        var actual = PreferencesMigration.Catalog(source, version);
        Assert.Equal(version < 19 ? null : (int?)0, actual.MaximumUpcomingMinutes);
        Assert.Equal(0, actual.MinimumUpcomingMinutes);
        Assert.Equal(4, actual.MinimumRemainingMinutes);
        Assert.Equal(new uint[] { 1, 3 }, actual.Regions);
        Assert.Equal(new uint[] { 0, 2 }, actual.Expansions);
        Assert.Equal(0, source.MaximumUpcomingMinutes);
        Assert.Equal(new uint[] { 3, 1, 3 }, source.Regions);
        Assert.True(actual.Matches(PreferencesMigration.Catalog(actual, 19)));
    }

    [Fact]
    public void PositiveBoundsAndConflictingRangeArePreserved()
    {
        var source = new CatalogSettings { MinimumUpcomingMinutes = 30, MaximumUpcomingMinutes = 10 };
        Assert.True(source.IsValid());
        Assert.True(source.Matches(PreferencesMigration.Catalog(source, 18)));
        var restored = JsonSerializer.Deserialize<CatalogSettings>(JsonSerializer.Serialize(source))!;
        Assert.True(source.Matches(restored));
        var empty = JsonSerializer.Deserialize<CatalogSettings>("{}")!;
        Assert.Null(empty.MinimumUpcomingMinutes);
        Assert.Null(empty.MaximumUpcomingMinutes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(43201)]
    public void OutOfRangeBoundsAreRejected(int minutes)
    {
        Assert.False((new CatalogSettings { MinimumRemainingMinutes = minutes }).IsValid());
        Assert.False((new CatalogSettings { MinimumUpcomingMinutes = minutes }).IsValid());
        Assert.False((new CatalogSettings { MaximumUpcomingMinutes = minutes }).IsValid());
    }

    [Theory]
    [InlineData(14, true)]
    [InlineData(15, false)]
    [InlineData(19, false)]
    public void CompactFavoritesAreInheritedOnce(int version, bool expected)
    {
        var compact = new CompactViewSettings { FavoritesOnly = false, UpcomingMinutes = 90 };
        var actual = PreferencesMigration.Compact(compact, new() { FavoritesOnly = true }, version);
        Assert.Equal(expected, actual.FavoritesOnly);
        Assert.Equal(90, actual.UpcomingMinutes);
        Assert.False(compact.FavoritesOnly);
    }

    [Theory]
    [InlineData(16, 6, 3)]
    [InlineData(17, 6, 6)]
    [InlineData(19, 12, 12)]
    [InlineData(16, 24, 24)]
    [InlineData(19, 2, 3)]
    public void ScheduleChangesOnlyOldDefaultOrInvalidValue(int version, int hours, int expected)
    {
        Assert.Equal(expected, PreferencesMigration.Schedule(new() { Hours = hours }, version).Hours);
        Assert.Equal(3, PreferencesMigration.Schedule(null, version).Hours);
    }

    [Fact]
    public void PresetRepairDoesNotDiscardOtherValidPresets()
    {
        var id = Guid.NewGuid();
        var valid = new CatalogPreset(id, "First", new() { MaximumUpcomingMinutes = 0 });
        var source = new[] { valid, valid with { Id = Guid.NewGuid(), Name = "first" },
            valid with { Name = "Another" }, valid with { Id = Guid.NewGuid(), Name = " bad " },
            valid with { Id = Guid.NewGuid(), Name = "Second", Settings = new() { MaximumUpcomingMinutes = 20 } } };
        var result = PreferencesMigration.Presets(source, 18);
        Assert.Equal(new[] { "First", "Second" }, result.Select(preset => preset.Name));
        Assert.Null(result[0].Settings.MaximumUpcomingMinutes);
        Assert.Equal(20, result[1].Settings.MaximumUpcomingMinutes);
        Assert.Equal(0, valid.Settings.MaximumUpcomingMinutes);
        Assert.Empty(PreferencesMigration.Presets(null, 19));
        Assert.Equal(20, CatalogPreset.ReadValid(Enumerable.Range(0, 30).Select(index =>
            new CatalogPreset(Guid.NewGuid(), $"Preset {index}", new()))).Count);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(19, true)]
    [InlineData(20, true)]
    [InlineData(24, true)]
    [InlineData(25, false)]
    public void UnsupportedSchemaIsNotMigrated(int version, bool expected) =>
        Assert.Equal(expected, PreferencesMigration.Supports(version));

    [Theory]
    [InlineData(80, 80)]
    [InlineData(100, 100)]
    [InlineData(200, 200)]
    [InlineData(0, 100)]
    [InlineData(123, 100)]
    public void InvalidScaleFallsBackToDefault(int value, int expected) =>
        Assert.Equal(expected, UiScaleSettings.Normalize(value));

    [Theory]
    [InlineData(null, "")]
    [InlineData("broken", "")]
    [InlineData("0.5.1.0", "0.5.1.0")]
    public void OptionalChangelogStateIsRepairedIndependently(string? value, string expected) =>
        Assert.Equal(expected, PreferencesMigration.SeenVersion(value));
}
