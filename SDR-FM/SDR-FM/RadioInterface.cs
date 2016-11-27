using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDR_FM
{
    class RadioInterface
    {
        public enum RadioCommands
        {
            SET_FREQ = 0x1,
            SET_SAMPLE_RATE = 0x2,
            SET_TUNER_GAIN_MODE = 0x3,
            SET_GAIN = 0x4,
            SET_FREQ_COR = 0x5,
            SET_AGC_MODE = 0x8,
            SET_TUNER_GAIN_INDEX = 0xd
        }
        public RadioInterface()
        {

        }

        public void Connect(String IPAddress, string port)
        {

        }

        public void Initialize()
        {

        }

        public void SetFrequency(double frequency)
        {

        }

        public void SetSampleRate(double rate)
        {

        }

        public void SetGain(int gain)
        {

        }

        public void StartSampleStream()
        {

        }
    }
}
