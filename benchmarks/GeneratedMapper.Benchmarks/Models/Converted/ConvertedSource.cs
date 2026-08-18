namespace GeneratedMapper.Benchmarks.Models;

// One computed/combined destination property, mirroring [MapUsing]'s test scenarios
// (User.FirstName + LastName -> UserDto.FullName).
public sealed partial class ConvertedSource
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}
