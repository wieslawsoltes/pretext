using System.Globalization;

namespace Pretext.LayoutFramework;

public readonly record struct LayoutFingerprint(ulong Value)
{
    public static LayoutFingerprint Empty => new(0);

    public override string ToString()
    {
        return Value.ToString("X16", CultureInfo.InvariantCulture);
    }
}

public struct LayoutFingerprintBuilder
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    private ulong _hash;

    public static LayoutFingerprintBuilder Create()
    {
        return new LayoutFingerprintBuilder { _hash = OffsetBasis };
    }

    public void Add(bool value)
    {
        EnsureInitialized();
        AddByte(value ? (byte)1 : (byte)0);
    }

    public void Add(int value)
    {
        EnsureInitialized();
        AddUInt32((uint)value);
    }

    public void Add(long value)
    {
        EnsureInitialized();
        AddUInt64((ulong)value);
    }

    public void Add(ulong value)
    {
        EnsureInitialized();
        AddUInt64(value);
    }

    public void Add(double value)
    {
        EnsureInitialized();
        AddUInt64((ulong)BitConverter.DoubleToInt64Bits(value));
    }

    public void Add(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Add(Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }

    public void Add(LayoutFingerprint fingerprint)
    {
        Add(fingerprint.Value);
    }

    public void Add(LayoutSize size)
    {
        Add(size.Width);
        Add(size.Height);
    }

    public void Add(LayoutRect rect)
    {
        Add(rect.X);
        Add(rect.Y);
        Add(rect.Width);
        Add(rect.Height);
    }

    public void Add(string? value)
    {
        EnsureInitialized();

        if (value is null)
        {
            AddByte(0);
            return;
        }

        AddByte(1);
        Add(value.AsSpan());
    }

    public void Add(ReadOnlySpan<char> value)
    {
        EnsureInitialized();
        AddUInt32((uint)value.Length);

        foreach (var ch in value)
        {
            AddUInt16(ch);
        }
    }

    public LayoutFingerprint ToFingerprint()
    {
        return new LayoutFingerprint(_hash == 0 ? OffsetBasis : _hash);
    }

    private void EnsureInitialized()
    {
        if (_hash == 0)
        {
            _hash = OffsetBasis;
        }
    }

    private void AddUInt16(ushort value)
    {
        AddByte((byte)value);
        AddByte((byte)(value >> 8));
    }

    private void AddUInt32(uint value)
    {
        AddByte((byte)value);
        AddByte((byte)(value >> 8));
        AddByte((byte)(value >> 16));
        AddByte((byte)(value >> 24));
    }

    private void AddUInt64(ulong value)
    {
        AddByte((byte)value);
        AddByte((byte)(value >> 8));
        AddByte((byte)(value >> 16));
        AddByte((byte)(value >> 24));
        AddByte((byte)(value >> 32));
        AddByte((byte)(value >> 40));
        AddByte((byte)(value >> 48));
        AddByte((byte)(value >> 56));
    }

    private void AddByte(byte value)
    {
        _hash ^= value;
        _hash *= Prime;
    }
}
