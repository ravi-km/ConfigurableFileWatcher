using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Security.Permissions;
using System.Text;
using System.Threading;

namespace FileWatcher
{
    public enum ApplicationResponseTypes
    {
        SUCCESS = 0,
        FAILNOTFOUND = 1,
        FAILEXCEPTION = -1,
        FAILMISSINGPARAMS = -2
    }

    public class Program
    {
        public static int Main(string[] args)
        {
            int success = (int)ApplicationResponseTypes.FAILNOTFOUND;
            Run(args, ref success);
            return success;
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void Run(string[] args, ref int success)
        {
            try
            {
                string FilePath, FileName, Destination = "";
                int PollingIntervalMilliSec = 0;
                object resourceLock = new object();

                if (args.Length > 0)
                {
                    FilePath = args[0].Trim();
                    FileName = args[1].Trim();
                    Destination = args[2].Trim();
                    PollingIntervalMilliSec = Convert.ToInt32(args[3].Trim());
                }
                else
                {
                    FilePath = ConfigurationManager.AppSettings.Get("path");
                    FileName = ConfigurationManager.AppSettings.Get("fileName");
                    FileName = ConfigurationManager.AppSettings.Get("Destination");
                    PollingIntervalMilliSec = Convert.ToInt32(ConfigurationManager.AppSettings.Get("PollingIntervalMilliSec"));
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Path - " + FilePath);
                sb.AppendLine("Name - " + FileName);
                sb.AppendLine("Destination - " + Destination);
                sb.AppendLine("PollingIntervalMilliSec - " + PollingIntervalMilliSec);

                Console.Write(sb.ToString());
                Trace.TraceInformation(sb.ToString());

                if (Directory.Exists(FilePath))
                {
                    Trace.TraceInformation("Path exists - starting file watcher");
                    Console.WriteLine("Path exists - starting file watcher");

                    FileWatcher fw = new FileWatcher(FilePath, FileName, Destination, resourceLock);


                    while (fw.success == (int)ApplicationResponseTypes.FAILNOTFOUND)
                    {
                        lock (resourceLock)
                        {
                            Thread.Sleep(PollingIntervalMilliSec);
                            string logThis = ($"Watching for input file/ Waiting for file to be moved - {DateTime.Now}");
                            Console.WriteLine(logThis);
                            Trace.TraceInformation(logThis);
                        }
                    }

                    success = fw.success;
                }
                else
                {
                    Console.WriteLine("Path does not exist");
                    Trace.TraceInformation("Path does not exist");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Trace.TraceError(ex.ToString());
            }
        }
    }
}