namespace AFOCS.Framework.Framework.Commands
{
    public interface ICommandUiItem
    {
        CommandDefinitionBase CommandDefinition { get; }
        void Update(CommandHandlerWrapper commandHandler);
    }
}