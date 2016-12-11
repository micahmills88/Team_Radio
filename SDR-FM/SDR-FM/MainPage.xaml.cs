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
        private RadioInterface radioInterface;
        private SignalProcessor signalProcessor;
        private AudioPlayer audioPlayer;
        private FFTHandler fftHandler;

        public MainPage()
        {
            this.InitializeComponent();
            this.InitializeRadio();
            ApplicationView.PreferredLaunchViewSize = new Size(1280, 720);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private void InitializeRadio()
        {
            fftHandler = new FFTHandler(4096, 4096, 4096, 1024);
            radioInterface = new RadioInterface(fftHandler);
            signalProcessor = new SignalProcessor(radioInterface, fftHandler);
            audioPlayer = new AudioPlayer(signalProcessor, fftHandler);
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            if(!radioInterface.IsConnected)
            {
                //TODO see about resetting all buffers on disconnect and when tuning happens
                radioInterface.Connect(tcpDestinationBox.Text.Split(':')[0], tcpDestinationBox.Text.Split(':')[1]);
                radioInterface.StartSampleStream();
                startButton.Content = "Disconnect";

                signalProcessor.StartSignalProcessing();
                audioPlayer.StartPlayback();
                //fftHandler.Start();

            }
            else
            {
                audioPlayer.StopPlayback();
                signalProcessor.StopAndClearAudio();
                radioInterface.StopAndDisconnect();
                startButton.Content = "Connect";
                //fftHandler.Stop();
            }
        }

        private void CanvasAnimatedControl_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            int scroll = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
            if(radioInterface.IsStreaming)
            {
                if (scroll > 1)
                {
                    radioInterface.SetFrequency(radioInterface.Frequency + 100000);
                }
                else if (scroll < 1)
                {
                    radioInterface.SetFrequency(radioInterface.Frequency - 100000);
                }
            }
        }

        private void CanvasAnimatedControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            message = radioInterface.Frequency.ToString();
            //if (signalProcessor.SamplesAvailable)
            //{
            //    double[] fft = fftHandler.getRawFFT();
            //    CanvasPathBuilder pathBuilder = new CanvasPathBuilder(sender);
            //    pathBuilder.BeginFigure(0, 200);

            //    for(int i = 0; i < fft.Length; i++)
            //    {
            //        pathBuilder.AddLine(i * (float)(1200 / fft.Length), 200 - (float)fft[i]);
            //    }

            //    pathBuilder.AddLine(1200, 200);
            //    pathBuilder.EndFigure(CanvasFigureLoop.Closed);
            //    args.DrawingSession.FillGeometry(CanvasGeometry.CreatePath(pathBuilder), Colors.Black);
                args.DrawingSession.DrawLine(600, 0, 600, 300, Colors.Red);
                args.DrawingSession.DrawText(message, 100, 20, Colors.Blue);
            //}
        }

    }
}
