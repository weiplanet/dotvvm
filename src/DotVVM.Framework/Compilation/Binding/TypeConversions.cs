﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using DotVVM.Framework.Utils;
using System.Reflection;

namespace DotVVM.Framework.Compilation.Binding
{
    internal class TypeConversion
    {
        private static Dictionary<Type, List<Type>> ImplicitNumericConversions = new Dictionary<Type, List<Type>>();
        private static readonly Dictionary<Type, int> typePrecedence = null;

        /// <summary>
        /// Performs implicit conversion between two expressions depending on their type precedence
        /// </summary>
        /// <param name="le"></param>
        /// <param name="re"></param>
        internal static void Convert(ref Expression le, ref Expression re)
        {
            if (typePrecedence.ContainsKey(le.Type) && typePrecedence.ContainsKey(re.Type))
            {
                if (typePrecedence[le.Type] > typePrecedence[re.Type]) re = Expression.Convert(re, le.Type);
                if (typePrecedence[le.Type] < typePrecedence[re.Type]) le = Expression.Convert(le, re.Type);
            }
        }

        /// <summary>
        /// Performs implicit conversion on an expression against a specified type
        /// </summary>
        /// <param name="le"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static Expression Convert(Expression le, Type type)
        {
            if (typePrecedence.ContainsKey(le.Type) && typePrecedence.ContainsKey(type))
            {
                if (typePrecedence[le.Type] < typePrecedence[type]) return Expression.Convert(le, type);
            }
            if (le.Type.IsNullable() && Nullable.GetUnderlyingType(le.Type) == type)
            {
                le = Expression.Property(le, "Value");
            }
            if (type.IsNullable() && Nullable.GetUnderlyingType(type) == le.Type)
            {
                le = Expression.Convert(le, type);
            }
            if (type == typeof(object))
            {
                return Expression.Convert(le, type);
            }
            if (le.Type == typeof(object))
            {
                return Expression.Convert(le, type);
            }
            return le;
        }

        /// <summary>
        /// Compares two types for implicit conversion
        /// </summary>
        /// <param name="from">The source type</param>
        /// <param name="to">The destination type</param>
        /// <returns>-1 if conversion is not possible, 0 if no conversion necessary, +1 if conversion possible</returns>
        internal static int CanConvert(Type from, Type to)
        {
            if (typePrecedence.ContainsKey(@from) && typePrecedence.ContainsKey(to))
            {
                return typePrecedence[to] - typePrecedence[@from];
            }
            else
            {
                if (@from == to) return 0;
                if (to.IsAssignableFrom(@from)) return 1;
            }
            return -1;
        }

        // 6.1.7 Boxing Conversions
        // A boxing conversion permits a value-type to be implicitly converted to a reference type. A boxing conversion exists from any non-nullable-value-type to object and dynamic,
        // to System.ValueType and to any interface-type implemented by the non-nullable-value-type.
        // Furthermore an enum-type can be converted to the type System.Enum.
        // A boxing conversion exists from a nullable-type to a reference type, if and only if a boxing conversion exists from the underlying non-nullable-value-type to the reference type.
        // A value type has a boxing conversion to an interface type I if it has a boxing conversion to an interface type I0 and I0 has an identity conversion to I.
        public static Expression BoxingConversion(Expression src, Type destType)
        {
            if (src.Type.GetTypeInfo().IsValueType && src.Type != typeof(void) && destType == typeof(object))
            {
                return Expression.Convert(src, destType);
            }
            return null;
        }

        //6.1.4 Nullable Type conversions
        public static Expression NullableConverion(Expression src, Type destType)
        {
            if (Nullable.GetUnderlyingType(src.Type) == destType)
            {
                return Expression.Property(src, "Value");
            }
            if (Nullable.GetUnderlyingType(destType) == src.Type)
            {
                return Expression.Convert(src, destType);
            }
            return null;
        }

        // 6.1.5 Null literal conversions
        // An implicit conversion exists from the null literal to any nullable type.
        // This conversion produces the null value (§4.1.10) of the given nullable type.
        public static Expression NullLiteralConverion(Expression src, Type destType)
        {
            if (src.NodeType == ExpressionType.Constant && src.Type == typeof(object) && ((ConstantExpression)src).Value == null && destType.IsNullable())
            {
                return Expression.Constant(Activator.CreateInstance(destType), destType);
            }
            return null;
        }

        public static Expression ReferenceConversion(Expression src, Type destType)
        {
            if (destType.IsAssignableFrom(src.Type) && src.Type != typeof(void))
            {
                return Expression.Convert(src, destType);
            }
            return null;
        }

        // 6.1 Implicit Conversions
        public static Expression ImplicitConversion(Expression src, Type destType, bool throwException = false, bool allowToString = false)
        {
            if (src is MethodGroupExpression)
            {
                return ((MethodGroupExpression)src).CreateDelegateExpression(destType, throwException);
            }
            if (src.Type == destType) return src;
            var result = ImplicitConstantConversion(src, destType) ??
                  ImplicitNumericConversion(src, destType) ??
                  NullableConverion(src, destType) ??
                  NullLiteralConverion(src, destType) ??
                  BoxingConversion(src, destType) ??
                  ReferenceConversion(src, destType);
            if (allowToString && destType == typeof(string) && (result == null || src.Type == typeof(object)))
            {
                result = ToStringConversion(src);
            }
            if (throwException && result == null) throw new InvalidOperationException($"Could not implicitly convert expression of type { src.Type } to { destType }.");
            return result;
        }

		public static bool IsStringConversionAllowed(Type fromType)
		{
			// allow primitive types, IConvertibles, types that override ToString
			return fromType.IsPrimitive || typeof(IConvertible).IsAssignableFrom(fromType) || fromType.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)?.DeclaringType != typeof(object);
		}

        public static Expression ToStringConversion(Expression src)
        {
			if (!IsStringConversionAllowed(src.Type)) return null;
            if (src.NodeType == ExpressionType.Constant)
            {
                var constant = (ConstantExpression)src;
                return Expression.Constant(System.Convert.ToString(constant.Value), typeof(string));
            }
            else return Expression.Call(typeof(Convert), "ToString", Type.EmptyTypes, Expression.Convert(src, typeof(object)));
        }

        // 6.1.9 Implicit constant expression conversions
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public static Expression ImplicitConstantConversion(Expression src, Type destType)
        {
            if (src.NodeType != ExpressionType.Constant)
                return null;

            var srcValue = ((ConstantExpression)src).Value;

            //An implicit constant expression conversion permits the following conversions:
            //	A constant-expression (§7.19) of type int can be converted to type sbyte, byte, short, ushort, uint, or ulong, provided the value of the constant-expression is within the range of the destination type.
            if (src.Type == typeof(int))
            {
                var value = (int)srcValue;
                if (destType == typeof(sbyte))
                {
                    if (value >= SByte.MinValue && value <= SByte.MinValue)
                    {
                        return Expression.Constant((sbyte)srcValue, typeof(sbyte));
                    }
                }
                if (destType == typeof(byte))
                {
                    if (value >= Byte.MinValue && value <= Byte.MaxValue)
                    {
                        return Expression.Constant((byte)srcValue, typeof(byte));
                    }
                }
                if (destType == typeof(short))
                {
                    if (value >= Int16.MinValue && value <= Int16.MaxValue)
                    {
                        return Expression.Constant((short)srcValue, typeof(short));
                    }
                }
                if (destType == typeof(ushort))
                {
                    if (value >= UInt16.MinValue && value <= UInt16.MaxValue)
                    {
                        return Expression.Constant((ushort)srcValue, typeof(ushort));
                    }
                }
                if (destType == typeof(uint))
                {
                    if (value >= uint.MinValue)
                    {
                        return Expression.Constant((uint)srcValue, typeof(uint));
                    }
                }
                if (destType == typeof(ulong))
                {
                    if (value >= 0)
                    {
                        return Expression.Constant((ulong)srcValue, typeof(ulong));
                    }
                }
            }
            //	A constant-expression of type long can be converted to type ulong, provided the value of the constant-expression is not negative.
            if (src.Type == typeof(long))
            {
                var value = (long)srcValue;
                if (destType == typeof(ulong))
                {
                    if (value >= 0)
                    {
                        return Expression.Constant((ulong)srcValue, typeof(ulong));
                    }
                }
            }

            // nonstandart implicit string conversions
            if (src.Type == typeof(string))
            {
                var value = (string)srcValue;
                // to enum
                if (destType.GetTypeInfo().IsEnum)
                {
                    // Enum.TryParse is generic and wants TEnum
                    try
                    {
                        var enumValue = Enum.Parse(destType, value);
                        return Expression.Constant(enumValue, destType);
                    }
                    catch { }
                }
                // to char
                if (destType == typeof(char) && value.Length == 1)
                {
                    return Expression.Constant(value[0]);
                }
            }
            return null;
        }

        public static Type GetBaseCommonType(IEnumerable<Expression> expressions)
        {
            Type baseType = null;

            foreach (var expression in expressions)
            {
                if (baseType == null)
                {
                    baseType = expression.Type;
                }
                else
                {
                    switch (CanConvert(expression.Type, baseType))
                    {
                        case 1:
                            baseType = expression.Type;
                            break;

                        case -1:
                            throw new Exception(string.Format("Cannot convert between types {0} and {1}", baseType.Name, expression.Type.Name));
                    }
                }
            }

            return baseType;
        }

        // 6.1.2 Implicit numeric conversions
        /// <summary>
        /// Tries to perform implicit numeric conversion
        /// </summary>
        public static Expression ImplicitNumericConversion(Expression src, Type target)
        {
            List<Type> allowed;
            if (ImplicitNumericConversions.TryGetValue(src.Type, out allowed))
            {
                if (allowed.Contains(target))
                {
                    return Expression.Convert(src, target);
                }
            }
            return null;
        }

        static TypeConversion()
        {
            typePrecedence = new Dictionary<Type, int>
            {
                    {typeof (object), 0},
                    {typeof (bool), 1},
                    {typeof (byte), 2},
                    {typeof (int), 3},
                    {typeof (short), 4},
                    {typeof (long), 5},
                    {typeof (float), 6},
                    {typeof (double), 7}
                };

            ImplicitNumericConversions.Add(typeof(sbyte), new List<Type>() { typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) });
            ImplicitNumericConversions.Add(typeof(byte), new List<Type>() { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) });
            ImplicitNumericConversions.Add(typeof(short), new List<Type>() { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) });
            ImplicitNumericConversions.Add(typeof(ushort), new List<Type>() { typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) });
            ImplicitNumericConversions.Add(typeof(int), new List<Type>() { typeof(long), typeof(float), typeof(double), typeof(decimal) });
            ImplicitNumericConversions.Add(typeof(uint), new List<Type>() { typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) });
            ImplicitNumericConversions.Add(typeof(long), new List<Type>() { typeof(float), typeof(double), typeof(decimal) });
            ImplicitNumericConversions.Add(typeof(ulong), new List<Type>() { typeof(float), typeof(double), typeof(decimal) });
            ImplicitNumericConversions.Add(typeof(char), new List<Type>() { typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) });
            ImplicitNumericConversions.Add(typeof(float), new List<Type>() { typeof(double) });
        }
    }
}