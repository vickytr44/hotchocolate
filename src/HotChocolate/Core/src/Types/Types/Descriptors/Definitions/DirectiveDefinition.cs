using System;
using HotChocolate.Language;

#nullable enable

namespace HotChocolate.Types.Descriptors.Definitions;

/// <summary>
/// Represents the data to create a directive.
/// </summary>
public sealed class DirectiveDefinition
{
    /// <summary>
    /// Initializes a new instance of a <see cref="DirectiveDefinition"/>
    /// </summary>
    /// <param name="directiveNode">
    /// The directive syntax node.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="directiveNode"/> is <c>null</c>.
    /// </exception>
    public DirectiveDefinition(DirectiveNode directiveNode)
    {
        Value = directiveNode ?? throw new ArgumentNullException(nameof(directiveNode));
        Type = TypeReference.CreateDirective(directiveNode.Name.Value);
    }

    /// <summary>
    /// Initializes a new instance of a <see cref="DirectiveDefinition"/>
    /// </summary>
    /// <param name="directive">
    /// The runtime instance of a directive.
    /// </param>
    /// <param name="extendedTypeDirectiveType">
    /// The type reference to refer to the directive type.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="directive"/> or <paramref name="extendedTypeDirectiveType"/> is <c>null</c>.
    /// </exception>
    public DirectiveDefinition(object directive, ExtendedTypeDirectiveReference extendedTypeDirectiveType)
    {
        Value = directive ?? throw new ArgumentNullException(nameof(directive));
        Type = extendedTypeDirectiveType ?? throw new ArgumentNullException(nameof(extendedTypeDirectiveType));
    }

    public ITypeReference Type { get; }

    public object Value { get; }
}
