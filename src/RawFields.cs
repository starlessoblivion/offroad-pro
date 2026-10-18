using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace OthDriveSystems
{
    // Interop generation skipped some generic fields (UpgradesLists.Parts, VehiclePartSO.Effects,
    // InventoryItemsCatalog dictionaries); these helpers reach them through the il2cpp runtime.
    internal static unsafe class RawFields
    {
        static IntPtr Field<T>(string name) where T : Il2CppObjectBase
        {
            var klass = Il2CppClassPointerStore<T>.NativeClassPtr;
            while (klass != IntPtr.Zero)
            {
                var f = IL2CPP.il2cpp_class_get_field_from_name(klass, name);
                if (f != IntPtr.Zero) return f;
                klass = IL2CPP.il2cpp_class_get_parent(klass);
            }
            throw new MissingFieldException(typeof(T).Name, name);
        }

        public static IntPtr GetPtr<T>(T obj, string name) where T : Il2CppObjectBase
        {
            var off = IL2CPP.il2cpp_field_get_offset(Field<T>(name));
            return *(IntPtr*)((byte*)obj.Pointer + off);
        }

        public static void SetPtr<T>(T obj, string name, IntPtr value) where T : Il2CppObjectBase
        {
            var off = IL2CPP.il2cpp_field_get_offset(Field<T>(name));
            IL2CPP.il2cpp_gc_wbarrier_set_field(obj.Pointer, (IntPtr)((byte*)obj.Pointer + off), value);
        }

        public static TResult Get<T, TResult>(T obj, string name) where T : Il2CppObjectBase where TResult : Il2CppObjectBase
        {
            var p = GetPtr(obj, name);
            return p == IntPtr.Zero ? null : (TResult)Activator.CreateInstance(typeof(TResult), p);
        }
    }
}
