using AnvilMap;

[MapTo(typeof(EmployeeDto))]
[MapProperty(typeof(EmployeeDto), "Address.City", nameof(EmployeeDto.HomeCity))]
public sealed class Employee
{
    public string Name { get; set; } = "";
    public Address Address { get; set; } = new();
}
