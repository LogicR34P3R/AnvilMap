using AnvilMap;

[MapFrom(typeof(Coordinates))]
public sealed class CoordinatesDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}
