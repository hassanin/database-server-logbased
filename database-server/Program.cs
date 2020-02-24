using log4net;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Logging
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
namespace database_server
{
    class Program
    {
       
        private static readonly log4net.ILog logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        static void Main(string[] args)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository(Assembly.GetExecutingAssembly());
            log4net.Config.BasicConfigurator.Configure(hierarchy);
            logger.Info("Example log message");
            logger.Debug("Mohamed");
            //var testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            //testDire testDirectory.Split(".")[0];
            //Directory.CreateDirectory(testDirectory);
            //logger.Info($"Created Directory is {testDirectory}");
            //logger.LogError("I have erroored");
            var myDatabase = LogDataBase.getTheDatabase(LogDataBase.DataBaseMode.ASYNCHORUS);

            BasicHttpListener basicHttpListener = new BasicHttpListener();
            //var ts = myDatabase.Add("Mohamed", "Ahmed").Result;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 1000; i < 2000; i++)
            {
                myDatabase.Add($"user{i}", $"I am honest Jack {3*i}").Wait();
                myDatabase.Add($"user{i}", $"I am honest Jack {2*i}").Wait();
                myDatabase.Add($"user{i}", $"I am honest Jack {3 * i}").Wait();
            }
            stopwatch.Stop();
            logger.Info($"Elapsed time is {stopwatch.ElapsedMilliseconds}");

            var valuex = myDatabase.Get("user10").Result;
            logger.Info($" User10 is {valuex}");
            logger.Info($" User12 is {myDatabase.Get("user1001").Result}");
            logger.Info($" User99 is {myDatabase.Get("user1100").Result}");
            logger.Info($" User205 is {myDatabase.Get("user1050").Result}");

            //Thread.Sleep(4000);
            //Console.ReadLine();
            Console.CancelKeyPress += (o,e) =>
            {
                e.Cancel = false;
                Console.WriteLine("Caught ctrl -c , shutting down server");
                var terminationResult = basicHttpListener.TerminateServer(5000); // will block until server timesOut
                Console.WriteLine("Finished ctrl -c , shutting down server");
                Thread.Sleep(2000);

                
                System.Environment.Exit(0);
            };
            while (true) { }


        }
        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }




    }

}
