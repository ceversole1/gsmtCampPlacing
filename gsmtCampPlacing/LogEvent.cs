using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace gsmtCampPlacing
{
    internal sealed class LogEvent
    {
        public static void Log(string str)
        {
            using (StreamWriter w = File.AppendText(myLogLocation() + @"\log.txt"))
            {
                Log(str, w);
                Console.WriteLine(str);
            }            
        }

        public static void Log(string str, TextWriter w)
        {
            w.Write("Log Entry | ");
            w.WriteLine("{0} {1} | {2}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString(), str);           
        }

        public static void DumpLog(StreamReader r)
        {
            string line;
            while ((line = r.ReadLine()) != null)
            {
                Console.WriteLine(line);
            }
        }

        private static string myLocation()
        {
            string DirectoryPath = Directory.GetCurrentDirectory();

            return DirectoryPath.ToString();
        }

        private static string myLogLocation()
        {
            string logLocation = myLocation() + @"\Logs";
            //check if a log folder
            if (!IsDirectoryExists(logLocation))
            {
                //directory doesn't exists, create one
                Directory.CreateDirectory(logLocation);
            }
            return logLocation;
        }

        private static bool IsDirectoryExists(string DirectoryName)
        {
            // Return the Exists property value.
            return Directory.Exists(DirectoryName);

        }
    }
}
