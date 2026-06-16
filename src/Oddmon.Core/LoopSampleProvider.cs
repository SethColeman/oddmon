using NAudio.Wave;

namespace Oddmon.Core;

/// <summary>
/// Endlessly loops one or more clips back-to-back. Always fills the requested
/// buffer, so feeding it through a volume gate gives a "play while busy" sound.
/// </summary>
public sealed class LoopSampleProvider(float[][] clips, WaveFormat format) : ISampleProvider
{
    private int _clip;
    private int _pos;

    public WaveFormat WaveFormat => format;

    public int Read(float[] buffer, int offset, int count)
    {
        int written = 0;
        while (written < count)
        {
            float[] cur = clips[_clip];
            int n = Math.Min(count - written, cur.Length - _pos);
            Array.Copy(cur, _pos, buffer, offset + written, n);
            written += n;
            _pos += n;
            if (_pos >= cur.Length)
            {
                _pos = 0;
                _clip = (_clip + 1) % clips.Length;
            }
        }
        return written;
    }
}
