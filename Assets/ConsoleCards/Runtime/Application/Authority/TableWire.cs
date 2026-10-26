using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Serialization;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// MP-1 (doc 30 A6, doc 11): the table's plain data as bytes, for the network. Commands and a whole table (the same
    /// data an Undo step keeps) are written field by field, so every command type, the match and a game's own state go
    /// over the wire without a hand-written format for each. Shared objects stay shared and loops are allowed.
    /// Reading builds only types from Console Cards, the .NET collections and Unity's small value types (vectors,
    /// colours), so a message cannot make the game create anything else. Both computers run the same build.
    /// Not used per frame: a command or a table at a time.
    /// </summary>
    public static class TableWire
    {
        private const byte TagNull = 0;
        private const byte TagBool = 1;
        private const byte TagByte = 2;
        private const byte TagSByte = 3;
        private const byte TagShort = 4;
        private const byte TagUShort = 5;
        private const byte TagInt = 6;
        private const byte TagUInt = 7;
        private const byte TagLong = 8;
        private const byte TagULong = 9;
        private const byte TagFloat = 10;
        private const byte TagDouble = 11;
        private const byte TagDecimal = 12;
        private const byte TagChar = 13;
        private const byte TagString = 14;
        private const byte TagGuid = 15;
        private const byte TagDateTime = 16;
        private const byte TagTimeSpan = 17;
        private const byte TagEnum = 20;
        private const byte TagStruct = 21;
        private const byte TagReference = 22;
        private const byte TagObject = 23;
        private const byte TagArray = 24;
        private const byte TagDictionary = 25;
        private const byte TagSet = 26;
        private const byte TagList = 27;

        private const int MaxCollection = 1 << 22;

        private static readonly Dictionary<Type, FieldInfo[]> fieldsByType = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<string, Type> typesByName = new Dictionary<string, Type>();
        private static readonly object gate = new object();

        /// <summary>Writes <paramref name="value"/> (and everything it holds) as bytes, gzipped when asked.</summary>
        public static byte[] Write(object value, bool compress)
        {
            using (MemoryStream raw = new MemoryStream(1024))
            {
                using (BinaryWriter writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, true))
                {
                    new Writer(writer).Value(value);
                }

                if (!compress) return raw.ToArray();
                using (MemoryStream packed = new MemoryStream((int)raw.Length / 3 + 64))
                {
                    using (GZipStream zip = new GZipStream(packed, CompressionLevel.Fastest, true))
                    {
                        raw.Position = 0;
                        raw.CopyTo(zip);
                    }

                    return packed.ToArray();
                }
            }
        }

        /// <summary>Reads what <see cref="Write"/> wrote. Throws <see cref="InvalidDataException"/> on bad data.</summary>
        public static object Read(byte[] data, int offset, int count, bool compressed)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (MemoryStream input = new MemoryStream(data, offset, count, false))
            {
                if (!compressed) return ReadFrom(input);
                using (GZipStream zip = new GZipStream(input, CompressionMode.Decompress))
                using (MemoryStream raw = new MemoryStream(count * 3 + 64))
                {
                    zip.CopyTo(raw);
                    raw.Position = 0;
                    return ReadFrom(raw);
                }
            }
        }

        /// <summary>A 64-bit FNV-1a hash of some bytes (to tell whether a table changed since it was last sent).</summary>
        public static ulong Hash(byte[] data, int offset, int count)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = offset; i < offset + count; i++)
            {
                hash ^= data[i];
                hash *= 1099511628211UL;
            }

            return hash;
        }

        private static object ReadFrom(Stream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                try
                {
                    return new Reader(reader).Value();
                }
                catch (EndOfStreamException exception)
                {
                    throw new InvalidDataException("The table data ended too early.", exception);
                }
            }
        }

        // ---------- types ----------

        private static FieldInfo[] FieldsOf(Type type)
        {
            lock (gate)
            {
                if (fieldsByType.TryGetValue(type, out FieldInfo[] cached)) return cached;
                List<FieldInfo> fields = new List<FieldInfo>();
                List<Type> chain = new List<Type>();
                for (Type at = type; at != null && at != typeof(object) && at != typeof(ValueType); at = at.BaseType) chain.Add(at);
                for (int level = chain.Count - 1; level >= 0; level--)
                {
                    FieldInfo[] declared = chain[level].GetFields(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    Array.Sort(declared, (a, b) => string.CompareOrdinal(a.Name, b.Name));
                    for (int i = 0; i < declared.Length; i++)
                    {
                        if (declared[i].IsLiteral) continue;
                        fields.Add(declared[i]);
                    }
                }

                FieldInfo[] result = fields.ToArray();
                fieldsByType[type] = result;
                return result;
            }
        }

        private static bool IsSkipped(Type type)
        {
            return typeof(Delegate).IsAssignableFrom(type)
                || typeof(MemberInfo).IsAssignableFrom(type)
                || type == typeof(IntPtr)
                || type == typeof(UIntPtr)
                || type.IsPointer;
        }

        private static bool IsUnityObject(Type type)
        {
            for (Type at = type; at != null; at = at.BaseType)
            {
                if (at.FullName == "UnityEngine.Object") return true;
            }

            return false;
        }

        private static string NameOf(Type type) => type.FullName + ", " + type.Assembly.GetName().Name;

        private static Type TypeNamed(string name)
        {
            lock (gate)
            {
                if (typesByName.TryGetValue(name, out Type cached)) return cached;
                Type type = Type.GetType(name, false);
                if (type == null) throw new InvalidDataException($"Unknown type in table data: {name}.");
                if (!IsAllowed(type)) throw new InvalidDataException($"Type not allowed in table data: {name}.");
                typesByName[name] = type;
                return type;
            }
        }

        // Only the game's own types, the .NET basics and collections, and Unity's value types (Vector3, Color ...).
        private static bool IsAllowed(Type type)
        {
            if (type.IsArray) return IsAllowed(type.GetElementType());
            if (type.IsGenericType)
            {
                Type[] arguments = type.GetGenericArguments();
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (!IsAllowed(arguments[i])) return false;
                }
            }

            if (IsSkipped(type) || IsUnityObject(type)) return false;
            string assembly = type.Assembly.GetName().Name;
            if (assembly.StartsWith("ConsoleCards.", StringComparison.Ordinal)) return true;
            if (assembly == "UnityEngine.CoreModule") return type.IsValueType;
            if (assembly == "mscorlib" || assembly == "netstandard" || assembly == "System.Private.CoreLib"
                || assembly == "System" || assembly == "System.Core" || assembly == "System.Collections")
            {
                string ns = type.Namespace ?? string.Empty;
                return ns == "System" || ns == "System.Collections.Generic" || ns == "System.Collections.ObjectModel"
                    || ns == "System.Collections";
            }

            return false;
        }

        private static bool IsGeneric(Type type, Type definition) => type.IsGenericType && type.GetGenericTypeDefinition() == definition;

        // ---------- writing ----------

        private sealed class Writer
        {
            private readonly BinaryWriter output;
            private readonly Dictionary<object, int> seen = new Dictionary<object, int>(ReferenceComparer.Instance);
            private readonly Dictionary<Type, int> types = new Dictionary<Type, int>();

            internal Writer(BinaryWriter output)
            {
                this.output = output;
            }

            internal void Value(object value)
            {
                if (value == null)
                {
                    output.Write(TagNull);
                    return;
                }

                Type type = value.GetType();
                if (IsSkipped(type))
                {
                    output.Write(TagNull); // events and delegates belong to this computer only
                    return;
                }

                if (IsUnityObject(type))
                {
                    throw new InvalidOperationException($"{type.Name} is a Unity object and cannot be sent as table data.");
                }

                switch (value)
                {
                    case bool v: output.Write(TagBool); output.Write(v); return;
                    case byte v: output.Write(TagByte); output.Write(v); return;
                    case sbyte v: output.Write(TagSByte); output.Write(v); return;
                    case short v: output.Write(TagShort); output.Write(v); return;
                    case ushort v: output.Write(TagUShort); output.Write(v); return;
                    case char v: output.Write(TagChar); output.Write((ushort)v); return;
                    case string v: output.Write(TagString); output.Write(v); return;
                    case Guid v: output.Write(TagGuid); output.Write(v.ToByteArray()); return;
                    case DateTime v: output.Write(TagDateTime); output.Write(v.ToBinary()); return;
                    case TimeSpan v: output.Write(TagTimeSpan); output.Write(v.Ticks); return;
                    case decimal v: output.Write(TagDecimal); output.Write(v); return;
                }

                if (type.IsEnum)
                {
                    output.Write(TagEnum);
                    WriteType(type);
                    output.Write(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                    return;
                }

                switch (value)
                {
                    case int v: output.Write(TagInt); output.Write(v); return;
                    case uint v: output.Write(TagUInt); output.Write(v); return;
                    case long v: output.Write(TagLong); output.Write(v); return;
                    case ulong v: output.Write(TagULong); output.Write(v); return;
                    case float v: output.Write(TagFloat); output.Write(v); return;
                    case double v: output.Write(TagDouble); output.Write(v); return;
                }

                if (type.IsValueType)
                {
                    output.Write(TagStruct);
                    WriteType(type);
                    Fields(value, type);
                    return;
                }

                if (seen.TryGetValue(value, out int known))
                {
                    output.Write(TagReference);
                    output.Write(known);
                    return;
                }

                seen.Add(value, seen.Count);
                if (type.IsArray)
                {
                    Array array = (Array)value;
                    if (array.Rank != 1) throw new InvalidOperationException("Only one-dimensional arrays can be sent.");
                    output.Write(TagArray);
                    WriteType(type.GetElementType());
                    output.Write(array.Length);
                    for (int i = 0; i < array.Length; i++) Value(array.GetValue(i));
                    return;
                }

                if (IsGeneric(type, typeof(Dictionary<,>)) || IsGeneric(type, typeof(SortedDictionary<,>)))
                {
                    IDictionary dictionary = (IDictionary)value;
                    output.Write(TagDictionary);
                    WriteType(type);
                    output.Write(dictionary.Count);
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        Value(entry.Key);
                        Value(entry.Value);
                    }

                    return;
                }

                if (IsGeneric(type, typeof(HashSet<>)) || IsGeneric(type, typeof(List<>)))
                {
                    ICollection items = value as ICollection;
                    int count = items != null ? items.Count : CountOf((IEnumerable)value);
                    output.Write(IsGeneric(type, typeof(List<>)) ? TagList : TagSet);
                    WriteType(type);
                    output.Write(count);
                    foreach (object item in (IEnumerable)value) Value(item);
                    return;
                }

                output.Write(TagObject);
                WriteType(type);
                Fields(value, type);
            }

            private static int CountOf(IEnumerable items)
            {
                int count = 0;
                foreach (object _ in items) count++;
                return count;
            }

            private void Fields(object value, Type type)
            {
                FieldInfo[] fields = FieldsOf(type);
                for (int i = 0; i < fields.Length; i++) Value(fields[i].GetValue(value));
            }

            private void WriteType(Type type)
            {
                if (types.TryGetValue(type, out int index))
                {
                    output.Write(index);
                    return;
                }

                types.Add(type, types.Count);
                output.Write(-1);
                output.Write(NameOf(type));
            }
        }

        // ---------- reading ----------

        private sealed class Reader
        {
            private readonly BinaryReader input;
            private readonly List<object> seen = new List<object>();
            private readonly List<Type> types = new List<Type>();

            internal Reader(BinaryReader input)
            {
                this.input = input;
            }

            internal object Value()
            {
                byte tag = input.ReadByte();
                switch (tag)
                {
                    case TagNull: return null;
                    case TagBool: return input.ReadBoolean();
                    case TagByte: return input.ReadByte();
                    case TagSByte: return input.ReadSByte();
                    case TagShort: return input.ReadInt16();
                    case TagUShort: return input.ReadUInt16();
                    case TagInt: return input.ReadInt32();
                    case TagUInt: return input.ReadUInt32();
                    case TagLong: return input.ReadInt64();
                    case TagULong: return input.ReadUInt64();
                    case TagFloat: return input.ReadSingle();
                    case TagDouble: return input.ReadDouble();
                    case TagDecimal: return input.ReadDecimal();
                    case TagChar: return (char)input.ReadUInt16();
                    case TagString: return input.ReadString();
                    case TagGuid: return new Guid(input.ReadBytes(16));
                    case TagDateTime: return DateTime.FromBinary(input.ReadInt64());
                    case TagTimeSpan: return new TimeSpan(input.ReadInt64());
                    case TagEnum:
                    {
                        Type type = ReadType();
                        if (!type.IsEnum) throw new InvalidDataException("Expected an enum type.");
                        return Enum.ToObject(type, input.ReadInt64());
                    }
                    case TagStruct:
                    {
                        Type type = ReadType();
                        if (!type.IsValueType) throw new InvalidDataException("Expected a value type.");
                        object boxed = FormatterServices.GetUninitializedObject(type);
                        Fields(boxed, type);
                        return boxed;
                    }
                    case TagReference:
                    {
                        int index = input.ReadInt32();
                        if (index < 0 || index >= seen.Count) throw new InvalidDataException("Bad reference in table data.");
                        return seen[index];
                    }
                    case TagArray:
                    {
                        Type element = ReadType();
                        int length = Count();
                        Array array = Array.CreateInstance(element, length);
                        seen.Add(array);
                        for (int i = 0; i < length; i++) array.SetValue(Value(), i);
                        return array;
                    }
                    case TagDictionary:
                    {
                        Type type = ReadType();
                        if (!IsGeneric(type, typeof(Dictionary<,>)) && !IsGeneric(type, typeof(SortedDictionary<,>)))
                            throw new InvalidDataException("Expected a dictionary type.");
                        IDictionary dictionary = (IDictionary)Activator.CreateInstance(type);
                        seen.Add(dictionary);
                        int count = Count();
                        for (int i = 0; i < count; i++)
                        {
                            object key = Value();
                            dictionary[key] = Value();
                        }

                        return dictionary;
                    }
                    case TagSet:
                    case TagList:
                    {
                        Type type = ReadType();
                        bool list = IsGeneric(type, typeof(List<>));
                        if (!list && !IsGeneric(type, typeof(HashSet<>))) throw new InvalidDataException("Expected a list or set type.");
                        object collection = Activator.CreateInstance(type);
                        seen.Add(collection);
                        int count = Count();
                        MethodInfo add = type.GetMethod("Add", new[] { type.GetGenericArguments()[0] });
                        object[] argument = new object[1];
                        for (int i = 0; i < count; i++)
                        {
                            argument[0] = Value();
                            add.Invoke(collection, argument);
                        }

                        return collection;
                    }
                    case TagObject:
                    {
                        Type type = ReadType();
                        if (type.IsValueType || type.IsAbstract) throw new InvalidDataException("Expected a class type.");
                        object value = FormatterServices.GetUninitializedObject(type);
                        seen.Add(value);
                        Fields(value, type);
                        return value;
                    }
                    default:
                        throw new InvalidDataException($"Unknown tag {tag} in table data.");
                }
            }

            private int Count()
            {
                int count = input.ReadInt32();
                if (count < 0 || count > MaxCollection) throw new InvalidDataException("Bad length in table data.");
                return count;
            }

            private void Fields(object target, Type type)
            {
                FieldInfo[] fields = FieldsOf(type);
                for (int i = 0; i < fields.Length; i++)
                {
                    object value = Value();
                    if (value == null && fields[i].FieldType.IsValueType && Nullable.GetUnderlyingType(fields[i].FieldType) == null)
                    {
                        continue; // a skipped value (an event) on a struct field keeps its default
                    }

                    if (value != null && !fields[i].FieldType.IsInstanceOfType(value)
                        && Nullable.GetUnderlyingType(fields[i].FieldType) != value.GetType())
                    {
                        throw new InvalidDataException($"Table data does not fit {type.Name}.{fields[i].Name}.");
                    }

                    fields[i].SetValue(target, value);
                }
            }

            private Type ReadType()
            {
                int index = input.ReadInt32();
                if (index >= 0)
                {
                    if (index >= types.Count) throw new InvalidDataException("Bad type index in table data.");
                    return types[index];
                }

                Type type = TypeNamed(input.ReadString());
                types.Add(type);
                return type;
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
