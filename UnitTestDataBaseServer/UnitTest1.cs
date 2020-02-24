using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using database_server;
using System.IO;
using log4net.Repository.Hierarchy;
using log4net;
using System.Reflection;
using System.Linq;

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
            var numElements = 1000;
            for (int i = 0; i < numElements; i++)
            {
                String key = $"Mohamed-{i}";
                String Value = $"test123213213-{i}";
                mainDatabase.Add(key, Value).Wait();
                var retrievedValue = mainDatabase.Get(key).Result;
                Assert.AreEqual(retrievedValue, Value);
            }
            //var myFiles = Directory.EnumerateFiles(testDirectory);
            //var numElemetsReal = myFiles.Count();
            //var numElementsExpected = numElements / 10 + 1;
            //Assert.AreEqual(numElementsExpected, numElements);
            
        }
        [TestCleanup]
        public void testClean()
        {
            
            
        }

        [TestInitialize]
        public void testInit()
        {
           
            
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


    }
}
