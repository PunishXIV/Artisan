using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using ClickLib.Attributes;
using ClickLib.Bases;
using ClickLib.Exceptions;

namespace ClickLib;

/// <summary>
/// Main class for clicking by name.
/// </summary>
public static class Click
{
    private static readonly Dictionary<string, PrecompiledDelegate> AvailableClicks = new();
    private static bool initialized = false;

    private delegate void PrecompiledDelegate(IntPtr addon);

    /// <summary>
    /// Load the mapping of click names.
    /// </summary>
    public static void Initialize()
    {
        if (initialized)
            return;

        initialized = true;

        //FFXIVClientStructs.Resolver.Initialize();

        // Get all parameterless methods, of types that inherit from ClickBase
        var clicks = typeof(IClickable).Assembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(IClickable)) && !type.IsGenericType)
            .SelectMany(cls => cls.GetMethods())
            .Where(method => method.GetParameters().Length == 0)
            .Select(method => (method, method.GetCustomAttribute<ClickNameAttribute>()?.Name))
            .Where(tpl => tpl.Name != null);

        foreach (var click in clicks)
        {
            var method = click.method;
            var clickType = method.DeclaringType!;
            var ctor = clickType.GetConstructor(new[] { typeof(IntPtr) })!;

            var param = Expression.Parameter(typeof(IntPtr), "addon");
            var instantiate = Expression.New(ctor, param);
            var invoke = Expression.Call(instantiate, method);
            var blockExpr = Expression.Block(invoke);
            var lambdaExpr = Expression.Lambda<PrecompiledDelegate>(blockExpr, param);
            var compiled = lambdaExpr.Compile()!;

            AvailableClicks.Add(click.Name!, compiled);
        }
    }

    /// <summary>
    /// Get a list of available click strings that can be used with SendClick.
    /// </summary>
    /// <returns>A list of click names.</returns>
    public static List<string> GetClickNames() => AvailableClicks.Keys.ToList();

    /// <summary>
    /// Send a click by the name of the individual click.
    /// </summary>
    /// <param name="name">Click name.</param>
    /// <param name="addon">Pointer to an existing addon.</param>
    public static void SendClick(string name, IntPtr addon = default)
    {
        if (!initialized)
            throw new InvalidClickException("Not initialized yet");

        if (!AvailableClicks.TryGetValue(name, out var clickDelegate))
            throw new ClickNotFoundError($"Click \"{name}\" does not exist");

        clickDelegate!(addon);
    }

    /// <summary>
    /// Send a click by the name of the individual click.
    /// </summary>
    /// <param name="name">Click name.</param>
    /// <param name="addon">Pointer to an existing addon.</param>
    /// <returns>A value indicating whether the delegate was successfully called.</returns>
    public static bool TrySendClick(string name, IntPtr addon = default)
    {
        try
        {
            SendClick(name, addon);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
