namespace AnvilMap.InterceptorSmokeTests;

[MapTo(typeof(WidgetDto))]
public sealed class Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class WidgetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

// Direct call sites the generator's own discovery stage must see in THIS project's own source -
// interception requires the [MapTo] declaration and the call site to live in the same
// compilation, since a generator never sees another project's source (only its compiled
// metadata) - a downstream project referencing AnvilMap.Benchmarks alone could never
// exercise this.
public static class Caller
{
    public static WidgetDto CallOneArg(Widget widget) => GeneratedMappings.Map<Widget, WidgetDto>(widget);

    public static WidgetDto CallTwoArg(Widget widget, WidgetDto destination) => GeneratedMappings.Map<Widget, WidgetDto>(widget, destination);
}
