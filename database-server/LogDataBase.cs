using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace database_server
{
    public class LogDataBase : ITestDataBase, IDisposable
    {
        private static readonly log4net.ILog logger =
             log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static int fileCounter;
        private Stream memStream;
        private ReaderWriterLock mainStreamLock = new ReaderWriterLock();
        private TextWriter activeFileStream;
        //private UnicodeEncoding uniEncoding = new UnicodeEncoding();
        private ASCIIEncoding asciEncoding = new ASCIIEncoding();
        private const int numThreads = 3;
        private readonly int writeToDiskAfter;
        private int currentWriteCount=0;
        private const int maxFilesBeforeCompaction = 3;
        private int maxFileCounter = 0;
        private Thread compactionThread;
        private static LogDataBase theSingleton;
        private string oldLogsDirectory = "./oldLogs";
        System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromSeconds(5).TotalMilliseconds);
        // List of stream readers
        //private List<String> oldLogsFileList;
        private DataBaseMode mode;
        private Semaphore semaphore = new Semaphore(numThreads, numThreads);
        SortedList<String, string> oldLogsSortedList = new SortedList<string, string>(Comparer<String>.Create((s1, s2) =>
        {
            int number1 = getFileNumber(s1);
            int number2 = getFileNumber(s2);
            return number2.CompareTo(number1);
        }));
        public async Task<bool> Add(string Key, string Value)
        {
            return await Add(Key, Value, false);
        }
        private async Task<bool> Add(string Key, string Value, bool isDead=false)
        {
            Record r = new Record(Key,Value,isDead);
            var writtenValue = r.getRecordRepresentastion();
            //logger.LogInformation($"record added is {writtenValue}");
            mainStreamLock.AcquireWriterLock(50000); // wait indefinetly
            
            try
            {
                if (mode == DataBaseMode.ASYNCHORUS)
                {
                    memStream.WriteAsync(asciEncoding.GetBytes(writtenValue));
                    activeFileStream.WriteAsync(writtenValue);
                    memStream.FlushAsync();
                    activeFileStream.FlushAsync();
                }
                // The below code is broken
                else
                {
                    memStream.WriteAsync(asciEncoding.GetBytes(writtenValue)).GetAwaiter().GetResult();
                    activeFileStream.WriteAsync(writtenValue).Wait();
                    memStream.FlushAsync().Wait();
                    activeFileStream.FlushAsync().Wait();
                }
                Interlocked.Increment(ref currentWriteCount);
                if (currentWriteCount >= writeToDiskAfter)
                {
                    startNewLogFile();
                    currentWriteCount = 0;
                }
            }
            catch(Exception ex)
            {
                logger.Error($"Caught Exception while writing entry  in main memory stream log, execption caught {ex}");
                throw;
            }
            finally
            {
                mainStreamLock.ReleaseWriterLock();
            }
           
            return true;
        }
        private void startNewLogFile()
        {
            oldLogsSortedList.Add($"{oldLogsDirectory}/dbFile-{fileCounter}.dat", $"{oldLogsDirectory}/dbFile-{fileCounter}.dat"); // the old file becomes one of the newest files
                                                                        //oldLogsFileList.ins
            fileCounter++;
            var activeFilePath = $"{oldLogsDirectory}/dbFile-{fileCounter}.dat";
            activeFileStream.Close();
            activeFileStream.Dispose();
            activeFileStream = TextWriter.Synchronized(new StreamWriter(activeFilePath));
            memStream.Close();
            memStream.Dispose();
            memStream = getNewMemoryStream();
        }
        public async Task<bool> Remove(string Key)
        {
            var foundValue = await Get(Key);
            if (foundValue == null) // No element was presnet in the database, hence we will return false
                return false;
            await Add(Key, foundValue, isDead: true); //
            return true; // item was found and deleted in the database

        }
        //[MethodImpl(MethodImplOptions.Synchronized)]
        public async Task<String?> Get(string Key)
        {
            //oldLogsFileList.ForEach((s) => { logger.LogInformation($"File name is {s}"); });
            try
            {
                string memoryValue = null;
                bool foundInMemory = false;
                //mainStreamLock.AcquireReaderLock(50000);
                mainStreamLock.AcquireWriterLock(50000);
                var memStreamreader = (new StreamReader(memStream)); // do not use 'using' otherwise the stream gets disposed!
                memStreamreader.BaseStream.Seek(0, SeekOrigin.Begin);
                // This checks the in-memory file
                while (!memStreamreader.EndOfStream)
                {
                    String? readLine=null;
                    try
                    {
                        //String? readLine = await memStreamreader.ReadLineAsync().ConfigureAwait(true);
                        readLine = memStreamreader.ReadLineAsync().Result;
                        //logger.LogInformation(readLine);
                        var record = Record.getRecordFromString(readLine);

                        if (record.Key == Key)
                        {
                            foundInMemory = true;
                            memoryValue = record.isDead ? null : record.Value;
                        }
                    }
                    catch(Exception ex2)
                    {
                        logger.Error($"Caught Exception while searching in main memory stream log, execption caught {ex2} with readline ${readLine}");
                        throw;
                    }
                }
                if (foundInMemory)
                    return memoryValue;
            }
            catch(Exception ex)
            {
                logger.Error($"Caught Exception while searching in main memory stream log, execption caught {ex}");
                throw;
            }
            finally
            {
                mainStreamLock.ReleaseReaderLock();
            }
            
                return searchInOldLogs(Key).Result;
            
        }

        private async Task<String> compactOldFiles(string oldFilePath,string newFilePath)
        {
            var files = new String[] { oldFilePath, newFilePath }; // order matter here!
            Dictionary<String, (String value, bool isDead)> keyValuePairs = new Dictionary<string, (string, bool)>();
            foreach (var file in files)
            {
                using var fileReader = new StreamReader(file);
                while (!fileReader.EndOfStream)
                {
                    var readLine = await fileReader.ReadLineAsync();
                    var record = Record.getRecordFromString(readLine);
                    keyValuePairs[record.Key] = (record.Value, record.isDead);
                }
            }

            // Writing the map to disk
            string newFileName = oldFilePath + "TMP";
            using StreamWriter sw = new StreamWriter(newFileName);
            foreach(var element in keyValuePairs)
            {
                Record r = new Record(element.Key, element.Value.value, element.Value.isDead);
                await sw.WriteAsync(r.getRecordRepresentastion());
            }
            
            return newFileName;
        }
         
        private static long getEpochTime()
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            long ms = (long)(DateTime.UtcNow - epoch).TotalMilliseconds;
            
            return ms;
        }
        private async Task<String> searchInOldLogs(String key)
        {
         
           try
            {
                semaphore.WaitOne(); // will use a semaphore here!
                for(int i=oldLogsSortedList.Count-1;i>= 0; i--) // have to search in reverse order, from newest to oldest
                {
                    var oldFileEntry = oldLogsSortedList.ElementAt(i);
                    var oldFile = oldFileEntry.Value;
                    bool foundinFile = false;
                    string returnValue = null;
                    using var myStreamReader = new StreamReader(oldFile);
                    while (!myStreamReader.EndOfStream)
                    {
                        var readLine = await myStreamReader.ReadLineAsync();
                       
                        var record = Record.getRecordFromString(readLine);
                        if (record.Key == key)
                        {
                            foundinFile = true;
                            returnValue = record.isDead ? null : record.Value;
                        }
                    }
                    if(foundinFile==true)
                    {
                        return returnValue;
                    }
                }
            }
            catch(Exception ex)
            {
                logger.Error($"Caught Exception while searching for old Logs, execption caught {ex}");
                throw ;
            }
            finally
            {
                semaphore.Release();
            }
            
            return null; 
        }

        private void startNewThread()
        {
            compactionThread = new Thread(async (t) =>
            {
                while(true)
                {
                    await Task.Delay(1000 * 5);
                    Console.WriteLine("Performing Compaction!");
                    performCompaction();
                }
            });
            compactionThread.Start();
        }
       /**
        * 
        */

        public static LogDataBase getTheDatabase(DataBaseMode mode = DataBaseMode.SYNCHRONOUS, int writeToDiskAfter = 200, string logDirectory="./oldLogs")
        {
            if(theSingleton == null)
            {
                theSingleton = new LogDataBase(mode,writeToDiskAfter,logDirectory);
            }
            return theSingleton;
        }
        private LogDataBase( DataBaseMode mode = DataBaseMode.SYNCHRONOUS,int writeToDiskAfter=200, string logDirectory = "./oldLogs")
        {
            //oldLogsFileList = new List<String>();
            logger.Info("Inatilaing the database!");
            logger.Info("Inatilaing the database!");
            this.oldLogsDirectory = logDirectory;
            this.mode = mode;
            initalizeDatabase();
            memStream = getNewMemoryStream(); 
            //memStreamreader = new StreamReader(memStream);
            var activeFilePath = $"{oldLogsDirectory}/dbFile-{fileCounter}.dat";
            activeFileStream = TextWriter.Synchronized(new StreamWriter(activeFilePath));
            this.writeToDiskAfter = writeToDiskAfter;
            //performPeriodicComapction2();
            startNewThread();
        }
        private void performCompaction()
        {
            if(oldLogsSortedList.Count > maxFilesBeforeCompaction && (oldLogsSortedList.Count > 3)) // There has to be at least 2 files to compact + 1 current
            {
                // Need to use a sorted list data structure where the oldest files are always the first in the list
                var listCount = oldLogsSortedList.Count;
                var oldFile = oldLogsSortedList.ElementAt(listCount-1).Value;
                var newFile = oldLogsSortedList.ElementAt(listCount-2).Value;
                string newFileMerged = compactOldFiles(oldFile, newFile).Result;
                mainStreamLock.AcquireWriterLock(50000); // wait indefinetly
                try
                {
                    File.Delete(oldFile);
                    //oldLogsSortedList.RemoveAt(listCount-1); // remove the old log file
                    File.Move(newFileMerged, oldFile, true);
                    File.Delete(newFile);
                    oldLogsSortedList.RemoveAt(listCount - 2); // remove the old log file
                    
                    Console.WriteLine("Done with compaction!");
                }
                catch(Exception ex)
                {

                }
                finally
                {
                    mainStreamLock.ReleaseWriterLock();
                }
            }
        }
        
        public enum DataBaseMode
        {
            SYNCHRONOUS,
            ASYNCHORUS
        }
        private Stream getNewMemoryStream()
        {
            return MemoryStream.Synchronized(new MemoryStream()); // does it need to be Synchorized, We are using a ReaderWriterLock to Protect this resource?
        }
        private void initalizeDatabase()
        {
            try
            {
                var oldLogsDirName = oldLogsDirectory;
                if(!Directory.Exists(oldLogsDirName))
                {
                    Directory.CreateDirectory(oldLogsDirName);
                }
                DirectoryInfo oldLogsDir = new DirectoryInfo(oldLogsDirName);
                var files = oldLogsDir.GetFiles("*.dat").OrderBy((s1)=> { return s1.FullName; },
                    Comparer<String>.Create((s1,s2) => { return String.Compare(s1,s2,System.StringComparison.InvariantCulture); })).ToArray(); // ascending comparison, assuming oldest file has smaller number
                logger.Info($"Number of databse files found is {files.Length}");
                //fileCounter = files.Length;
                foreach (var file in files)
                {
                    oldLogsSortedList.Add(file.Name, file.FullName);
                    int fileNumber = getFileNumber(file.Name);
                    maxFileCounter = Math.Max(fileNumber, maxFileCounter);
                }
                fileCounter = maxFileCounter+1;
            }
            catch(Exception ex)
            {
                logger.Error(ex + " Caught exception wile inatilizaing data base main files, rethrowing the exception");
                throw ex;
            }
        }
      

        private static int getFileNumber(string fileName)
        {
            int numberBegiing = fileName.IndexOf("-") + 1;
            int numberEnd = fileName.LastIndexOf(".");
            String number1String = fileName.Substring(numberBegiing, numberEnd - numberBegiing);
            int number1 = int.Parse(number1String);
            return number1;
        }



        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    // Clear streams
                    
                    //oldLogsSortedList.ForEach(sr => {
                       
                    //});
                    activeFileStream.Flush();
                    activeFileStream.Close();
                    memStream.Flush();
                    memStream.Close();
                    semaphore.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~LogDataBase()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
