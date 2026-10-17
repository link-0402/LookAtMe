using System;
using System.Linq;
using System.Reflection;

namespace TargetMe.Integrations.Sync;

/// <summary>
/// Reflection helpers for reaching into another plugin. Nothing here references the other plugin's
/// assemblies or its Microsoft.Extensions copies; each plugin loads its own, so typed casts fail.
/// </summary>
public static class SyncReflection
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string LocalPluginType = "Dalamud.Plugin.Internal.Types.LocalPlugin";

    /// <summary>The loaded plugin's main object, or null when it isn't loaded.</summary>
    public static object? FindPluginInstance(string internalName)
    {
        var exposed = Services.PluginInterface.InstalledPlugins
            .FirstOrDefault(p => p.IsLoaded && string.Equals(p.InternalName, internalName, StringComparison.OrdinalIgnoreCase));
        if (exposed == null)
            return null;

        // Dalamud's IExposedPlugin wraps its LocalPlugin; find it by type, not by a generated field name.
        object? localPlugin = null;
        for (var type = exposed.GetType(); type != null && localPlugin == null; type = type.BaseType)
        {
            localPlugin = type.GetFields(Members | BindingFlags.DeclaredOnly)
                .Where(f => f.FieldType.FullName == LocalPluginType)
                .Select(f => f.GetValue(exposed))
                .FirstOrDefault(v => v != null);
        }

        return Read(localPlugin, "instance")
               ?? throw new MissingMemberException(LocalPluginType, "instance");
    }

    /// <summary>A service from the plugin's own DI container (its host's <c>Services</c>).</summary>
    public static object GetService(object plugin, string typeName)
    {
        var host = Read(plugin, "_host") ?? Read(Read(plugin, "_lifecycle"), "_host")
                   ?? throw new MissingMemberException(plugin.GetType().Name, "_host");
        var services = Read(host, "Services") as IServiceProvider
                       ?? throw new MissingMemberException(host.GetType().Name, "Services");
        return services.GetService(Type(plugin.GetType().Assembly, typeName))
               ?? throw new MissingMemberException(typeName);
    }

    public static Type Type(Assembly assembly, string typeName) =>
        assembly.GetType(typeName) ?? throw new MissingMemberException(typeName);

    /// <summary>A property or field, public or not, looked up through the base types.</summary>
    public static object? Read(object? instance, string member)
    {
        for (var type = instance?.GetType(); type != null; type = type.BaseType)
        {
            if (type.GetProperty(member, Members | BindingFlags.DeclaredOnly) is { } property)
                return property.GetValue(instance);
            if (type.GetField(member, Members | BindingFlags.DeclaredOnly) is { } field)
                return field.GetValue(instance);
        }

        return null;
    }

    public static MethodInfo Method(Type type, string name, params Type[] parameters) =>
        type.GetMethod(name, Members, null, parameters, null)
        ?? throw new MissingMemberException(type.Name, name);

    public static PropertyInfo Property(Type type, string name) =>
        type.GetProperty(name, Members)
        ?? throw new MissingMemberException(type.Name, name);

    public static FieldInfo Field(Type type, string name) =>
        type.GetField(name, Members)
        ?? throw new MissingMemberException(type.Name, name);

    public static Exception Unwrap(Exception e)
    {
        while (e is TargetInvocationException { InnerException: { } inner })
            e = inner;
        return e;
    }
}
