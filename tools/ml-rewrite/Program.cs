// Retargets a plugin compiled against BepInEx's interop assemblies to MelonLoader's.
// Both loaders generate them with Il2CppInterop; MelonLoader uses PrefixMode.OptOut, which adds "Il2Cpp" to every
// assembly and namespace not starting with "Assembly-CSharp" or "Unity" (the global namespace becomes "Il2Cpp").
// BepInEx uses OptIn and already prefixes System/mscorlib/Microsoft/Mono/I18N, so those names stay as they are.
// Usage: ml-rewrite <compiled plugin.dll> <output.dll> <BepInEx/interop folder>  (always reads the untouched compiler output)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

static class Program
{
    static readonly string[] OptIn = { "System", "mscorlib", "Microsoft", "Mono", "I18N" };
    static readonly string[] NotPrefixed = { "Assembly-CSharp", "Unity" };

    static HashSet<string> interop;
    static readonly HashSet<TypeReference> done = new(ReferenceEqualityComparer.Instance);
    static readonly HashSet<AssemblyNameReference> scopes = new(ReferenceEqualityComparer.Instance);
    static int types, attrs;

    static string Fix(string name)
    {
        if (name.StartsWith("Il2Cpp", StringComparison.Ordinal) && OptIn.Any(p => name[6..].StartsWith(p, StringComparison.Ordinal))) return name;
        return NotPrefixed.Any(p => name.StartsWith(p, StringComparison.Ordinal)) ? name : "Il2Cpp" + name;
    }

    static int Main(string[] args)
    {
        if (args.Length != 3) { Console.Error.WriteLine("usage: ml-rewrite <compiled plugin.dll> <output.dll> <BepInEx/interop folder>"); return 2; }
        var dll = args[0];
        interop = Directory.GetFiles(args[2], "*.dll").Select(Path.GetFileNameWithoutExtension).ToHashSet();

        var bytes = File.ReadAllBytes(dll);
        using var module = ModuleDefinition.ReadModule(new MemoryStream(bytes));

        // Namespaces first, while the scopes still carry their BepInEx names.
        foreach (var tr in module.GetTypeReferences()) FixType(tr);
        foreach (var a in AllAttributes(module)) FixAttribute(a);

        int asms = 0;
        foreach (var r in module.AssemblyReferences.Concat(scopes).Distinct().Where(r => interop.Contains(r.Name)).ToList())
        {
            var n = Fix(r.Name);
            if (n != r.Name) { r.Name = n; asms++; }
        }

        module.Write(args[1]);
        Console.WriteLine($"ml-rewrite: {asms} assembly refs, {types} type refs, {attrs} attribute type args -> MelonLoader names");
        return 0;
    }

    static void FixType(TypeReference t)
    {
        switch (t)
        {
            case null: return;
            case GenericInstanceType g:
                FixType(g.ElementType);
                foreach (var a in g.GenericArguments) FixType(a);
                return;
            case TypeSpecification s:
                FixType(s.ElementType);
                return;
            case GenericParameter:
                return;
        }
        if (t.DeclaringType != null) { FixType(t.DeclaringType); return; }
        if (!done.Add(t)) return;
        if (t.Scope is AssemblyNameReference asm && interop.Contains(asm.Name))
        {
            var ns = Fix(t.Namespace);
            if (ns != t.Namespace) { t.Namespace = ns; types++; }
        }
    }

    // typeof(...) in attribute blobs (e.g. [HarmonyPatch(typeof(VehicleInput))]) is stored as a type name string with its own scope.
    static void FixAttribute(CustomAttribute a)
    {
        foreach (var arg in a.ConstructorArguments) FixArg(arg);
        foreach (var na in a.Fields.Concat(a.Properties)) FixArg(na.Argument);
    }

    static void FixArg(CustomAttributeArgument arg)
    {
        switch (arg.Value)
        {
            case TypeReference t:
                bool fresh = !done.Contains(t);
                FixType(t);
                if (fresh) attrs++;
                var top = t; while (top.DeclaringType != null) top = top.DeclaringType;
                if (top.Scope is AssemblyNameReference asm) scopes.Add(asm);
                break;
            case CustomAttributeArgument inner: FixArg(inner); break;
            case CustomAttributeArgument[] arr: foreach (var x in arr) FixArg(x); break;
        }
    }

    static IEnumerable<CustomAttribute> AllAttributes(ModuleDefinition m)
    {
        foreach (var a in m.Assembly.CustomAttributes) yield return a;
        foreach (var a in m.CustomAttributes) yield return a;
        foreach (var t in m.GetTypes())
        {
            foreach (var a in t.CustomAttributes) yield return a;
            foreach (var x in t.Methods)
            {
                foreach (var a in x.CustomAttributes) yield return a;
                foreach (var p in x.Parameters) foreach (var a in p.CustomAttributes) yield return a;
            }
            foreach (var x in t.Fields) foreach (var a in x.CustomAttributes) yield return a;
            foreach (var x in t.Properties) foreach (var a in x.CustomAttributes) yield return a;
        }
    }
}
