using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using AutoPropertyAttribute;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoPropertyGenerator
{
    [Generator]
    public class Generator : IIncrementalGenerator
    {
        private const string PROPERTY_NAME_PREFIX = "p_";
        
        public void Initialize(IncrementalGeneratorInitializationContext ctx) =>
            ctx.RegisterSourceOutput(ctx.CompilationProvider.Combine(ctx.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: IsFieldInPartialClass,
                    transform: HasAttribute)
                .Where(IsNotNull).Collect()), Execute);

        private static void Execute(SourceProductionContext ctx,
            (Compilation compilation, ImmutableArray<FieldDeclarationSyntax> fds) result)
        {
            foreach (var kv in GetGroupedByClass(result.fds))
            {
                var className = kv.Key;
                var cds = GetClassOf(kv.Value[0]);
                var classAccessor = GetAccessOfClass(cds);
                
                var source = new StringBuilder();
                source.AppendLine("public event System.Action OnAnyChanged;");
                foreach (var fds in kv.Value)
                {
                    var fieldName = fds.Declaration.Variables[0].Identifier.ValueText.Trim();
                    var propertyName = ConvertFieldName(fieldName);
                    var fieldType = fds.Declaration.Type.ToFullString().Trim();

                    source.Append("public event System.Action<").Append(fieldType).Append("> ")
                        .Append(ConstructEvent(propertyName)).AppendLine(";");
                    source.Append(GetGetAccessor(fds)).Append(" ").Append(fieldType).Append(" ")
                        .Append(propertyName).AppendLine(" { ")
                        .Append("get => ").Append(fieldName).AppendLine(";");
                    if (GetSetAccessor(fds) != GetGetAccessor(fds)) source.Append(GetSetAccessor(fds));
                    source.Append(" set { ")
                        .Append(fieldName).AppendLine(" = value; ")
                        .Append(ConstructEvent(propertyName)).AppendLine("?.Invoke(value);")
                        .AppendLine("OnAnyChanged?.Invoke();").AppendLine(" } }");

                }

                var sourceCode = string.Format(CLASS_SOURCE,
                    /*{0}*/classAccessor,
                    /*{1}*/className,
                    /*{2}*/source);
                
                var ns = GetNamespace(cds);
                if (!string.IsNullOrEmpty(ns))
                {
                    sourceCode = $"namespace {ns}{{ {sourceCode} }}";
                }
                
                ctx.AddSource($"{className}_codegen.cs", sourceCode);
            }
        }
       
        /**
         * {0} class accessor
         * {1} class name
         * {2} source
         */
        private const string CLASS_SOURCE = @"{0} partial class {1} {{
{2}
}}";
        
        private static string ConstructEvent(string propertyName) => $"On{propertyName}Change";

        private static string ConvertFieldName(string fieldName)
        {
            var result = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(fieldName);
            if (result == fieldName) result = $"{PROPERTY_NAME_PREFIX}{fieldName}";
            return result;
        }

        private static Dictionary<string, List<FieldDeclarationSyntax>> GetGroupedByClass(
            ImmutableArray<FieldDeclarationSyntax> result)
        {
            var  res = new Dictionary<string, List<FieldDeclarationSyntax>>();
            foreach (var fds in result)
            {
                var className = GetClassNameOf(fds);
                if (!res.ContainsKey(className)) res.Add(className, new List<FieldDeclarationSyntax>());
                res[className].Add(fds);
            }

            return res;
        }
        private const string AUTO_PROPERTY_ATTRIBUTE = nameof(AutoProperty);
        private const string PRIVATE_GET_ATTRIBUTE = nameof(PrivateGet);
        private const string PRIVATE_SET_ATTRIBUTE = nameof(PrivateSet);
        private const string PRIVATE_GET_SET_ATTRIBUTE = nameof(PrivateGetSet);

        
        internal static bool HasAttribute(SyntaxNode syntaxNode, string attributeName) =>
            GetAttributeList(syntaxNode)
                .SelectMany(nodeAttribute => nodeAttribute.Attributes)
                .Any(attribute => attribute.Name.ToString().Trim().ToLower()
                    .Equals(attributeName.Trim().ToLower()));

        private static FieldDeclarationSyntax HasAttribute(GeneratorSyntaxContext ctx, CancellationToken token) =>
            HasAttribute(ctx.Node, AUTO_PROPERTY_ATTRIBUTE)
                ? ctx.Node as FieldDeclarationSyntax
                : null;

        private static string GetGetAccessor(FieldDeclarationSyntax fds) =>
            HasAttribute(fds, PRIVATE_GET_ATTRIBUTE) || HasAttribute(fds, PRIVATE_GET_SET_ATTRIBUTE)
                ? "private"
                : "public";

        private static string GetSetAccessor(FieldDeclarationSyntax fds) =>
            HasAttribute(fds, PRIVATE_SET_ATTRIBUTE) || HasAttribute(fds, PRIVATE_GET_SET_ATTRIBUTE)
                ? "private"
                : "public";

        private static bool IsNotNull(FieldDeclarationSyntax fds) => !(fds is null);

        private static bool IsFieldInPartialClass(SyntaxNode node, CancellationToken token) =>
            node is FieldDeclarationSyntax fds
            && IsPartial(GetClassOf(node));

        private static SyntaxList<AttributeListSyntax> GetAttributeList(SyntaxNode syntaxNode) =>
            syntaxNode is FieldDeclarationSyntax fds
                ? fds.AttributeLists
                : new SyntaxList<AttributeListSyntax>();

        private static bool IsPartial(MemberDeclarationSyntax cds) =>
            cds.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword));

        private static string GetAccessOfClass(ClassDeclarationSyntax cds) =>
            IsPublic(cds) ? "public" : IsPrivate(cds) ? "private" : "internal";

        private static ClassDeclarationSyntax GetClassOf(SyntaxNode node)
        {
            foreach (var syntaxNode in node.Ancestors())
            {
                if (syntaxNode is ClassDeclarationSyntax cds)
                    return cds;
            }

            return null;
        }

        private static bool IsPublic(MemberDeclarationSyntax cds) =>
            cds.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword));

        private static bool IsPrivate(MemberDeclarationSyntax cds) =>
            cds.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PrivateKeyword));

        private static string GetClassNameOf(SyntaxNode node) =>
            GetClassOf(node).Identifier.ValueText;

        private static string GetNamespace(SyntaxNode node)
        {
            var nameSpace = string.Empty;
            var potentialNamespaceParent = node.Parent;

            while (potentialNamespaceParent != null
                   && !(potentialNamespaceParent is NamespaceDeclarationSyntax))
                potentialNamespaceParent = potentialNamespaceParent.Parent;

            if (!(potentialNamespaceParent is NamespaceDeclarationSyntax namespaceParent)) return nameSpace;
            
            nameSpace = namespaceParent.Name.ToString();

            while (true)
            {
                if (!(namespaceParent.Parent is NamespaceDeclarationSyntax parent)) break;

                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                namespaceParent = parent;
            }

            return string.IsNullOrEmpty(nameSpace)
                ? string.Empty
                : nameSpace;
        }
    }
}