using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class CommandParser
{
    private const string Prefix = "/agente";

    public AgentCommand? Parse(ChatEvent chatEvent)
    {
        if (!chatEvent.Text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = chatEvent.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        var command = parts[1].ToLowerInvariant();
        var args = parts.Length > 2 ? parts[2..] : [];

        return new AgentCommand
        {
            Command = command,
            Args = args,
            Player = chatEvent.Player
        };
    }

    public CommandType GetCommandType(AgentCommand command)
    {
        return command.Command switch
        {
            "narrar" => CommandType.Narrar,
            "hora" => CommandType.Hora,
            "clima" => CommandType.Clima,
            "tiempo" => CommandType.Tiempo,
            "invocar" => CommandType.Invocar,
            "consejo" => CommandType.Consejo,
            "peligro" => CommandType.Peligro,
            _ => CommandType.Unknown
        };
    }
}
