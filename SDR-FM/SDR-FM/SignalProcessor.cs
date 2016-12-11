using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDR_FM
{
    /*
     * Class that ingests a single buffer of complex samples
     * The samples are processed in the following order:
     * 1. The samples are filtered down to 200kHz
     * 2. The samples are 
     *
     */

    class SignalProcessor
    {
        private const int AUDIO_SAMPLE_RATE = 48000;
        private bool isStarted = false;

        private RadioInterface radio;
        private ConcurrentQueue<double[]> outSamples;

        private Task signalWorker;
        private Complex prevSample;

        private double[] filter;
        private Complex[] filterHistory;

        private double[] audioFilter;
        private double[] audioFilterHistory;
        

        public SignalProcessor(FFTHandler handler)
        {
            Initialize();
        }

        public int SamplesAvailable()
        {
            return outSamples.Count;
        }

        public void Initialize()
        {
            prevSample = Complex.Zero;

            audioFilter = CreateWindowedSincFilter(0.25, 7); //use0.25 or 0.125
            filter = CreateWindowedSincFilter(0.15, 3); //use 0.1 or 0.5

            filterHistory = new Complex[filter.Length];
            audioFilterHistory = new double[audioFilter.Length];

            outSamples = new ConcurrentQueue<double[]>();            
        }

        public void StartSignalProcessing()
        {
            isStarted = true;
            signalWorker = Task.Run(() => SignalProcessor_DoWork());
        }

        public void StopAndClearAudio()
        {
            isStarted = false;         
        }

        private void SignalProcessor_DoWork()
        {
            do
            {
                Complex[] inbound = null;
                do
                {
                    inbound = radio.GetSamples();
                } while (inbound == null);

                if (inbound != null)
                {
                    //apply filter
                    Complex[] filtered = ApplyFilter(inbound, filter, filterHistory);

                    //downsample to 192k
                    Complex[] decimated = Downsample(filtered);

                    //demodulate
                    double[] demodulated = Quadrature_Demodulation3(decimated);

                    //filter audio
                    double[] filteredAudio = ApplyFilter(demodulated, audioFilter, audioFilterHistory);

                    //downsample again
                    double[] audio = DownsampleAudio(filteredAudio);

                    //store in queue
                    outSamples.Enqueue(audio);
                }                

            } while (isStarted);

            outSamples = new ConcurrentQueue<double[]>();

        }

        public double[] GetAudioSamples()
        {
            double[] temp = null;
            outSamples.TryDequeue(out temp);
            return temp;
        }

        private double[] CreateWindowedSincFilter(double frequencyCutoff, int taps)
        {
            double[] sincFilter = new double[taps];
            double M = taps;
            double FC = frequencyCutoff;
            double sum = 0;

            for (int i = 0; i < sincFilter.Length; i++)
            {
                if ((i - M / 2) == 0)
                {
                    sincFilter[i] = (float)(2.0 * Math.PI * FC);
                }
                else
                {
                    sincFilter[i] = (float)(Math.Sin(2.0 * Math.PI * FC * (i - M / 2.0)) / (i - M / 2));
                }
                //multiply by hamming window
                sincFilter[i] *= (float)(0.54 - 0.46 * Math.Cos(2.0 * Math.PI * i / M));
                sum += sincFilter[i];
            }

            for (int i = 0; i < sincFilter.Length; i++)
            {
                sincFilter[i] /= (float)sum;
            }

            return sincFilter;
        }

        private double[] ApplyFilter(double[] signal, double[] filter, double[] history)
        {
            double[] filteredSignal = new double[signal.Length];
            double[] input = new double[signal.Length + history.Length];

            for (int i = 0; i < history.Length; i++)
            {
                input[i] = history[i];
                history[i] = signal[signal.Length - filter.Length + i];
            }

            for (int i = history.Length; i < input.Length; i++)
            {
                input[i] = signal[i - history.Length];
            }

            for (int i = filter.Length; i < input.Length; i++)
            {
                filteredSignal[i - filter.Length] = 0;
                for (int j = 0; j < filter.Length; j++)
                {
                    filteredSignal[i - filter.Length] += input[i - j] * filter[j];
                }
            }
            return filteredSignal;
        }

        private Complex[] ApplyFilter(Complex[] signal, double[] filter, Complex[] history)
        {
            Complex[] filteredSignal = new Complex[signal.Length];
            Complex[] input = new Complex[signal.Length + history.Length];

            for(int i = 0; i < history.Length; i++)
            {
                input[i] = history[i];
                history[i] = signal[signal.Length - filter.Length + i];
            }

            for(int i = history.Length; i < input.Length; i++)
            {
                input[i] = signal[i - history.Length];
            }

            for (int i = filter.Length; i < input.Length; i++)
            {
                filteredSignal[i - filter.Length] = 0;
                for (int j = 0; j < filter.Length; j++)
                {
                    filteredSignal[i - filter.Length] += input[i - j] * filter[j];
                }
            }
            return filteredSignal;
        }

        private Complex[] Downsample(Complex[] samples, int decimation = 10)
        {
            //assume time divisions of 100th of a second
            //assuming buffer size of 19,200
            Complex[] downSampled = new Complex[samples.Length / decimation];
            for(int i = 0; i < downSampled.Length; i++)
            {
                downSampled[i] = samples[i * decimation];
            }
            return downSampled;
        }

        private double[] DownsampleAudio(double[] samples, int decimation = 4)
        {
            //assuming sample rate of of 1,920,000
            double[] downSampled = new double[samples.Length / decimation];
            for(int i = 0; i < downSampled.Length; i++)
            {
                downSampled[i] = samples[i * decimation];
            }
            return downSampled;
        }

        private double[] Quadrature_Demodulation3(Complex[] samples)
        {
            //traditional atan2 demodulator with phase tracking
            double currentPhase;
            double deltaPhase;

            double[] demodSamples = new double[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                currentPhase = samples[i].Phase;
                deltaPhase = currentPhase - prevSample.Phase;
                if (deltaPhase < -Math.PI)
                {
                    deltaPhase += Math.PI * 2.0;
                }
                if(deltaPhase > Math.PI)
                {
                    deltaPhase -= Math.PI * 2.0;
                }
                demodSamples[i] = deltaPhase / Math.PI;
                prevSample = samples[i];
                
            }
            return demodSamples;
        }

        private double[] Quadrature_Demodulation2(Complex[] samples)
        {
            //Taken from "Understanding Digital Signal Processing" by Richard G. Lyons (p.760)
            double[] demodSamples = new double[samples.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                double di = samples[i].Real - prevSample.Real;
                double dq = samples[i].Imaginary - prevSample.Imaginary;

                double numerator = (samples[i].Real * dq) - (samples[i].Imaginary * di);
                double denominator = (samples[i].Real * samples[i].Real + samples[i].Imaginary * samples[i].Imaginary);

                if(denominator == 0)
                {
                    demodSamples[i] = 0.0;
                }
                else
                {
                    demodSamples[i] = Math.PI * numerator / denominator;
                }

                prevSample = samples[i];
            }
            return demodSamples;
        }

        private double[] Quadrature_Demodulation(Complex[] samples)
        {
            //GNU Radio Quadrature_Demod block implemented in c#
            double[] demodSamples = new double[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                Complex temp = samples[i] * Complex.Conjugate(prevSample);
                Complex normal = temp / temp.Magnitude;
                demodSamples[i] = normal.Phase; //multiply gain here for volume
                prevSample = samples[i];
            }
            return demodSamples;
        }

    }
}
