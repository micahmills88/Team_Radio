using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.System.Threading;

namespace SDR_FM
{
    class RadioInterface
    {
        //RTL_TCP command constants
        private enum RadioCommands
        {
            SET_FREQ = 0x1,
            SET_SAMPLE_RATE = 0x2,
            SET_TUNER_GAIN_MODE = 0x3,
            SET_GAIN = 0x4,
            SET_FREQ_COR = 0x5,
            SET_AGC_MODE = 0x8,
            SET_TUNER_GAIN_INDEX = 0xd
        }

        private Task samplesWorker;

        //storage of samples
        private ConcurrentQueue<Complex[]> sampleBuffers;
        private ConcurrentQueue<Complex[]> fftBuffers;

        //RTL_TCP settings
        private const int DongleInfoLength = 12;
        private const uint DefaultFrequency = 99100000;
        private const uint DefaultSampleRate = 1920000;
        private const uint DefaultGain = 9;

        private uint currentFrequency = DefaultFrequency;
        private uint currentGain = DefaultGain;

        //class variables
        private const int STREAM_BUFFER_LENGTH = 1024000;
        private StreamSocket socket;
        private StreamReader streamReader;
        private StreamWriter streamWriter;
        private bool isConnected = false;
        private bool isStreaming = false;
        private bool canWrite = true;
        private string rtlName;

        public RadioInterface()
        {

        }

        public void Connect(String IPAddress, string port)
        {
            if(!isConnected)
            {
                sampleBuffers = new ConcurrentQueue<Complex[]>();
                fftBuffers = new ConcurrentQueue<Complex[]>();

                socket = new StreamSocket();
                Task t = socket.ConnectAsync(new HostName(IPAddress), port).AsTask();
                t.Wait();

                streamReader = new StreamReader(socket.InputStream.AsStreamForRead());
                streamWriter = new StreamWriter(socket.OutputStream.AsStreamForWrite());

                char[] rtlType = new char[12];
                streamReader.Read(rtlType, 0, 12);
                rtlName = new string(rtlType);

                SetSampleRate(DefaultSampleRate);
                SetFrequency(DefaultFrequency);
                SetGain(DefaultGain);
            }
            isConnected = !isConnected;
        }

        public bool IsConnected
        {
            get { return isConnected; }
        }

        public bool IsStreaming
        {
            get { return isStreaming; }
        }

        public uint Frequency
        {
            get { return currentFrequency; }
        }

        public uint Gain
        {
            get { return currentGain; }
        }

        public uint SampleRate
        {
            get { return DefaultSampleRate; }
        }

        public string GetRTLName()
        {
            if(String.IsNullOrEmpty(rtlName))
            {
                return "Not Connected";
            }
            else
            {
                return rtlName;
            }
        }

        private byte[] SendCommand(RadioCommands cmd, byte[] value)
        {
            byte[] networkBytes = new byte[5];
            networkBytes[0] = (byte)cmd;
            networkBytes[1] = value[3];
            networkBytes[2] = value[2];
            networkBytes[3] = value[1];
            networkBytes[4] = value[0];
            return networkBytes;
        }

        private byte[] SendCommand(RadioCommands cmd, UInt32 value)
        {
            byte[] uintBytes = BitConverter.GetBytes(value);
            return SendCommand(cmd, uintBytes);
        }

        public void SetFrequency(uint frequency)
        {
            currentFrequency = frequency;
            streamWriter.BaseStream.Write(SendCommand(RadioCommands.SET_FREQ, frequency), 0, 5);
            streamWriter.Flush();
        }

        public void SetSampleRate(uint rate)
        {
            streamWriter.BaseStream.Write(SendCommand(RadioCommands.SET_SAMPLE_RATE, rate), 0, 5);
            streamWriter.Flush();
        }

        public void SetGain(uint gain)
        {
            streamWriter.BaseStream.Write(SendCommand(RadioCommands.SET_TUNER_GAIN_INDEX, gain), 0, 5);
            streamWriter.Flush();
        }

        public void StartSampleStream()
        {
            samplesWorker = Task.Run(()=> Radio_DoWork());        
        }

        public void StopAndDisconnect()
        {            
            isStreaming = false;
            isConnected = false;
        }

        public bool SamplesAvailable
        {
            get { return !sampleBuffers.IsEmpty; }
        }

        public int SamplesFFTAvailable
        {
            get { return fftBuffers.Count; }
        }

        public int GetBufferCount()
        {
            return sampleBuffers.Count;
        }

        public Complex[] GetSamples()
        {
            Complex[] temp = null;
            sampleBuffers.TryDequeue(out temp);
            return temp;
        }

        public Complex[] GetFFTSamples()
        {
            Complex[] temp = null;
            fftBuffers.TryDequeue(out temp);
            return temp;
        }

        private void Radio_DoWork()
        {
            if (!isConnected)
                return;
            do
            {
                int samplesRead = 0;
                byte[] samples = new byte[(DefaultSampleRate / 100) * 2];
                
                while (samplesRead != samples.Length)
                {
                    samplesRead += streamReader.BaseStream.Read(samples, samplesRead, samples.Length - samplesRead);
                }
                Complex[] temp = new Complex[samples.Length / 2];
                for (int i = 0; i < temp.Length; i++)
                {
                    int index = i * 2;
                    temp[i] = new Complex((samples[index] / (Byte.MaxValue / 2.0) - 1.0), (samples[index + 1] / (Byte.MaxValue / 2.0) - 1.0)); //swapped iq
                }

                if(canWrite)
                {
                    sampleBuffers.Enqueue(temp);
                    fftBuffers.Enqueue(temp);
                }

                isStreaming = true;
            }
            while (isConnected);

            streamWriter.Dispose();
            streamReader.Dispose();
            socket.Dispose();
        }

    }
}
