using AnvilMap;

[MapTo(typeof(UserDto), GenerateReverse = true)]
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
