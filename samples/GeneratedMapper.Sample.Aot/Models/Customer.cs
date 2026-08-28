using GeneratedMapper.Sample.Aot.ViewModels;

namespace GeneratedMapper.Sample.Aot.Models;

[MapTo(typeof(CustomerDto))]
public sealed class Customer
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}
