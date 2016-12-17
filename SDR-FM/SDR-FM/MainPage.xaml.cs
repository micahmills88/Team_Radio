using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.ViewManagement;
using Windows.System.Threading;
using System.Numerics;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas.Geometry;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace SDR_FM
{
    public sealed partial class MainPage : Page
    {
        private ThreadPoolTimer fftTimer;
        private string message = "";
        private RadioInterface radioInterface;
        private SignalProcessor signalProcessor;
        private AudioPlayer audioPlayer;
        private bool startTimer = false;
        private FFTWrapper fftWrapper;

        private Point pointer;
        private String stationValue = "";

        public MainPage()
        {
            this.InitializeComponent();
            this.InitializeRadio();
            ApplicationView.PreferredLaunchViewSize = new Size(1280, 720);
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(1280, 720));
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private void InitializeRadio()
        {
            radioInterface = new RadioInterface();
            signalProcessor = new SignalProcessor(radioInterface);
            audioPlayer = new AudioPlayer(signalProcessor);
            fftWrapper = new FFTWrapper(32768, 768);
            
        }

        private void runFFT(ThreadPoolTimer timer)
        {
            if(startTimer)
            {
                Complex[] samples = radioInterface.GetFFTSamples();
                if (samples != null)
                {
                    for (int i = 0; i < samples.Length; i++)
                    {
                        fftWrapper.push(samples[i]);
                    }
                }
            }
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

                startTimer = true;
                //Task.Run(() => runFFT());
                fftTimer = ThreadPoolTimer.CreatePeriodicTimer(runFFT, TimeSpan.FromMilliseconds(10));

            }
            else
            {
                audioPlayer.StopPlayback();
                signalProcessor.StopAndClearAudio();
                radioInterface.StopAndDisconnect();
                startButton.Content = "Connect";
                startTimer = false;
            }
        }        

        private void CanvasAnimatedControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            message = radioInterface.Frequency.ToString();
            if (radioInterface.IsStreaming)
            {
                double[] fft = fftWrapper.GetFFTDisplayData();

                if (fft == null)
                {
                    fft = new double[1200];
                }
                CanvasPathBuilder pathBuilder = new CanvasPathBuilder(sender);
                pathBuilder.BeginFigure(0, 280);

                for (int i = 0; i < fft.Length; i++)
                {
                    float xValue = i * (float)((float)1200 / (float)fft.Length);
                    float yValue = 280 - ((float)fft[i]);
                    pathBuilder.AddLine(xValue, yValue);
                }

                pathBuilder.AddLine(1200, 280);
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                args.DrawingSession.FillGeometry(CanvasGeometry.CreatePath(pathBuilder), Colors.Black);
                Color box = Colors.LightGreen;
                box.A = 25;
                args.DrawingSession.FillRectangle(540, 0, 120, 300, box);

                args.DrawingSession.DrawLine((float)pointer.X, 0, (float)pointer.X, 300, Colors.Red);
                args.DrawingSession.DrawText(stationValue, (float)pointer.X + 20, (float)(pointer.Y - 25), Colors.Blue);

            }
        }

        private Point GetPointerPosition()
        {
            CoreWindow currentWindow = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow;
            Point point;
            try
            {
                point = currentWindow.PointerPosition;
            }
            catch(Exception ex)
            {
                return new Point(-100, -100);
            }

            Rect bounds = currentWindow.Bounds;
            return new Point(point.X - bounds.X - 40, point.Y - bounds.Y - 40);
        }

        private void CanvasAnimatedControl_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            int scroll = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
            if (radioInterface.IsStreaming)
            {
                if (scroll > 1 && radioInterface.Frequency < 109000000)
                {
                    radioInterface.SetFrequency(radioInterface.Frequency + 100000);
                }
                else if (scroll < 1 && radioInterface.Frequency > 87000000)
                {
                    radioInterface.SetFrequency(radioInterface.Frequency - 100000);
                }
                tunerBox.Text = (radioInterface.Frequency / 1000000.0).ToString() + " Mhz";
            }
        }

        private void tunerBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TuneRadio();
        }

        private void tunerBox_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                TuneRadio();
            }
        }

        private void TuneRadio()
        {
            double number = radioInterface.Frequency;
            String text = tunerBox.Text.ToUpper();
            if (text.Contains("MHZ"))
            {
                number = Double.Parse(text.ToUpper().Split('M')[0].Trim());
            }
            else if (!String.IsNullOrWhiteSpace(text))
            {
                double temp;
                if (Double.TryParse(text, out temp))
                {
                    number = temp;
                }
            }

            if (number < 110)
                number *= 1000000;

            if (number < 109000000 && number > 87000000)
            {
                radioInterface.SetFrequency((uint)number);
            }

            tunerBox.Text = (radioInterface.Frequency / 1000000.0).ToString() + " Mhz";
        }

        private void CanvasAnimatedControl_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            pointer = GetPointerPosition();
            double station = ((double)radioInterface.Frequency + GetPointerStationValue()) / 1000000.0;
            stationValue = String.Format("{0} Mhz", station);
        }

        private double GetPointerStationValue()
        {
            Point temp = GetPointerPosition();
            double value = (temp.X - 600) * 1600;
            double remainder = value % 100000;
            if (value > 0)
            {
                if (Math.Abs(remainder) > 50000)
                {
                    value = value - remainder + 100000;
                }
                else
                {
                    value = value - remainder;
                }
            }
            else
            {
                if (Math.Abs(remainder) > 50000)
                {
                    value = value - remainder - 100000;
                }
                else
                {
                    value = value - remainder;
                }
            }
            return value;
        }

        private void CanvasAnimatedControl_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            pointer = new Point(double.NegativeInfinity, double.NegativeInfinity);
        }

        private void CanvasAnimatedControl_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            double temp = GetPointerStationValue();
            radioInterface.SetFrequency((uint)(radioInterface.Frequency + temp));
            tunerBox.Text = (radioInterface.Frequency / 1000000.0).ToString() + " Mhz";
        }

        private void tuneUpButton_Click(object sender, RoutedEventArgs e)
        {
            radioInterface.SetFrequency((uint)(radioInterface.Frequency + 100000));
            tunerBox.Text = (radioInterface.Frequency / 1000000.0).ToString() + " Mhz";
        }

        private void tuneDownButton_Click(object sender, RoutedEventArgs e)
        {
            radioInterface.SetFrequency((uint)(radioInterface.Frequency - 100000));
            tunerBox.Text = (radioInterface.Frequency / 1000000.0).ToString() + " Mhz";
        }

        private void volumeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if(startTimer)
            {
                double value = volumeSlider.Value;
                audioPlayer.SetVolume((float)value / 5.0f);
            }
        }
    }
}
