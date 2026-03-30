namespace Pretext.LayoutFramework;

public readonly record struct LayoutPoint(double X, double Y);

public readonly record struct LayoutSize(double Width, double Height)
{
    public static LayoutSize Empty => new(0, 0);

    public static LayoutSize Infinite => new(double.PositiveInfinity, double.PositiveInfinity);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public double Left => X;

    public double Top => Y;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public LayoutPoint Location => new(X, Y);

    public LayoutSize Size => new(Width, Height);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool IntersectsVertically(double top, double bottom)
    {
        return Bottom >= top && Top <= bottom;
    }
}

public readonly record struct LayoutViewport(double X, double Y, double Width, double Height)
{
    public double Left => X;

    public double Top => Y;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public LayoutRect Bounds => new(X, Y, Width, Height);
}

public readonly record struct LayoutConstraints(
    double AvailableWidth,
    double AvailableHeight,
    LayoutViewport? Viewport = null,
    double Density = 1d)
{
    public LayoutConstraints WithoutViewport()
    {
        return Viewport is null ? this : new LayoutConstraints(AvailableWidth, AvailableHeight, null, Density);
    }
}
