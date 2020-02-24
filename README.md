# database-server-logbased
Implements a simple database that stores and retreives values by keys using the File system.

-Introduction:
This project implemnets a simple databse using the local file system. It performs automatic compaction of the database logs and helps keep the size of the database within operable arranges by trimming and merging deleted records.

# example usage
mainDatabase = LogDataBase.getTheDatabase(mode: LogDataBase.DataBaseMode.SYNCHRONOUS, writeToDiskAfter: 10);
String key = $"Mohamed-{i}"; <br/>
String insertedValue = $"test123213213-{i}"; <br/>
mainDatabase.Add(key, insertedValue).Wait(); <br/>
var retrievedValue = mainDatabase.Get(key).Result; <br/>
Assert.AreEqual(retrievedValue, insertedValue); <br/>

