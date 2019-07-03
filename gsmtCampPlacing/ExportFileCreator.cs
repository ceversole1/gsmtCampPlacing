using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace gsmtCampPlacing
{
    internal sealed class ExportFileCreator
    {
        public string FullPath { get { return _fullpath; } }

        private string _fullpath;

        public void Write(string str, string fileName)
        {
            _fullpath = myFileLocation() + String.Format(@"\{0}", fileName);

            using (StreamWriter w = File.AppendText(_fullpath))
            {
                WriteToFile(str, w);
            }            
        }

        public void WriteToFile(string str, TextWriter w)
        {            
            w.WriteLine(str);
        }
               
        private string myLocation()
        {
            string DirectoryPath = Directory.GetCurrentDirectory();

            return DirectoryPath.ToString();
        }

        private string myFileLocation()
        {
            string exportFileLocation = myLocation() + @"\ExportFiles";
            //check if a log folder
            if (!IsDirectoryExists(exportFileLocation))
            {
                //directory doesn't exists, create one
                Directory.CreateDirectory(exportFileLocation);
            }
            return exportFileLocation;
        }

        private bool IsDirectoryExists(string DirectoryName)
        {
            // Return the Exists property value.
            return Directory.Exists(DirectoryName);

        }
    }
}
