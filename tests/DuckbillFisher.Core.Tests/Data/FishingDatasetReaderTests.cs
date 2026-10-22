using System.Text.Json.Nodes;
using DuckbillFisher.Core;
using Xunit;

namespace DuckbillFisher.Core.Tests;

public sealed class FishingDatasetReaderTests
{
    private const string Valid = """
        {
          "schemaVersion":2,"region":"KR","gameBuild":"test-build","dataVersion":"test-v2",
          "sourceRepository":"https://example.invalid/source","sourceRevision":"0123456789012345678901234567890123456789",
          "fish":[{
            "itemId":1,"fishParameterId":2,"fishingSpotId":3,"sourceName":"synthetic",
            "startHour":0,"endHour":24,"weatherIds":[],"previousWeatherIds":[],
            "baitPath":[4],"baitSteps":[[4]],"hookset":"","tug":"","predators":[],
            "intuitionSeconds":null,"folkloreRequired":false,"fishEyesSupported":null,
            "conditionsKnown":true,"snaggingRequired":false,"lure":"","bigFish":true,"patch":7
          }],"spearfishing":[]
        }
        """;
    private static JsonObject Document() => JsonNode.Parse(Valid)!.AsObject();
    private static JsonObject Fish(JsonObject document) => document["fish"]![0]!.AsObject();
    private static void Reject(JsonObject document, FishingDataError code, string path)
    {
        var exception = Assert.Throws<FishingDataValidationException>(() => FishingDatasetReader.ReadConditions(document.ToJsonString()));
        Assert.Equal(new FishingDataIssue(code, path), exception.Issue);
    }

    [Fact]
    public void NullableUnknownFactsSurviveReading()
    {
        var fish = Assert.Single(FishingDatasetReader.ReadConditions(Valid).Fish);
        Assert.Null(fish.IntuitionSeconds);
        Assert.Null(fish.FishEyesSupported);
        Assert.Empty(fish.Predators);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("sourceRevision")]
    [InlineData("fish")]
    [InlineData("spearfishing")]
    public void MissingRootFieldsAreNotDefaulted(string field)
    {
        var document = Document();
        document.Remove(field);
        Reject(document, FishingDataError.MissingField, "$." + field);
    }

    [Theory]
    [InlineData("conditionsKnown")]
    [InlineData("fishEyesSupported")]
    [InlineData("intuitionSeconds")]
    [InlineData("weatherIds")]
    public void MissingFishFactsStayErrorsEvenWhenNullable(string field)
    {
        var document = Document();
        Fish(document).Remove(field);
        Reject(document, FishingDataError.MissingField, "$.fish[0]." + field);
    }

    [Theory]
    [InlineData("schemaVersion", "3", FishingDataError.UnsupportedSchema, "$.schemaVersion")]
    [InlineData("region", "\"EN\"", FishingDataError.InvalidValue, "$.region")]
    [InlineData("sourceRevision", "\"main\"", FishingDataError.InvalidValue, "$.sourceRepository/sourceRevision")]
    public void InvalidMetadataHasStableErrorCodes(string field, string json, FishingDataError code, string path)
    {
        var document = Document();
        document[field] = JsonNode.Parse(json);
        Reject(document, code, path);
    }

    [Theory]
    [InlineData("itemId", "0", "$.fish[0]")]
    [InlineData("fishingSpotId", "0", "$.fish[0].fishingSpotId")]
    [InlineData("endHour", "0", "$.fish[0].startHour/endHour")]
    [InlineData("weatherIds", "[1,1]", "$.fish[0].weatherIds/previousWeatherIds")]
    [InlineData("previousWeatherIds", "[0]", "$.fish[0].weatherIds/previousWeatherIds")]
    [InlineData("predators", "[{\"itemId\":1,\"count\":1}]", "$.fish[0].predators")]
    [InlineData("predators", "[{\"itemId\":9,\"count\":0}]", "$.fish[0].predators")]
    [InlineData("intuitionSeconds", "175", "$.fish[0].intuitionSeconds")]
    [InlineData("hookset", "\"invented\"", "$.fish[0].hookset/tug/lure")]
    public void InvalidFactsAreRejectedWithoutInventingValues(string field, string json, string path)
    {
        var document = Document();
        Fish(document)[field] = JsonNode.Parse(json);
        Reject(document, FishingDataError.InvalidValue, path);
    }

    [Fact]
    public void DuplicateIdentitiesAndInconsistentBaitPathsAreRejected()
    {
        var document = Document();
        document["fish"]!.AsArray().Add(Fish(document).DeepClone());
        Reject(document, FishingDataError.DuplicateIdentity, "$.fish[1]");
        document = Document();
        Fish(document)["baitPath"] = JsonNode.Parse("[5]");
        Reject(document, FishingDataError.InconsistentReference, "$.fish[0].baitSteps/baitPath");
    }

    [Fact]
    public void DuplicateJsonFieldsAndMalformedJsonAreRejected()
    {
        var duplicate = Valid.Replace("\"itemId\":1", "\"itemId\":1,\"itemId\":2");
        var error = Assert.Throws<FishingDataValidationException>(() => FishingDatasetReader.ReadConditions(duplicate));
        Assert.Equal(new FishingDataIssue(FishingDataError.DuplicateField, "$.fish[0].itemId"), error.Issue);
        Assert.Equal(FishingDataError.InvalidJson,
            Assert.Throws<FishingDataValidationException>(() => FishingDatasetReader.ReadConditions("{")).Issue.Code);
    }

    [Fact]
    public void UnknownConditionsMayOmitSpotButCannotBecomeConfirmed()
    {
        var document = Document();
        Fish(document)["conditionsKnown"] = false;
        Fish(document)["fishingSpotId"] = 0;
        var fish = Assert.Single(FishingDatasetReader.ReadConditions(document.ToJsonString()).Fish);
        Assert.False(fish.ConditionsKnown);
        Assert.Equal(0u, fish.FishingSpotId);
    }

    private static JsonObject Grades(string hash) => new()
    {
        ["schemaVersion"] = 1, ["gameBuild"] = "test-build", ["dataVersion"] = "test-v2-grades",
        ["conditionDataSha256"] = hash,
        ["entries"] = new JsonArray(new JsonObject { ["fishParameterId"] = 2, ["itemId"] = 1, ["grade"] = "Big" }),
    };

    [Fact]
    public void GradesAreBoundToExactConditionContentAndIdentities()
    {
        var conditions = FishingDatasetReader.ReadConditions(Valid);
        var hash = FishingDatasetReader.ContentHash(Valid);
        var grades = Grades(hash);
        Assert.Equal(FishGrade.Big, Assert.Single(FishingDatasetReader.ReadGrades(grades.ToJsonString(), conditions, hash).Entries).Grade);
        grades["conditionDataSha256"] = new string('A', 64);
        Assert.Equal(FishingDataError.SourceMismatch, Assert.Throws<FishingDataValidationException>(() =>
            FishingDatasetReader.ReadGrades(grades.ToJsonString(), conditions, hash)).Issue.Code);
        grades = Grades(hash);
        grades["entries"]![0]!["itemId"] = 9;
        Assert.Equal(FishingDataError.InconsistentReference, Assert.Throws<FishingDataValidationException>(() =>
            FishingDatasetReader.ReadGrades(grades.ToJsonString(), conditions, hash)).Issue.Code);
    }

    [Theory]
    [InlineData("\"Regular\"")]
    [InlineData("\"Unknown\"")]
    [InlineData("2")]
    public void PositiveGradeDatasetCannotSilentlyDowngradeEntries(string grade)
    {
        var conditions = FishingDatasetReader.ReadConditions(Valid);
        var hash = FishingDatasetReader.ContentHash(Valid);
        var grades = Grades(hash);
        grades["entries"]![0]!["grade"] = JsonNode.Parse(grade);
        Assert.Equal(FishingDataError.InvalidValue, Assert.Throws<FishingDataValidationException>(() =>
            FishingDatasetReader.ReadGrades(grades.ToJsonString(), conditions, hash)).Issue.Code);
    }

    [Fact]
    public void CurrentShippedDataPassesRuntimeValidationAndRetainsUnknownFacts()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fishing.kr.v2.json"));
        var conditions = FishingDatasetReader.ReadConditions(json);
        Assert.Equal(1258, conditions.Fish.Length);
        Assert.Equal(2, conditions.Fish.Count(fish => !fish.ConditionsKnown));
        Assert.Contains(conditions.Fish, fish => fish.Predators.Length > 0 && fish.IntuitionSeconds is null);
        Assert.Contains(conditions.Fish, fish => fish.FishEyesSupported is null);
        var grades = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fish-classifications.kr.v1.json"));
        Assert.Equal(335, FishingDatasetReader.ReadGrades(grades, conditions, FishingDatasetReader.ContentHash(json)).Entries.Length);
    }

    [Fact]
    public void HashNormalizesLineEndingsButDetectsContentChanges()
    {
        Assert.Equal(FishingDatasetReader.ContentHash("{}\n"), FishingDatasetReader.ContentHash("{}\r\n"));
        Assert.NotEqual(FishingDatasetReader.ContentHash("{}\n"), FishingDatasetReader.ContentHash("{ }\n"));
    }
}
