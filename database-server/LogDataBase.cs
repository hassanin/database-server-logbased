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
    class LogDataBase : ITestDataBase, IDisposable
    {
        private ILogger logger = Program.mainLogger;
        private static int fileCounter;
        private Stream memStream;
        private ReaderWriterLock mainStreamLock = new ReaderWriterLock();
        private TextWriter activeFileStream;
        //private UnicodeEncoding uniEncoding = new UnicodeEncoding();
        private ASCIIEncoding asciEncoding = new ASCIIEncoding();
        private const int numThreads = 3;
        private readonly int writeToDiskAfter;
        private int currentWriteCount=0;
        private const int maxFilesBeforeCompaction = 10;
        private int maxFileCounter = 0;
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
            Record r = new Record(Key,Value);
            var writtenValue = r.getRecordRepresentastion();
            //logger.LogInformation($"record added is {writtenValue}");
            mainStreamLock.AcquireWriterLock(0); // wait indefinetly
            
            try
            {
                if (mode == DataBaseMode.ASYNCHORUS)
                {
                    memStream.WriteAsync(asciEncoding.GetBytes(writtenValue));
                    activeFileStream.WriteAsync(writtenValue);
                    memStream.FlushAsync();
                    activeFileStream.FlushAsync();
                }
                else
                {
                    await memStream.WriteAsync(asciEncoding.GetBytes(writtenValue));
                    await activeFileStream.WriteAsync(writtenValue);
                    await memStream.FlushAsync();
                    await activeFileStream.FlushAsync();
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
                logger.LogError($"Caught Exception while writing entry  in main memory stream log, execption caught {ex}");
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
            oldLogsSortedList.Add($"./oldLogs/dbFile-{fileCounter}.dat", $"./oldLogs/dbFile-{fileCounter}.dat"); // the old file becomes one of the newest files
                                                                        //oldLogsFileList.ins
            fileCounter++;
            var activeFilePath = $"./oldLogs/dbFile-{fileCounter}.dat";
            activeFileStream.Close();
            activeFileStream.Dispose();
            activeFileStream = TextWriter.Synchronized(new StreamWriter(activeFilePath));
            memStream.Close();
            memStream.Dispose();
            memStream = getNewMemoryStream();
        }

        //[MethodImpl(MethodImplOptions.Synchronized)]
        public async Task<String?> Get(string Key)
        {
            //oldLogsFileList.ForEach((s) => { logger.LogInformation($"File name is {s}"); });
            try
            {
                string memoryValue = null;
                bool foundInMemory = false;
                mainStreamLock.AcquireReaderLock(0);
                
                var memStreamreader = new StreamReader(memStream); // do not use 'using' otherwise the stream gets disposed!
                memStreamreader.BaseStream.Seek(0, SeekOrigin.Begin);
                // This checks the in-memory file
                while (!memStreamreader.EndOfStream)
                {
                    //String? readLine = await memStreamreader.ReadLineAsync().ConfigureAwait(true);
                    String? readLine = memStreamreader.ReadLineAsync().Result;
                    //logger.LogInformation(readLine);
                    var record = Record.getRecordFromString(readLine);
                    
                    if (record.Key == Key)
                    {
                        foundInMemory = true;
                        memoryValue = record.isDead ? null : record.Value;
                    }
                }
                if (foundInMemory)
                    return memoryValue;
            }
            catch(Exception ex)
            {
                logger.LogError($"Caught Exception while searching in main memory stream log, execption caught {ex}");
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
            string newFileName = $"db-File-{getEpochTime()}";
            using StreamWriter sw = new StreamWriter(newFileName);
            foreach(var element in keyValuePairs)
            {
                Record r = new Record(element.Key, element.Value.value, element.Value.isDead);
                await sw.WriteAsync(r.getRecordRepresentastion());
            }
            return null;
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
                
                foreach (var oldFileEntry in oldLogsSortedList)
                {
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
                logger.LogError($"Caught Exception while searching for old Logs, execption caught {ex}");
                throw ;
            }
            finally
            {
                semaphore.Release();
            }
            
            return null; 
        }

     
       /**
        * 
        */
        public LogDataBase( DataBaseMode mode = DataBaseMode.SYNCHRONOUS,int writeToDiskAfter=200)
        {
            //oldLogsFileList = new List<String>();
            this.mode = mode;
            initalizeDatabase();
            memStream = getNewMemoryStream(); 
            //memStreamreader = new StreamReader(memStream);
            var activeFilePath = $"./oldLogs/dbFile-{fileCounter}.dat";
            activeFileStream = TextWriter.Synchronized(new StreamWriter(activeFilePath));
            this.writeToDiskAfter = writeToDiskAfter;
            
        }
        private void performCompaction()
        {
            if(oldLogsSortedList.Count > maxFilesBeforeCompaction)
            {
                // Need to use a sorted list data structure where the oldest files are always the first in the list
                var oldFile = oldLogsSortedList.ElementAt(0).Key;
                var newFile = oldLogsSortedList.ElementAt(1).Key;
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
                var oldLogsDirName = "./oldLogs";
                DirectoryInfo oldLogsDir = new DirectoryInfo(oldLogsDirName);
                var files = oldLogsDir.GetFiles("*.dat").OrderBy((s1)=> { return s1.FullName; },
                    Comparer<String>.Create((s1,s2) => { return String.Compare(s1,s2,System.StringComparison.InvariantCulture); })).ToArray(); // ascending comparison, assuming oldest file has smaller number
                logger.LogInformation($"Number of databse files found is {files.Length}");
                fileCounter = files.Length;
                foreach (var file in files)
                {
                    oldLogsSortedList.Add(file.Name, file.Name);
              
                    
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Caught exception wile inatilizaing data base main files, rethrowing the exception");
                throw ex;
            }
        }
      

        private static int getFileNumber(string fileName)
        {
            int numberBegiing = fileName.IndexOf("-");
            int numberEnd = fileName.IndexOf(".");
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
