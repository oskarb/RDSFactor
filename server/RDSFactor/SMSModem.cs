using System;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;

namespace RDSFactor
{
    public class SMSModem
    {
        private readonly SerialPort _serialPort;

        public SMSModem(string comPort)
        {
            _serialPort = new SerialPort
            {
                PortName = comPort,
                BaudRate = 38400,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.RequestToSend,
                DtrEnable = true,
                RtsEnable = true,
                NewLine = Environment.NewLine
            };
        }

        public bool Send(string cellNo, string sms, string SMSC)
        {
            string messages = sms;
            if (_serialPort.IsOpen)
            {
                try
                {
                    _serialPort.WriteLine("AT" + (char) 13);
                    Thread.Sleep(4);
                    _serialPort.WriteLine("AT+CSCA=\"" + SMSC + "\"" + (char) 13);
                    Thread.Sleep(30);
                    _serialPort.WriteLine(((char) 13).ToString());
                    Thread.Sleep(30);
                    _serialPort.WriteLine("AT+CMGS=\"" + cellNo + "\"");

                    Thread.Sleep(30);
                    _serialPort.WriteLine(messages + (char) 26);
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
            if (!_serialPort.IsOpen)
            {
                try
                {
                    // bool ok =this.serialPort.IsOpen //does not work between 2 treads

                    _serialPort.Open();
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
            if (_serialPort.IsOpen)
                _serialPort.Close();
        }
    }
}
