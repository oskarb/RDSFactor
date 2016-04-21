using System;
using System.IO;

namespace RDSFactor
{
    public class LogWriter
    {
        private readonly string _filePath;
        private FileStream _fileStream;
        private StreamWriter _streamWriter;


        public LogWriter(string logfilePath)
        {
            _filePath = logfilePath;
        }


        private void OpenFile()
        {
            try
            {
                string strPath = _filePath;
                _fileStream = File.Exists(strPath)
                    ? new FileStream(strPath, FileMode.Append, FileAccess.Write)
                    : new FileStream(strPath, FileMode.Create, FileAccess.Write);

                _streamWriter = new StreamWriter(_fileStream);
            }
            catch (Exception)
            {
                // ignored, because we don't want exceptions writing the log file to disturb the actual function.
            }
        }


        public void WriteLog(string strComments)
        {
            try
            {
                OpenFile();

                _streamWriter?.WriteLine(strComments);

            }
            catch (Exception)
            {
                // ignored, because we don't want exceptions writing the log file to disturb the actual function.
            }
            finally
            {
                CloseFile();
            }
        }


        private void CloseFile()
        {
            try
            {
                _streamWriter?.Close();
                _streamWriter = null;

                _fileStream?.Close();
                _fileStream = null;
            }
            catch (Exception)
            {
                // ignored, because we don't want exceptions writing the log file to disturb the actual function.
            }
        }
    }
}
