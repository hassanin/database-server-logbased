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
        private StreamReader memStreamreader;
        private TextWriter activeFileStream;
        //private UnicodeEncoding uniEncoding = new UnicodeEncoding();
        private ASCIIEncoding asciEncoding = new ASCIIEncoding();
        private const int numThreads = 3;
        private Semaphore semaphore = new Semaphore(numThreads, numThreads);
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

        //[MethodImpl(MethodImplOptions.Synchronized)]
        public async Task<String?> Get(string Key)
        {
            mainStreamLock.AcquireReaderLock(0);
            try
            {
                memStreamreader.BaseStream.Seek(0, SeekOrigin.Begin);
                // This checks the in-memory file
                while (!memStreamreader.EndOfStream)
                {
                    String? readLine = await memStreamreader.ReadLineAsync();
                    //logger.LogInformation(readLine);
                    var parts = readLine.Split("\t");
                    if (parts[0] == Key)
                    {
                        return parts[1];
                    }
                }
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
   
        private async Task<String> searchInOldLogs(String key)
        {
          semaphore.WaitOne(); // will use a semaphore here!
           try
            {

                foreach (var oldFile in oldLogsFileList)
                {
                    using var myStreamReader = new StreamReader(oldFile);
                    while (!myStreamReader.EndOfStream)
                    {
                        var readLine = await myStreamReader.ReadLineAsync();
                        var parts = readLine.Split("\t");
                        if (parts[0] == key)
                        {
                            return parts[1];
                        }
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

        // List of stream readers
        private List<String> oldLogsFileList;
        private DataBaseMode mode;
        public LogDataBase( DataBaseMode mode = DataBaseMode.SYNCHRONOUS)
        {
            oldLogsFileList = new List<String>();
            this.mode = mode;
            initalizeDatabase();
            memStream = MemoryStream.Synchronized(new MemoryStream());
            memStreamreader = new StreamReader(memStream);
            var activeFilePath = $"./oldLogs/dbFile-{fileCounter}.dat";
            activeFileStream = TextWriter.Synchronized(new StreamWriter(activeFilePath));

            //var er = new StreamWriter()
        }
        public enum DataBaseMode
        {
            SYNCHRONOUS,
            ASYNCHORUS
        }
        private void initalizeDatabase()
        {
            try
            {
                var oldLogsDirName = "./oldLogs";
                DirectoryInfo oldLogsDir = new DirectoryInfo(oldLogsDirName);
                var files = oldLogsDir.GetFiles("*.dat").OrderBy((s1)=> { return s1.FullName; },
                    Comparer<String>.Create((s1,s2) => { return String.Compare(s1,s2,System.StringComparison.InvariantCulture); })).ToArray(); // ascending comparison, assuming oldest file has smaller number
                //files.OrderBy()
                logger.LogInformation($"Number of databse files found is {files.Length}");
                fileCounter = files.Length;
                foreach (var file in files)
                {
                    oldLogsFileList.Add(file.FullName);
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Caught exception wile inatilizaing data base main files, rethrowing the exception");
                throw ex;
            }
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
                    oldLogsFileList.ForEach(sr => {
                       
                    });
                    activeFileStream.Flush();
                    activeFileStream.Close();
                    memStream.Flush();
                    memStream.Close();
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
