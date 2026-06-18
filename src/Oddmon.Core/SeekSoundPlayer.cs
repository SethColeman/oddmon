using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Oddmon.Core;

/// <summary>
/// Plays disk sounds while the disk is busy, muted during calls / manual mute.
/// A real sound set (recorded WAVs) loops while busy; with no set it falls back
/// to synthesized one-shot clicks. See scope §3.4.
/// </summary>
public sealed class SeekSoundPlayer : IDisposable
{
    private readonly MixingSampleProvider _mixer;
    private readonly VolumeSampleProvider _master;
    private IWavePlayer _output = null!; // assigned by RebuildOutput in the ctor
    private MMDevice? _device;
    private bool _started;
    private readonly WaveFormat _format;
    private readonly Func<bool> _isBusy;
    private readonly System.Timers.Timer _timer;

    // One of these is active depending on whether a real sound set loaded.
    private readonly VolumeSampleProvider? _busyGate; // sound-set: loop, gated by busy
    private readonly float[][]? _clicks;              // synth: discrete one-shots
    private readonly Random _rng = new();

    public bool Enabled { get; set; } = true;

    public float Volume
    {
        get => _master.Volume;
        set => _master.Volume = Math.Clamp(value, 0f, 1f);
    }

    /// <param name="isBusy">Returns true while the disk is active.</param>
    /// <param name="volume">Master volume 0–1.</param>
    /// <param name="soundSetDir">Folder of WAV clips; null/empty falls back to synth clicks.</param>
    /// <param name="outputDevice">Output device name (partial match); null/blank uses Windows default.</param>
    public SeekSoundPlayer(Func<bool> isBusy, float volume = 0.3f, int sampleRate = 44100,
        double clickIntervalMs = 85.0, string? soundSetDir = null, string? outputDevice = null)
    {
        _isBusy = isBusy;
        _format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _mixer = new MixingSampleProvider(_format) { ReadFully = true };

        var set = SoundSet.Load(soundSetDir, sampleRate);
        if (set.Length > 0)
        {
            // Real recording: loop it continuously, gate the volume on disk activity.
            // ponytail: hard 0/1 gate may click on transitions; add a short ramp if audible.
            _busyGate = new VolumeSampleProvider(new LoopSampleProvider(set, _format)) { Volume = 0f };
            _mixer.AddMixerInput(_busyGate);
        }
        else
        {
            double[] freqs = { 1100, 1400, 1750, 2200 };
            _clicks = freqs.Select((f, i) => ClickSynth.Generate(sampleRate, f, 30, seed: i + 1)).ToArray();
        }

        _master = new VolumeSampleProvider(_mixer) { Volume = Math.Clamp(volume, 0f, 1f) };
        RebuildOutput(outputDevice);

        _timer = new System.Timers.Timer(clickIntervalMs) { AutoReset = true };
        _timer.Elapsed += (_, _) => Tick();
    }

    /// <summary>Switch playback to <paramref name="deviceName"/> (null/blank/missing = Windows default).
    /// Rebuilds only the output sink; the mixer and providers are untouched.</summary>
    public void SetOutputDevice(string? deviceName) => RebuildOutput(deviceName);

    private void RebuildOutput(string? deviceName)
    {
        // ponytail: no lock around the swap; a tray utility's rare device change can tolerate a brief gap.
        _output?.Stop();
        _output?.Dispose();
        _device?.Dispose();
        _device = AudioOutputs.Resolve(deviceName);

        if (_device is null)
        {
            _output = new WaveOutEvent();        // Windows default device
            _output.Init(_master);               // WaveOut accepts the mono-float master directly
        }
        else
        {
            try
            {
                var mix = _device.AudioClient.MixFormat; // device shared-mode format
                ISampleProvider chain = _master;          // mono float, _format.SampleRate
                if (mix.Channels >= 2) chain = new MonoToStereoSampleProvider(chain);
                if (mix.SampleRate != _format.SampleRate)
                    chain = new WdlResamplingSampleProvider(chain, mix.SampleRate);
                var wasapi = new WasapiOut(_device, AudioClientShareMode.Shared, false, 200);
                wasapi.Init(chain);
                _output = wasapi;
            }
            catch
            {
                _device?.Dispose();
                _device = null;
                _output = new WaveOutEvent();    // fall back to default on any failure
                _output.Init(_master);
            }
        }

        if (_started) _output.Play();
    }

    private void Tick()
    {
        bool play = Enabled && _isBusy();

        if (_busyGate is not null)
        {
            _busyGate.Volume = play ? 1f : 0f;
            return;
        }

        // Synth fallback: irregular one-shot clicks (random skip + variant pick).
        if (!play || _rng.NextDouble() < 0.35)
            return;
        _mixer.AddMixerInput(new OneShot(_clicks![_rng.Next(_clicks.Length)], _format));
    }

    public void Start()
    {
        _started = true;
        _output.Play();
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _output?.Stop();
        _output?.Dispose();
        _device?.Dispose();
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
