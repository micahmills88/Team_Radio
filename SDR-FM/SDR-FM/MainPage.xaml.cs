using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Networking;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Diagnostics;
using Windows.UI.ViewManagement;
using System.Numerics;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.System.Threading;
using Windows.UI.Input;

namespace SDR_FM
{
    public sealed partial class MainPage : Page
    {
        //RTL_TCP constants
        private const byte CMD_SET_FREQ = 0x1;
        private const byte CMD_SET_SAMPLE_RATE = 0x2;
        private const byte CMD_SET_TUNER_GAIN_MODE = 0x3;
        private const byte CMD_SET_GAIN = 0x4;
        private const byte CMD_SET_FREQ_COR = 0x5;
        private const byte CMD_SET_AGC_MODE = 0x8;
        private const byte CMD_SET_TUNER_GAIN_INDEX = 0xd;

        //RTL_TCP settings
        private const int DongleInfoLength = 12;
        private const string DefaultHost = "10.0.0.197";
        private const string DefaultPort = "1234";
        private const uint DefaultFrequency = 99100000;
        private const uint DefaultSampleRate = 1920000;
        private const uint DefaultGain = 10;
        private uint CurrentFrequency = DefaultFrequency;

        //network and data storage
        private StreamSocket sock;
        private StreamReader streamReader;
        private StreamWriter streamWriter;
        private bool socketIsConnected;
        private bool socketIsStreaming;
        private CircleBuffer<Complex> sampleBuffer;
        //private ThreadPoolTimer timer;
        private string message = "Hello World";

        //data processing
        private const int BUFFER_SIZE = 32768;
        private double[] realData;
        private double[] imagData;
        private double[] fftRealData;
        private double[] fftImagData;

        private FFT fft;


        public MainPage()
        {
            this.InitializeComponent();
            this.InitializeRadio();
            ApplicationView.PreferredLaunchViewSize = new Size(1280, 720);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private void InitializeRadio()
        {
            //timer = ThreadPoolTimer.CreatePeriodicTimer(timer_DoWork, TimeSpan.FromMilliseconds(100));
            socketIsConnected = false;
            socketIsStreaming = false;
            sampleBuffer = new CircleBuffer<Complex>(BUFFER_SIZE * 4);

            realData = new double[BUFFER_SIZE];
            imagData = new double[BUFFER_SIZE];
            fftRealData = new double[BUFFER_SIZE];
            fftImagData = new double[BUFFER_SIZE];
            fft = new FFT(BUFFER_SIZE);
        }
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            if (!socketIsConnected)
            {
                sock = new StreamSocket();
                socket_setup();
                Task t = Task.Run(() => receive_samples());
                startButton.Content = "Disconnect";
            }
            else
            {
                startButton.Content = "Connect";
            }
            socketIsConnected = !socketIsConnected;

        }

        private void socket_setup()
        {
            Task t = sock.ConnectAsync(new HostName(tcpDestinationBox.Text.Split(':')[0]), tcpDestinationBox.Text.Split(':')[1]).AsTask();
            t.Wait();

            streamReader = new StreamReader(sock.InputStream.AsStreamForRead());
            streamWriter = new StreamWriter(sock.OutputStream.AsStreamForWrite());

            char[] rtlType = new char[12];
            streamReader.Read(rtlType, 0, 12);
            Debug.WriteLine(new String(rtlType));

            streamWriter.BaseStream.Write(SendCommand(CMD_SET_SAMPLE_RATE, DefaultSampleRate), 0, 5);
            streamWriter.Flush();
            streamWriter.BaseStream.Write(SendCommand(CMD_SET_FREQ, CurrentFrequency - 960000), 0, 5);
            streamWriter.Flush();
            streamWriter.BaseStream.Write(SendCommand(CMD_SET_TUNER_GAIN_INDEX, DefaultGain), 0, 5);
            streamWriter.Flush();
        }

        private void receive_samples()
        {
            while (socketIsConnected)
            {
                int samplesRead = 0;
                byte[] samples = new byte[BUFFER_SIZE * 4];
                samplesRead = streamReader.BaseStream.Read(samples, 0, samples.Length);
                message = "Samples read:" + samplesRead;
                for (int i = 1; i < samplesRead; i += 2)
                {
                    sampleBuffer.AddValue(new Complex((float)samples[i - 1], (float)samples[i]));
                }
                socketIsStreaming = true;
            }

            if (!socketIsConnected)
            {
                //cleanup
                socketIsStreaming = false;
                streamWriter.Dispose();
                streamReader.Dispose();
                sock.Dispose();
            }
        }

        public byte[] SendCommand(byte cmd, byte[] val)
        {
            byte[] message = new byte[5];
            message[0] = cmd;
            message[1] = val[3]; //Network byte order
            message[2] = val[2];
            message[3] = val[1];
            message[4] = val[0];
            return message;
        }

        private byte[] SendCommand(byte cmd, UInt32 val)
        {
            byte[] valBytes = BitConverter.GetBytes(val);
            return SendCommand(cmd, valBytes);
        }

        private void CanvasAnimatedControl_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            int scroll = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
            if(socketIsStreaming)
            {
                if (scroll > 1)
                {
                    CurrentFrequency += 100000;
                }
                else if (scroll < 1)
                {
                    CurrentFrequency -= 100000;
                }
                socketIsStreaming = false;
                for (int i = 0; i < 768; i++)
                {
                    realData[i] = 0.0;
                    imagData[i] = 0.0;
                    fftRealData[i] = 0.0;
                    fftImagData[i] = 0.0;
                }
                streamWriter.BaseStream.Write(SendCommand(CMD_SET_FREQ, CurrentFrequency), 0, 5);
                streamWriter.Flush();
                socketIsStreaming = true;

            }
        }

        private void CanvasAnimatedControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            if(socketIsStreaming)
            {
                for (int i = 0; i < 768; i++)
                {
                    realData[i] = realData[i + 768];
                    imagData[i] = imagData[i + 768];
                    fftRealData[i] = realData[i];
                    fftImagData[i] = imagData[i];
                }

                for (int i = 768; i < BUFFER_SIZE; i++)
                {
                    Complex temp = sampleBuffer.GetValue();
                    realData[i] = temp.Real;
                    imagData[i] = temp.Imaginary;
                    fftRealData[i] = realData[i];
                    fftImagData[i] = imagData[i];
                }

                fft.TransformRadix2(fftRealData, fftImagData);

                args.DrawingSession.DrawText(message, 500, 20, Colors.Blue);

                CanvasPathBuilder pathBuilder = new CanvasPathBuilder(sender);
                pathBuilder.BeginFigure(0, 200);
                for (int i = 0; i < BUFFER_SIZE; i++)
                {
                    double powerSpectrum = (0.01 * Math.Sqrt(fftRealData[i] * fftRealData[i] + fftImagData[i] * fftImagData[i]));
                    pathBuilder.AddLine(i * 1200 / BUFFER_SIZE, (float)(200 - powerSpectrum));
                }
                pathBuilder.AddLine(1200, 200);
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                args.DrawingSession.FillGeometry(CanvasGeometry.CreatePath(pathBuilder), Colors.Black);
                args.DrawingSession.DrawLine(600, 0, 600, 300, Colors.Red);
            }
        }

    }
}
