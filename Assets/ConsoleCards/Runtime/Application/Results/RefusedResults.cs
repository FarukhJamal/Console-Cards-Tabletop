using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace ConsoleCards.Application.Results
{
    /// <summary>
    /// PRIVACY-P6C (s152): the result a command that was NOT run reports. A command result's default value has the
    /// status <see cref="CommandResultStatus.Accepted"/> (the enum's first value), so a refused command handed back as
    /// <c>default</c> reported "Accepted" to any caller reading its Status (Succeeded itself is false for the result
    /// types checked). This builds the same type with its status set to Rejected (in the result or its CommandResult).
    /// </summary>
    public static class RefusedResults
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static TResult Of<TResult>()
        {
            Type type = typeof(TResult);
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return default;
            object boxed = type.IsValueType ? (object)default(TResult) : FormatterServices.GetUninitializedObject(type);
            MarkRejected(boxed, 0);
            return (TResult)boxed;
        }

        private static bool MarkRejected(object target, int depth)
        {
            if (target == null) return false;
            FieldInfo[] fields = target.GetType().GetFields(Fields);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType != typeof(CommandResultStatus)) continue;
                fields[i].SetValue(target, CommandResultStatus.Rejected);
                return true;
            }

            if (depth >= 2) return false;
            for (int i = 0; i < fields.Length; i++)
            {
                Type fieldType = fields[i].FieldType;
                if (!fieldType.IsValueType || fieldType.IsPrimitive || fieldType.IsEnum) continue;
                object inner = fields[i].GetValue(target);
                if (!MarkRejected(inner, depth + 1)) continue;
                fields[i].SetValue(target, inner);
                return true;
            }

            return false;
        }
    }
}
