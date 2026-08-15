namespace Terraria.Agent.Api.Services;

public sealed class ActionValidation
{
    public bool IsValid { get; init; }
    public string? Action { get; init; }
    public string? Reason { get; init; }
    public string? TargetPlayer { get; init; }
    public string? Item { get; init; }
    public string? GiveTarget { get; init; }
    public int Quantity { get; init; } = 1;
}

public class ActionValidator
{
    private static readonly HashSet<string> TimeNames = new(StringComparer.OrdinalIgnoreCase)
        { "day", "night", "noon", "dusk", "midnight" };

    private static readonly HashSet<string> RainModes = new(StringComparer.OrdinalIgnoreCase)
        { "on", "off", "heavy" };

    private static readonly HashSet<string> SlimeRainModes = new(StringComparer.OrdinalIgnoreCase)
        { "on", "off" };

    private static readonly HashSet<string> WorldEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "bloodmoon", "eclipse", "fullmoon", "sandstorm", "meteor", "lanternsnight",
        "meteorshower", "coinrain", "star", "halloween", "xmas", "goblins", "pirates", "martians",
        "slime"
    };

    private static readonly HashSet<string> NoArgCommands = new(StringComparer.OrdinalIgnoreCase)
        { "hardmode", "home", "spawn", "setspawn", "settle", "butcher", "save" };

    private static readonly Dictionary<string, string> BossAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kingslime"] = "KingSlime",
        ["eyeofcthulhu"] = "EyeOfCthulhu",
        ["ojo"] = "EyeOfCthulhu",
        ["eaterofworlds"] = "EaterOfWorlds",
        ["gusano"] = "EaterOfWorlds",
        ["skeletron"] = "Skeletron",
        ["esqueleto"] = "Skeletron",
        ["queenbee"] = "QueenBee",
        ["abeja"] = "QueenBee",
        ["thetwins"] = "TheTwins",
        ["gemelos"] = "TheTwins",
        ["thedestroyer"] = "TheDestroyer",
        ["destructor"] = "TheDestroyer",
        ["skeletronprime"] = "SkeletronPrime",
        ["primo"] = "SkeletronPrime",
        ["plantera"] = "Plantera",
        ["golem"] = "Golem",
        ["lunaticcultist"] = "LunaticCultist",
        ["moonlord"] = "MoonLord",
        ["wallofflesh"] = "WallOfFlesh",
        ["muralladecarne"] = "WallOfFlesh",
        ["pareddecarne"] = "WallOfFlesh",
        ["murodecarne"] = "WallOfFlesh",
        ["wof"] = "WallOfFlesh"
    };

    public ActionValidation Validate(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return Invalid(null, "el comando está vacío");

        var parts = action.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var verb = parts[0].TrimStart('/').ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        switch (verb)
        {
            case "time":
                return ValidateTime(args);
            case "bridge":
                return ValidateBridge(args);
            case "worldevent":
                return ValidateWorldEvent(args);
            case "spawnboss":
                return ValidateSpawnBoss(args);
            case "spawnmob":
                return ValidateSpawnMob(args);
            case "give":
                return ValidateGive(args);
            case "heal":
            case "godmode":
            case "maxhp":
                return ValidateOptionalPlayer(verb, args);
            case "kill":
            case "kick":
            case "mute":
            case "slap":
                return ValidateRequiredPlayer(verb, args, allowExtraArgs: true);
            case "tp":
            case "tphere":
                return ValidateRequiredPlayer(verb, args, allowExtraArgs: false);
            case "warp":
                return ValidateWarp(args);
            case "maxspawns":
            case "spawnrate":
                return ValidateNumber(verb, args);
            default:
                if (NoArgCommands.Contains(verb))
                    return args.Length == 0
                        ? Valid(verb, null)
                        : Invalid($"{verb} {string.Join(" ", args)}", $"'{verb}' no admite argumentos");
                return Invalid(action, $"no conozco el comando '{verb}'");
        }
    }

    private static ActionValidation ValidateTime(string[] args)
    {
        if (args.Length != 1)
            return Invalid(null, "'time' requiere una hora o un momento del día");
        var arg = args[0].ToLowerInvariant();
        if (TimeNames.Contains(arg))
            return Valid($"time {arg}", null);
        if (int.TryParse(args[0], out var hour) && hour >= 0 && hour <= 23)
            return Valid($"time {hour}", null);
        return Invalid(null, $"'{args[0]}' no es una hora válida (0-23 o day/night/noon/dusk/midnight)");
    }

    private static ActionValidation ValidateBridge(string[] args)
    {
        if (args.Length == 0)
            return Invalid(null, "'bridge' requiere un subcomando");

        if (args[0].Equals("rain", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 2)
                return Invalid(null, "'bridge rain' requiere on, off o heavy");
            var mode = args[1].ToLowerInvariant();
            return RainModes.Contains(mode)
                ? Valid($"bridge rain {mode}", null)
                : Invalid(null, $"'{args[1]}' no es un modo de lluvia válido");
        }

        if (args[0].Equals("slime", StringComparison.OrdinalIgnoreCase) &&
            args.Length >= 2 && args[1].Equals("rain", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 3)
                return Invalid(null, "'bridge slime rain' requiere on u off");
            var mode = args[2].ToLowerInvariant();
            return SlimeRainModes.Contains(mode)
                ? Valid($"bridge slime rain {mode}", null)
                : Invalid(null, $"'{args[2]}' no es un modo válido");
        }

        return Invalid(null, $"'bridge {string.Join(" ", args)}' no es un comando conocido");
    }

    private static ActionValidation ValidateWorldEvent(string[] args)
    {
        if (args.Length != 1)
            return Invalid(null, "'worldevent' requiere un evento");
        var evt = args[0].ToLowerInvariant();
        return WorldEvents.Contains(evt)
            ? Valid($"worldevent {evt}", null)
            : Invalid(null, $"'{args[0]}' no es un evento conocido");
    }

    private static ActionValidation ValidateSpawnBoss(string[] args)
    {
        if (args.Length == 0)
            return Invalid(null, "'spawnboss' requiere un jefe");
        var name = string.Join(" ", args).Trim();
        var compact = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"\s+", "");
        if (BossAliases.TryGetValue(compact, out var canonical))
            return Valid($"spawnboss {canonical}", null);
        return Invalid(null, $"no conozco al jefe '{name}'");
    }

    private static ActionValidation ValidateSpawnMob(string[] args)
    {
        if (args.Length == 0)
            return Invalid(null, "'spawnmob' requiere un mob");
        var last = args[^1];
        var qty = 1;
        var mobTokens = args;
        if (int.TryParse(last, out var q) && q > 0 && q <= 999)
        {
            qty = q;
            mobTokens = args[..^1];
        }
        if (mobTokens.Length == 0)
            return Invalid(null, "'spawnmob' requiere un mob");
        var mob = string.Join(" ", mobTokens);
        return Valid($"spawnmob {mob} {qty}".TrimEnd(), null);
    }

    private static ActionValidation ValidateGive(string[] args)
    {
        if (args.Length < 2)
            return Invalid(null, "el comando 'give' necesita el item y el jugador");

        var rest = string.Join(" ", args);
        string item;
        string player;
        int qty = 1;

        if (rest.StartsWith("\""))
        {
            var end = rest.IndexOf('"', 1);
            if (end < 0)
                return Invalid(null, "las comillas del item no están cerradas");
            item = rest[1..end];
            var tail = rest[(end + 1)..].Trim();
            var tailParts = tail.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tailParts.Length == 0)
                return Invalid(null, "el comando 'give' necesita el jugador");
            if (tailParts.Length >= 2 && int.TryParse(tailParts[^1], out var q) && q > 0 && q <= 999)
            {
                qty = q;
                player = string.Join(" ", tailParts[..^1]);
            }
            else
            {
                player = tail;
            }
        }
        else
        {
            var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var consumed = parts.Length;
            if (parts.Length >= 2 && int.TryParse(parts[^1], out var q) && q > 0 && q <= 999)
            {
                qty = q;
                consumed = parts.Length - 1;
            }
            if (consumed < 2)
                return Invalid(null, "el comando 'give' necesita el item y el jugador");
            player = parts[consumed - 1];
            item = string.Join(" ", parts.Take(consumed - 1));
        }

        if (string.IsNullOrWhiteSpace(item) || string.IsNullOrWhiteSpace(player))
            return Invalid(null, "el comando 'give' necesita el item y el jugador");

        return new ActionValidation
        {
            IsValid = true,
            Action = $"give \"{item}\" {player} {qty}".TrimEnd(),
            TargetPlayer = player,
            Item = item,
            GiveTarget = player,
            Quantity = qty
        };
    }

    private static ActionValidation ValidateOptionalPlayer(string verb, string[] args)
    {
        if (args.Length == 0)
            return Valid(verb, null);
        if (args.Length == 1)
            return Valid($"{verb} {args[0]}", args[0]);
        return Invalid($"{verb} {string.Join(" ", args)}", $"'{verb}' admite a lo sumo un jugador");
    }

    private static ActionValidation ValidateRequiredPlayer(string verb, string[] args, bool allowExtraArgs)
    {
        if (args.Length == 0)
            return Invalid(verb, $"'{verb}' necesita un jugador");
        if (!allowExtraArgs && args.Length != 1)
            return Invalid(verb, $"'{verb}' admite exactamente un jugador");
        return Valid($"{verb} {string.Join(" ", args)}", args[0]);
    }

    private static ActionValidation ValidateWarp(string[] args)
    {
        if (args.Length == 0)
            return Invalid(null, "'warp' requiere list, add <nombre> o <nombre>");
        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase) && args.Length == 1)
            return Valid("warp list", null);
        if (args[0].Equals("add", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
            return Valid($"warp add {string.Join(" ", args.Skip(1))}", null);
        return Valid($"warp {string.Join(" ", args)}", null);
    }

    private static ActionValidation ValidateNumber(string verb, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var n) || n < 1 || n > 999)
            return Invalid(null, $"'{verb}' requiere un número entre 1 y 999");
        return Valid($"{verb} {n}", null);
    }

    private static ActionValidation Valid(string? action, string? targetPlayer)
        => new() { IsValid = true, Action = action, TargetPlayer = targetPlayer };

    private static ActionValidation Invalid(string? action, string reason)
        => new() { IsValid = false, Action = action, Reason = reason };
}
