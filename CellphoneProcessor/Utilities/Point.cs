namespace CellphoneProcessor.Utilities;

readonly struct Point
{
    public double Lon { get; }
    public double Lat { get; }
    public int Pings { get; }

    public bool NightStart { get; }

    public long Duration { get; }

    public Point(double lon, double lat, int pings, bool nightStart, long duration)
    {
        Lon = lon;
        Lat = lat;
        Pings = pings;
        NightStart = nightStart;
        Duration = duration;
    }
}
