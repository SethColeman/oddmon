using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Oddmon.Core;

/// <summary>
/// Plays overlapping seek clicks while the disk is busy. The NAudio mixer
/// handles overlap; a timer ticks clicks at a steady cadence. See scope §3.4.
/// </summary>
public sealed class SeekSoundPlayer : IDisposable
{
    private readonly MixingSampleProvider _mixer;
    private readonly WaveOutEvent _output;
    private readonly WaveFormat _format;
    private readonly float[] _click;
    private readonly Func<bool> _isBusy;
    private readonly System.Timers.Timer _timer;

    /// <param name="isBusy">Returns true while the disk is active (drives clicks).</param>
    /// <param name="clickIntervalMs">Cadence of clicks while busy.</param>
    public SeekSoundPlayer(Func<bool> isBusy, int sampleRate = 44100, double clickIntervalMs = 110.0)
    {
        _isBusy = isBusy;
        _format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _click = ClickSynth.Generate(sampleRate);

        _mixer = new MixingSampleProvider(_format) { ReadFully = true };
        _output = new WaveOutEvent();
        _output.Init(_mixer);

        // ponytail: fixed cadence while busy, not true per-I/O rate scaling.
        // Add rate-scaled chatter (ETW or counter-rate) if it feels too uniform (scope §3.4).
        _timer = new System.Timers.Timer(clickIntervalMs) { AutoReset = true };
        _timer.Elapsed += (_, _) => { if (_isBusy()) PlayClick(); };
    }

    public void Start()
    {
        _output.Play();
        _timer.Start();
    }

    public void PlayClick() => _mixer.AddMixerInput(new OneShot(_click, _format));

    public void Dispose()
    {
        _timer.Dispose();
        _output.Dispose();
    }

    // Reads a sample buffer once, then ends — the mixer drops it automatically.
    private sealed class OneShot(float[] data, WaveFormat format) : ISampleProvider
    {
        private int _pos;
        public WaveFormat WaveFormat => format;

        public int Read(float[] buffer, int offset, int count)
        {
            int n = Math.Min(count, data.Length - _pos);
            Array.Copy(data, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }
    }
}
