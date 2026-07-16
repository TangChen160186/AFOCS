namespace AFOCS.Framework.Framework.Commands
{
    public interface ICommandRouter
    {
        CommandHandlerWrapper GetCommandHandler(CommandDefinitionBase commandDefinition);
    }
}