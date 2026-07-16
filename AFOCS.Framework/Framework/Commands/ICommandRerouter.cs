namespace AFOCS.Framework.Framework.Commands
{
    public interface ICommandRerouter
    {
        object GetHandler(CommandDefinitionBase commandDefinition);
    }
}