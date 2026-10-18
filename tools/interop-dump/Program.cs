// Usage: interop-dump <assembly.dll> <TypeName-substring>...  prints fields/methods/properties of matching types
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var asm = args[0];
using var pe = new PEReader(File.OpenRead(asm));
var md = pe.GetMetadataReader();
var prov = new Sig(md);
foreach (var th in md.TypeDefinitions)
{
    var td = md.GetTypeDefinition(th);
    var name = md.GetString(td.Name);
    var ns = md.GetString(td.Namespace);
    var decl = td.GetDeclaringType();
    var full = decl.IsNil ? (ns.Length > 0 ? ns + "." : "") + name : md.GetString(md.GetTypeDefinition(decl).Name) + "+" + name;
    if (!args.Skip(1).Any(a => full == a || full.EndsWith("." + a) || full.EndsWith("+" + a))) continue;
    var bt = td.BaseType.IsNil ? "" : " : " + prov.Name(td.BaseType);
    Console.WriteLine($"== {full}{bt}");
    foreach (var fh in td.GetFields()) { var f = md.GetFieldDefinition(fh); Console.WriteLine($"  F {f.DecodeSignature(prov, null)} {md.GetString(f.Name)}"); }
    foreach (var ph in td.GetProperties()) { var p = md.GetPropertyDefinition(ph); Console.WriteLine($"  P {p.DecodeSignature(prov, null).ReturnType} {md.GetString(p.Name)}"); }
    foreach (var mh in td.GetMethods())
    {
        var m = md.GetMethodDefinition(mh); var n = md.GetString(m.Name);
        if (n.StartsWith("get_") || n.StartsWith("set_") || n.Contains("NativeMethodInfoPtr")) continue;
        var s = m.DecodeSignature(prov, null);
        var pnames = m.GetParameters().Select(p => md.GetString(md.GetParameter(p).Name)).ToArray();
        var ps = string.Join(", ", s.ParameterTypes.Select((t, i) => t + " " + (i < pnames.Length ? pnames[i] : "")));
        Console.WriteLine($"  M {((m.Attributes & System.Reflection.MethodAttributes.Static) != 0 ? "static " : "")}{s.ReturnType} {n}({ps})");
    }
}

class Sig : ISignatureTypeProvider<string, object>
{
    readonly MetadataReader md; public Sig(MetadataReader m) { md = m; }
    public string Name(EntityHandle h) => h.Kind switch
    {
        HandleKind.TypeDefinition => GetTypeFromDefinition(md, (TypeDefinitionHandle)h, 0),
        HandleKind.TypeReference => GetTypeFromReference(md, (TypeReferenceHandle)h, 0),
        HandleKind.TypeSpecification => GetTypeFromSpecification(md, null, (TypeSpecificationHandle)h, 0),
        _ => "?"
    };
    public string GetArrayType(string e, ArrayShape s) => e + "[,]";
    public string GetByReferenceType(string e) => "ref " + e;
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    public string GetGenericInstantiation(string g, System.Collections.Immutable.ImmutableArray<string> a) => g + "<" + string.Join(",", a) + ">";
    public string GetGenericMethodParameter(object c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object c, int i) => "!" + i;
    public string GetModifiedType(string m, string u, bool r) => u;
    public string GetPinnedType(string e) => e;
    public string GetPointerType(string e) => e + "*";
    public string GetPrimitiveType(PrimitiveTypeCode t) => t.ToString();
    public string GetSZArrayType(string e) => e + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) { var t = r.GetTypeDefinition(h); var d = t.GetDeclaringType(); return (d.IsNil ? "" : GetTypeFromDefinition(r, d, 0) + "+") + r.GetString(t.Name); }
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) { var t = r.GetTypeReference(h); var ns = r.GetString(t.Namespace); return (ns.Length > 0 ? ns + "." : "") + r.GetString(t.Name); }
    public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte k) => r.GetTypeSpecification(h).DecodeSignature(this, c);
}
