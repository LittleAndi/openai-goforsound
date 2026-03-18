using NAudio.Dsp;

namespace Services;

public class FftProvider
{
    private readonly int _m;
    private readonly int _fftLength;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _window;
    private int _fftPos;

    public FftProvider(int m)
    {
        _m = m;
        _fftLength = 1 << m;
        _fftBuffer = new Complex[_fftLength];
        _window = new float[_fftLength];
        for (int i = 0; i < _fftLength; i++)
        {
            // Hanning window
            _window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / _fftLength)));
        }
    }

    public void Add(float[] buffer, int count)
    {
        for (int n = 0; n < count; n++)
        {
            if (_fftPos < _fftLength)
            {
                _fftBuffer[_fftPos].X = (float)(buffer[n] * _window[_fftPos]);
                _fftBuffer[_fftPos].Y = 0;
                _fftPos++;
            }
        }
    }

    public FrequencyResult[] GetFrequencies(int sampleRate)
    {
        FastFourierTransform.FFT(true, _m, _fftBuffer);
        var results = new FrequencyResult[_fftLength / 2];
        for (int n = 0; n < _fftLength / 2; n++)
        {
            double intensity = Math.Sqrt(_fftBuffer[n].X * _fftBuffer[n].X + _fftBuffer[n].Y * _fftBuffer[n].Y);
            results[n] = new FrequencyResult { Frequency = n * sampleRate / _fftLength, Intensity = intensity };
        }
        return results;
    }
}

public struct FrequencyResult
{
    public double Time { get; set; }
    public int Frequency { get; set; }
    public double Intensity { get; set; }
}
