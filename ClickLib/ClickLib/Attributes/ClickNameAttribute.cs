using System;

namespace ClickLib.Attributes;

/// <summary>
/// The callable name of a click.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal class ClickNameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickNameAttribute"/> class.
    /// </summary>
    /// <param name="name">Name of the click.</param>
    public ClickNameAttribute(string name)
    {
        this.Name = name;
    }

    /// <summary>
    /// Gets the name of the click.
    /// </summary>
    public string Name { get; init; }
}
