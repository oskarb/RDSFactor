using System;
using System.ServiceProcess;


namespace RDSFactor
{
    class Program
    {
        static void Main(string[] args)
        {
            ServiceBase[] servicesToRun;

            // More than one NT Service may run within the same process. To add
            // another service to this process, change the following line to
            // create a second service object. For example,
            //
            //   ServicesToRun = New System.ServiceProcess.ServiceBase () {New Service1, New MySecondUserService}
            //

            var server = new RDSFactor();

            if (Environment.UserInteractive)
            {
                server.StartInteractive(args);
                Console.WriteLine("Type any character to exit");
                Console.Read();
                server.StopInteractive();
            }
            else
            {
                servicesToRun = new ServiceBase[] {server};
                ServiceBase.Run(servicesToRun);
            }
        }
    }
}
