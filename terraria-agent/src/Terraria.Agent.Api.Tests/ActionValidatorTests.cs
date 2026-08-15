using Terraria.Agent.Api.Services;
using Xunit;

namespace Terraria.Agent.Api.Tests;

public class ActionValidatorTests
{
    private readonly ActionValidator _validator = new();

    [Theory]
    [InlineData("spawnboss EyeOfCthulhu", true)]
    [InlineData("spawnboss eye of cthulhu", true)]
    [InlineData("spawnboss The Twins", true)]
    [InlineData("spawnboss muralla de carne", true)]
    [InlineData("spawnboss Medusa", false)]
    [InlineData("spawnboss", false)]
    public void SpawnBoss_Validates(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Fact]
    public void SpawnBoss_NormalizesSpanishAliasToCanonicalName()
    {
        var result = _validator.Validate("spawnboss muralla de carne");
        Assert.True(result.IsValid);
        Assert.Equal("spawnboss WallOfFlesh", result.Action);
    }

    [Fact]
    public void SpawnBoss_NormalizesCanonicalNameCaseInsensitively()
    {
        var result = _validator.Validate("spawnboss eye of cthulhu");
        Assert.True(result.IsValid);
        Assert.Equal("spawnboss EyeOfCthulhu", result.Action);
    }

    [Fact]
    public void SpawnBoss_RejectsUnknownBoss()
    {
        var result = _validator.Validate("spawnboss Medusa");
        Assert.False(result.IsValid);
        Assert.Contains("jefe", result.Reason);
    }

    [Fact]
    public void Give_WithoutArgumentsIsRejected()
    {
        Assert.False(_validator.Validate("give").IsValid);
    }

    [Fact]
    public void Give_WithQuotedItemParsesTargetAndQuantity()
    {
        var result = _validator.Validate("give \"Iron Bar\" Testeador1 20");
        Assert.True(result.IsValid);
        Assert.Equal("Iron Bar", result.Item);
        Assert.Equal("Testeador1", result.GiveTarget);
        Assert.Equal(20, result.Quantity);
        Assert.Equal("Testeador1", result.TargetPlayer);
        Assert.Equal("give \"Iron Bar\" Testeador1 20", result.Action);
    }

    [Fact]
    public void Give_WithoutQuotesParsesItemAndTarget()
    {
        var result = _validator.Validate("give Iron Bar Testeador1 20");
        Assert.True(result.IsValid);
        Assert.Equal("Iron Bar", result.Item);
        Assert.Equal("Testeador1", result.GiveTarget);
        Assert.Equal(20, result.Quantity);
    }

    [Fact]
    public void Give_WithQuotedItemAndMultiWordTargetKeepsTargetIntact()
    {
        var result = _validator.Validate("give \"Life Fruit\" hor net");
        Assert.True(result.IsValid);
        Assert.Equal("Life Fruit", result.Item);
        Assert.Equal("hor net", result.GiveTarget);
    }

    [Theory]
    [InlineData("time 10", true)]
    [InlineData("time 22", true)]
    [InlineData("time 25", false)]
    [InlineData("time night", true)]
    [InlineData("time midnight", true)]
    [InlineData("time 10:30", false)]
    public void Time_AcceptsHourOrMoment(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Theory]
    [InlineData("worldevent sandstorm", true)]
    [InlineData("worldevent bloodmoon", true)]
    [InlineData("worldevent lluvia", false)]
    [InlineData("worldevent", false)]
    public void WorldEvent_AcceptsKnownEvents(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Theory]
    [InlineData("bridge rain on", true)]
    [InlineData("bridge rain heavy", true)]
    [InlineData("bridge rain maybe", false)]
    [InlineData("bridge slime rain off", true)]
    [InlineData("bridge slime rain", false)]
    public void Bridge_AcceptsKnownSubcommands(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Fact]
    public void Buff_IsRejected()
    {
        Assert.False(_validator.Validate("buff").IsValid);
    }

    [Theory]
    [InlineData("heal", null)]
    [InlineData("heal Sobrino2", "Sobrino2")]
    [InlineData("maxhp Testeador1", "Testeador1")]
    public void OptionalPlayer_FamiliesExposeTarget(string action, string? expected)
    {
        var result = _validator.Validate(action);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.TargetPlayer);
    }

    [Theory]
    [InlineData("tp", false)]
    [InlineData("tp Sobrino2", true)]
    [InlineData("kill", false)]
    [InlineData("kill Sobrino2", true)]
    [InlineData("kick Sobrino2 razon", true)]
    public void RequiredPlayer_FamiliesRequireTarget(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Fact]
    public void Hardmode_AndSave_AreValid()
    {
        Assert.True(_validator.Validate("hardmode").IsValid);
        Assert.True(_validator.Validate("save").IsValid);
        Assert.False(_validator.Validate("hardmode extra").IsValid);
    }

    [Theory]
    [InlineData("spawnmob zombie", "spawnmob zombie 1")]
    [InlineData("spawnmob zombie 10", "spawnmob zombie 10")]
    [InlineData("spawnmob Giant Bat 5", "spawnmob Giant Bat 5")]
    public void SpawnMob_ParsesMobAndOptionalQuantity(string action, string expected)
    {
        var result = _validator.Validate(action);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Action);
    }

    [Theory]
    [InlineData("maxspawns 10", true)]
    [InlineData("spawnrate 5", true)]
    [InlineData("maxspawns 0", false)]
    [InlineData("spawnrate xyz", false)]
    public void Numeric_FamiliesRequireNumber(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Fact]
    public void UnknownVerb_IsRejected()
    {
        Assert.False(_validator.Validate("teleportalo todo").IsValid);
    }

    [Fact]
    public void NullOrEmpty_IsRejected()
    {
        Assert.False(_validator.Validate(null!).IsValid);
        Assert.False(_validator.Validate("").IsValid);
    }
}
