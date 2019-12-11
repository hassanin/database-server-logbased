using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Logging
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace database_server
{
    class Program
    {
        internal static ILogger mainLogger;
        static void Main(string[] args)
        {
        

            
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddEventLog()
                    .AddConsole((s) => { Console.WriteLine(s); s.IncludeScopes = false; });

            });
            
     
            ILogger logger = loggerFactory.CreateLogger<Program>();
            mainLogger = logger;
            logger.LogInformation("Example log message");
            logger.LogDebug("Mohamed");
            //logger.LogError("I have erroored");
            Console.WriteLine(logger.IsEnabled(LogLevel.Information));
            using var myDatabase = new LogDataBase(LogDataBase.DataBaseMode.ASYNCHORUS);

            //var ts = myDatabase.Add("Mohamed", "Ahmed").Result;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 5; i < 20; i++)
            {
                myDatabase.Add($"user{i}", $"I am honest Jack {i*25}").Wait();
            }
            stopwatch.Stop();
            logger.LogInformation($"Elapsed time is {stopwatch.ElapsedMilliseconds}");

            var valuex = myDatabase.Get("user10").Result;
            logger.LogInformation($" User10 is {valuex}");
            logger.LogInformation($" User12 is {myDatabase.Get("user12").Result}");
            logger.LogInformation($" User99 is {myDatabase.Get("user99").Result}");
            logger.LogInformation($" User205 is {myDatabase.Get("user205").Result}");

            //Thread.Sleep(4000);
            Console.ReadLine();

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
