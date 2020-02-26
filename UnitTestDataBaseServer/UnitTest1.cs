using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using database_server;
using System.IO;
using log4net.Repository.Hierarchy;
using log4net;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace UnitTestDataBaseServer
{
    [TestClass]
    public class UnitTest1
    {
        private static readonly log4net.ILog logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static LogDataBase mainDatabase;
        private static string testDirectory;
        [TestMethod]
        public void TestBasicInsert()
        {
            String key = "Mohamed";
            String Value = "test123213213";
            mainDatabase.Add(key, Value).Wait();
            var retrievedValue = mainDatabase.Get(key).Result;
            Assert.AreEqual(retrievedValue, Value);
        }

        [TestMethod]
        public void TestMultiInsert()
        {
            DoMultiInsert(1000, "mykey1", "great");



        }
        [TestMethod]
        public void TestRemove()
        {
            String key = "Mohamed12";
            String Value = "test12321321334";
            mainDatabase.Add(key, Value).Wait();
            var retrievedValue = mainDatabase.Get(key).Result;
            Assert.AreEqual(retrievedValue, Value);
            var removalResult= mainDatabase.Remove(Key: key).Result;
            Assert.AreEqual(true, removalResult);
            retrievedValue = mainDatabase.Get(key).Result;
            Assert.AreEqual(retrievedValue, null);
        }
        [TestMethod]
        public void TestParallel()
        {
            
            //await Task.Delay(5000);
            int numThreads = 5;
            Thread[] myThreads = new Thread[numThreads];
            for(int i=0;i<numThreads;i++)
            {
                myThreads[i]=new Thread(() => {
                    DoMultiInsert(1000, $"{i}-{i}", $"value-{i}");
                });
                
            }
            foreach(var thread in myThreads)
            {
                thread.Start();
            }
            foreach (var thread in myThreads)
            {
                thread.Join();
            }
            Assert.IsTrue(false);
            
        }
       

        [ClassInitialize]
        public static void InitClass(TestContext context)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository(Assembly.GetExecutingAssembly());
            log4net.Config.BasicConfigurator.Configure(hierarchy);
            testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            testDirectory = testDirectory.Split(".")[0];
            Directory.CreateDirectory(testDirectory);
            //testDirectory = testDirectory.Remove(testDirectory.Length);
            mainDatabase = LogDataBase.getTheDatabase(mode: LogDataBase.DataBaseMode.SYNCHRONOUS, writeToDiskAfter: 10, logDirectory: testDirectory);
        }

        [ClassCleanup]
        public static void ClassClean()
        {
            mainDatabase.Dispose();
            Directory.Delete(testDirectory, true);

        }

        [TestCleanup]
        public void testClean()
        {


        }

        [TestInitialize]
        public void testInit()
        {


        }

        private static void DoMultiInsert(int numElements=1000,string keyPrefix="key",string valuePrefix="value")
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
       

    }
}
