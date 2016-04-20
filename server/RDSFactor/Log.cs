using System;
using System.IO;

namespace LogFile
{
    public class LogWriter
    {
        public string filePath;
        private FileStream fileStream;
        private StreamWriter streamWriter;

        public void OpenFile()
        {
            try
            {
                string strPath = filePath;
                if (System.IO.File.Exists(strPath))
                    fileStream = new FileStream(strPath, FileMode.Append, FileAccess.Write);
                else
                    fileStream = new FileStream(strPath, FileMode.Create, FileAccess.Write);

                streamWriter = new StreamWriter(fileStream);
            }
            catch (Exception)
            {

            }
        }

        public void WriteLog(string strComments)
        {
            try
            {
                OpenFile();
                streamWriter.WriteLine(strComments);
                CloseFile();
            }
            catch (Exception)
            {
            }
        }

        public void CloseFile()
        {
            try
            {
                streamWriter.Close();
                fileStream.Close();
            }
            catch (Exception)
            {
            }
        }
    }
}
