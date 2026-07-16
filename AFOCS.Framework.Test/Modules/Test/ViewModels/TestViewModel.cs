using AFOCS.Framework.Framework;
using Caliburn.Micro;
using System.ComponentModel;
using System.ComponentModel.Composition;

namespace AFOCS.Framework.Test.Modules.Test.ViewModels
{
    [DisplayName("Home View Model")]
    [Export]
    internal class TestViewModel: Document
    {

        public string Text => "fafa";




    }
}
