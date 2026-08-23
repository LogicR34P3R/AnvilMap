using GeneratedMapper;

var user = new User { Id = 1, Name = "Ada" };
var dto = user.ToUserDto();

if (dto.Id != 1 || dto.Name != "Ada")
    throw new InvalidOperationException($"Generated mapping produced wrong values: Id={dto.Id}, Name={dto.Name}");

IMapper mapper = new GeneratedMapperService();
var viaMapper = mapper.Map<User, UserDto>(user);

if (viaMapper.Id != 1 || viaMapper.Name != "Ada")
    throw new InvalidOperationException($"IMapper dispatch produced wrong values: Id={viaMapper.Id}, Name={viaMapper.Name}");

var roundTripped = dto.ToUser();

if (roundTripped.Id != 1 || roundTripped.Name != "Ada")
    throw new InvalidOperationException($"GenerateReverse mapping produced wrong values: Id={roundTripped.Id}, Name={roundTripped.Name}");

Console.WriteLine("Smoke test passed: GeneratedMapper.Generator + GeneratedMapper.Abstractions work correctly from packed NuGet packages.");

[MapTo(typeof(UserDto), GenerateReverse = true)]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
