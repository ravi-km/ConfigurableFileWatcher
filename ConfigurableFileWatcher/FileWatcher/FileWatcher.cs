using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.Caching;

namespace FileWatcher
{
    internal class CacheItemValue
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int RetryCount { get; set; }
    }

    public class FileWatcher
    {
        private FileSystemWatcher fileSystemWatcher { get; set; }
        private readonly MemoryCache _memCache;
        private readonly CacheItemPolicy _cacheItemPolicy;
        private readonly int CacheTimeSeconds = Convert.ToInt32(ConfigurationManager.AppSettings.Get("CacheTimeSeconds"));
        private readonly int MaxRetries = Convert.ToInt32(ConfigurationManager.AppSettings.Get("MaxRetries"));
        public string destination { get; set; }
        public int success = (int)ApplicationResponseTypes.FAILNOTFOUND;
        private readonly object resourceLock = new object();

        public FileWatcher(string FilePath, string FileName, string Destination, object ResourceLock)
        {
            try
            {

                fileSystemWatcher = new FileSystemWatcher(FilePath, FileName);
                fileSystemWatcher.Error += new ErrorEventHandler(OnError);

                fileSystemWatcher.Created += OnCreated;
                fileSystemWatcher.EnableRaisingEvents = true;

                resourceLock = ResourceLock;

                destination = Destination;

                _memCache = MemoryCache.Default;
                _cacheItemPolicy = new CacheItemPolicy
                {
                    RemovedCallback = OnRemovedFromCache
                };

                Console.WriteLine($"Watching for input file in folder: {FilePath}");
                Trace.TraceInformation($"Watching for input file in folder: {FilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Trace.TraceError(ex.ToString());
            }

        }

        private static void OnError(object source, ErrorEventArgs e)
        {
            Console.WriteLine("The FileSystemWatcher has detected an error");
            Trace.TraceError("The FileSystemWatcher has detected an error");
            if (e.GetException().GetType() == typeof(InternalBufferOverflowException))
            {
                Console.WriteLine(("The file system watcher experienced an internal buffer overflow: " + e.GetException().Message));
                Trace.TraceError(("The file system watcher experienced an internal buffer overflow: " + e.GetException().Message));
            }
        }

        private void OnCreated(object source, FileSystemEventArgs e)
        {
            try
            {

                _cacheItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(CacheTimeSeconds);

                CacheItemValue fileData = new CacheItemValue()
                {
                    FilePath = e.FullPath,
                    RetryCount = 0,
                    FileName = e.Name
                };

                _memCache.AddOrGetExisting(e.Name, fileData, _cacheItemPolicy);

                Console.WriteLine("File Found - " + e.FullPath);
                Trace.TraceInformation("File Found - " + e.FullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Trace.TraceError(ex.ToString());
            }

        }

        private void OnRemovedFromCache(CacheEntryRemovedArguments args)
        {
            try
            {
                if (args.RemovedReason != CacheEntryRemovedReason.Expired)
                {
                    return;
                }

                CacheItemValue cacheItemValue = (CacheItemValue)args.CacheItem.Value;

                if (cacheItemValue.RetryCount > MaxRetries)
                {
                    return;
                }

                if (IsFileLocked(cacheItemValue.FilePath))
                {
                    cacheItemValue.RetryCount++;
                    _cacheItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(CacheTimeSeconds);

                    _memCache.Add(cacheItemValue.FileName, cacheItemValue, _cacheItemPolicy);
                    Console.WriteLine("File is locked, waiting to move the file");
                    Trace.TraceInformation("File is locked, waiting to move the file");
                }
                else
                {
                    lock (resourceLock)
                    {
                        Console.WriteLine("Ready to move file");
                        File.Copy(cacheItemValue.FilePath, destination + "\\" + cacheItemValue.FileName);
                        fileSystemWatcher.Dispose();
                        Console.WriteLine("File moved");
                        Trace.TraceInformation("File moved");
                        success = (int)ApplicationResponseTypes.SUCCESS;
                        Environment.Exit(0);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Trace.TraceError(ex.ToString());
            }
        }

        protected static bool IsFileLocked(string filePath)
        {
            FileStream stream = null;
            FileInfo file = new FileInfo(filePath);

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                stream?.Close();
            }
            return false;
        }

    }
}