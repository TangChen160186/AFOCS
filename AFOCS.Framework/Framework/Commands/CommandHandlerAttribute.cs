using System.ComponentModel.Composition;

namespace AFOCS.Framework.Framework.Commands
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandHandlerAttribute : ExportAttribute
    {
        public CommandHandlerAttribute()
            : base(typeof(ICommandHandler))
        {
            
        }
    }
}