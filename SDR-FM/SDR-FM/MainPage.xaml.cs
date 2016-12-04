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
        private string message = "";
        private RadioInterface radio;

        //data processing
        private const int BUFFER_SIZE = 32768;
        private double[] realData;
        private double[] imagData;
        private double[] fftRealData;
        private double[] fftImagData;
        private double[] hannWindow;
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
            radio = new RadioInterface();
            hannWindow = new double[BUFFER_SIZE];
            realData = new double[BUFFER_SIZE];
            imagData = new double[BUFFER_SIZE];
            fftRealData = new double[BUFFER_SIZE];
            fftImagData = new double[BUFFER_SIZE];
            fft = new FFT(BUFFER_SIZE);

            for(int i = 0; i < BUFFER_SIZE; i++)
            {
                hannWindow[i] = (float)(0.5f * (1.0f - Math.Cos((2.0f * Math.PI * (i + 1)) / (BUFFER_SIZE - 1))));
            }

        }
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            if(!radio.IsConnected)
            {
                //TODO see about resetting all buffers on disconnect and when tuning happens
                radio.Connect(tcpDestinationBox.Text.Split(':')[0], tcpDestinationBox.Text.Split(':')[1]);
                radio.StartSampleStream();
                startButton.Content = "Disconnect";
            }
            else
            {
                radio.StopAndDisconnect();
                startButton.Content = "Connect";
            }
        }

        private void CanvasAnimatedControl_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            int scroll = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
            if(radio.IsStreaming)
            {
                if (scroll > 1)
                {
                    radio.SetFrequency(radio.Frequency + 100000);
                }
                else if (scroll < 1)
                {
                    radio.SetFrequency(radio.Frequency - 100000);
                }
            }
        }

        private void CanvasAnimatedControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            if(radio.IsStreaming && radio.SamplesAvailable)
            {
                Complex[] temp = radio.GetSamples();
                message = temp.Length.ToString();

                for(int i = 0; i < 768; i++)
                {
                    int index = i + temp.Length;
                    realData[i] = realData[index];
                    imagData[i] = imagData[index];
                    fftRealData[i] = realData[i];
                    fftImagData[i] = imagData[i];
                }

                for(int i = 0; i < temp.Length; i++)
                {
                    int index = i + 768;
                    realData[index] = temp[i].Real;
                    imagData[index] = temp[i].Imaginary;
                    fftRealData[index] = realData[index];
                    fftImagData[index] = imagData[index];
                }

                
                fft.TransformRadix2(fftRealData, fftImagData);

                args.DrawingSession.DrawText(message, 500, 20, Colors.Blue);

                CanvasPathBuilder pathBuilder = new CanvasPathBuilder(sender);
                pathBuilder.BeginFigure(0, 200);
                for (int i = 0; i < BUFFER_SIZE; i++)
                {
                    double powerSpectrum = (0.01 * Math.Sqrt(fftRealData[i] * fftRealData[i] + fftImagData[i] * fftImagData[i]));
                    pathBuilder.AddLine(i * ((float)1200 / (float)BUFFER_SIZE), (float)(200 - powerSpectrum));
                }
                pathBuilder.AddLine(1200, 200);
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                args.DrawingSession.FillGeometry(CanvasGeometry.CreatePath(pathBuilder), Colors.Black);
                args.DrawingSession.DrawLine(600, 0, 600, 300, Colors.Red);
            }
        }

    }
}
