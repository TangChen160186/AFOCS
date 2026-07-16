using System.ComponentModel.Composition;

namespace AFOCS.Framework.Test
{
    public interface ITestService
    {
        void To();
    }
    public interface ITestService1: ITestService
    {
        void To();
    }

    [Export]
    [Export(typeof(ITestService1))]
    public class ITest1 : ITestService
    {
        public void To()
        {
            Console.WriteLine("ITest1");
        }
    }

    [Export(typeof(ITestService))]
    public class ITest2 : ITestService
    {
        public void To()
        {
            Console.WriteLine("ITest2");
        }
    }
}
