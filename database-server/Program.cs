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
using NUnit.Framework;

namespace database_server
{
    class Program
    {
       
        private static readonly log4net.ILog logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static LogDataBase mainDatabase;
        static void Main(string[] args)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository(Assembly.GetExecutingAssembly());
            log4net.Config.BasicConfigurator.Configure(hierarchy);
            logger.Info("Example log message");
            mainDatabase = LogDataBase.getTheDatabase();
            int numThreads = 7;
            Thread[] myThreads = new Thread[numThreads];
            for (int i = 0; i < numThreads; i++)
            {
                int threadNumber = i; // Need to capture outside the body of the lambda!!!!
                myThreads[i] = new Thread((j) => {
                    DoMultiInsert(1000, $"{threadNumber}-{threadNumber}", $"value-{i}");
                });

            }
            foreach (var thread in myThreads)
            {
                thread.Start();
            }
            foreach (var thread in myThreads)
            {
                thread.Join();
            }
            Assert.IsTrue(true);
            logger.Info("Finished!"); 
        }
        private static void DoMultiInsert(int numElements = 1000, string keyPrefix = "key", string valuePrefix = "value")
        {

            for (int i = 0; i < numElements; i++)
            {
                String key = $"{keyPrefix}-{i}";
                String Value = $"{valuePrefix}-{i}";
                mainDatabase.Add(key, Value).Wait();
                var retrievedValue = mainDatabase.Get(key).Result;
                Assert.AreEqual(retrievedValue, Value);
            }
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
