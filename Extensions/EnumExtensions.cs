using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;


namespace NTH.Extensions
{
    public static class EnumExtensions
    {
        public static T GetAttribute<T>(this Enum enumValue) where T : Attribute
        {
            Type type = enumValue.GetType();
            FieldInfo field = type.GetField(enumValue.ToString());
            object obj = field.GetCustomAttributes(typeof(T), inherit: false).SingleOrDefault();
            return (T)obj;
        }

        public static string GetEnumDescription(this Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());
            DescriptionAttribute[] array = (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false);
            if (array != null && array.Length != 0)
            {
                return array[0].Description;
            }

            return value.ToString();
        }

        public static bool IsSet(this Enum input, Enum matchTo)
        {
            return (Convert.ToUInt32(input) & Convert.ToUInt32(matchTo)) != 0;
        }

        public static bool IsMarkedWithAttribute<T>(this Enum enumValue) where T : Attribute
        {
            Type type = enumValue.GetType();
            FieldInfo field = type.GetField(enumValue.ToString());
            return Attribute.IsDefined(field, typeof(T));
        }
    }
}
