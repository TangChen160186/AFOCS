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



        [Import]  private ITestService1 _testService1;

        public string Text => "fafa";

     
        public TestViewModel()
        {

        }

        public void Test()
        {
            var d = IoC.Get<ITestService>();
            var d1 = IoC.Get<ITestService1>();
            var m = _testService1;
            //_testService.To();
            Console.WriteLine();
        }

    }
}
