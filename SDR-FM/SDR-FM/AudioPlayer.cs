using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.UI.Popups;

namespace SDR_FM
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]

    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    class AudioPlayer
    {
        private string message = "";
        private AudioGraph graph;
        private AudioDeviceOutputNode deviceOutputNode;
        private AudioFrameInputNode frameInputNode;

        private SignalProcessor processor;
        private FFTHandler fftHandler;
        private bool isStarted = false;

        public enum ChannelType
        {
            Mono, Stereo
        }

        public AudioPlayer(SignalProcessor inProcessor, FFTHandler handler)
        {
            processor = inProcessor;
            Initialize();
            message = "we started...";
        }

        private void Initialize()
        {
            CreateAudioGraph();   
        }
        public string Message
        {
            get { return message; }
        }

        private async void CreateAudioGraph()
        {
            try
            {
                // Create an AudioGraph with default settings
                AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
                //settings.PrimaryRenderDevice = device; //TODO make this work
                settings.DesiredSamplesPerQuantum = 480;
                settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;
                CreateAudioGraphResult audioGraphResult = await AudioGraph.CreateAsync(settings);
                if (audioGraphResult.Status == AudioGraphCreationStatus.Success)
                {
                    graph = audioGraphResult.Graph;
                }
                else
                {
                    throw new Exception("Audio graph bad");
                }

                //create frame input node
                AudioEncodingProperties nodeEncodingProperties = graph.EncodingProperties;
                nodeEncodingProperties.ChannelCount = 1;
                frameInputNode = graph.CreateFrameInputNode(nodeEncodingProperties);
                frameInputNode.Stop();
                frameInputNode.QuantumStarted += AudioGraph_InputQuantumStarted;
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message + " " + ex.Source + " " + ex.Data + " " + ex.InnerException).ShowAsync();
            }
            CreateOutputDevice();
        }

        private async void CreateOutputDevice()
        {
            try
            {
                //Create a device input node using selected audio input device
                CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await graph.CreateDeviceOutputNodeAsync();
                if (deviceOutputNodeResult.Status == AudioDeviceNodeCreationStatus.Success)
                {
                    deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;
                }
                else
                {
                    throw new Exception("device input bad");
                }
            }
            catch (Exception ex)
            {
                await new MessageDialog(ex.Message + " " + ex.Source + " " + ex.Data + " " + ex.InnerException).ShowAsync();
            }

            //add the audio device and the frame node as outputs to the input device
            frameInputNode.AddOutgoingConnection(deviceOutputNode);
        }

        private void AudioGraph_InputQuantumStarted(AudioFrameInputNode sender, FrameInputNodeQuantumStartedEventArgs args)
        {
            message = args.RequiredSamples.ToString();
            uint numSamplesNeeded = (uint)args.RequiredSamples;
            if (numSamplesNeeded != 0 && processor.SamplesAvailable() > 0)
            {
                AudioFrame audioData = QueueAudioSamples(numSamplesNeeded);
                frameInputNode.AddFrame(audioData);
            }
        }

        unsafe private AudioFrame QueueAudioSamples(uint sampleCount)
        {
            double[] audioSamples;
            do
            {
                audioSamples = processor.GetAudioSamples();
            } while (audioSamples == null);
            
            uint bufferSize = sampleCount * sizeof(float);
            AudioFrame frame = new AudioFrame(bufferSize);

            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                byte* dataInBytes;
                uint capacityInBytes;
                float* dataInFloat;

                // Get the buffer from the AudioFrame
                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacityInBytes);

                // Cast to float since the data we are generating is float
                dataInFloat = (float*)dataInBytes;
                int sampleRate = (int)graph.EncodingProperties.SampleRate;

                for (int i = 0; i < sampleCount; i++)
                {
                    dataInFloat[i] = (float)audioSamples[i];
                }
            }

            return frame;
        }

        public bool IsStarted
        {
            get { return isStarted; }
        }

        public void SetChannelType(ChannelType channels)
        {
            //TODO: implement stereo channel detection in SignalProcessor class
            //see if we can call this method automatically based on detection
        }

        public void StartPlayback()
        {
            if (!isStarted)
            {
                frameInputNode.Start();
                graph.Start();

                isStarted = true;
            }
        }

        public void StopPlayback()
        {
            frameInputNode.Stop();
            graph.Stop();
        }

        public void SetVolume(int volume)
        {

        }

    }
}
