using System;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;

namespace RDSFactor
{
    public class SMSModem
    {
        private SerialPort serialPort;

        public SMSModem(string comPort)
        {
            serialPort = new SerialPort();
            serialPort.PortName = comPort;
            serialPort.BaudRate = 38400;
            serialPort.Parity = Parity.None;
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Handshake = Handshake.RequestToSend;
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;
            serialPort.NewLine = System.Environment.NewLine;
        }

        public bool send(string cellNo, string sms, string SMSC)
        {
            string messages = sms;
            if (serialPort.IsOpen)
            {
                try
                {
                    serialPort.WriteLine("AT" + (char) 13);
                    Thread.Sleep(4);
                    serialPort.WriteLine("AT+CSCA=\"" + SMSC + "\"" + (char) 13);
                    Thread.Sleep(30);
                    serialPort.WriteLine(((char) 13).ToString());
                    Thread.Sleep(30);
                    serialPort.WriteLine("AT+CMGS=\"" + cellNo + "\"");

                    Thread.Sleep(30);
                    serialPort.WriteLine(messages + (char) 26);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Source);
                }

                return true;
            }

            return false;
        }

        public void Opens()
        {
            if (!serialPort.IsOpen)
            {
                try
                {
                    // bool ok =this.serialPort.IsOpen //does not work between 2 treads

                    serialPort.Open();
                }
                catch (Exception)
                {
                    Thread.Sleep(1000);
                    //wait for the port to get ready if 
                    Opens();
                }
            }
        }


        public void Closes()
        {
            if (serialPort.IsOpen)
                serialPort.Close();
        }
    }
}
