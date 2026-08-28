namespace AnvilMap;

/// <summary>
/// DI-friendly entry point for the generated mappings, for call sites that need a single
/// injectable service rather than calling the generated <c>To{Dest}()</c> extension methods
/// directly (e.g. <c>services.AddSingleton&lt;IMapper, AnvilMapService&gt;()</c>).
/// The generator emits <c>AnvilMapService</c>, which implements this interface by
/// dispatching to the same generated mapping methods the extension-method call sites use -
/// there is no separate runtime configuration, reflection, or setup step involved.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Maps <paramref name="source"/> to a new <typeparamref name="TDestination"/> instance,
    /// resolving which generated mapping to use from <paramref name="source"/>'s runtime type.
    /// Throws <see cref="InvalidOperationException"/> if no <see cref="MapToAttribute"/>
    /// declaration produced a mapping from that type to <typeparamref name="TDestination"/>.
    /// </summary>
    TDestination Map<TDestination>(object source);

    /// <summary>
    /// Maps <paramref name="source"/> to a new <typeparamref name="TDestination"/> instance.
    /// Prefer this overload over <see cref="Map{TDestination}(object)"/> when the source type
    /// is known at the call site, since it avoids the runtime type lookup.
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>
    /// Maps <paramref name="source"/> into the already-constructed <paramref name="destination"/>
    /// instance, overwriting its mapped properties in place. Not generated for destination
    /// types with <c>init</c>-only properties, since those can't be assigned after
    /// construction (see diagnostic AM008) - calling this overload for such a
    /// <typeparamref name="TDestination"/> throws <see cref="InvalidOperationException"/>.
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
}
