﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using PortaCapena.OdooJsonRpcClient.Attributes;
using PortaCapena.OdooJsonRpcClient.Extensions;

namespace PortaCapena.OdooJsonRpcClient.Models
{
    public class OdooCreateDictionary : Dictionary<string, object>
    {
        public string TableName { get; private set; }

        public OdooCreateDictionary() { }

        public OdooCreateDictionary(string tableName)
        {
            TableName = tableName;
        }

        public static OdooCreateDictionary Create()
        {
            return new OdooCreateDictionary();
        }

        public static OdooCreateDictionary Create(string tableName)
        {
            return new OdooCreateDictionary(tableName);
        }

        public static OdooCreateDictionary Create<T>(Expression<Func<T>> expression) where T : IOdooModel, new()
        {
            return Create().Add(expression);
        }

        public OdooCreateDictionary Add<T>(Expression<Func<T>> expression, object value) where T : IOdooModel
        {
            if (TableName != null && TryGetOdooTableName(expression, out var tableName))
                TableName = tableName;
            Add(OdooExtensions.GetOdooPropertyName(expression), value);
            return this;
        }

        public OdooCreateDictionary Add<T>(Expression<Func<T>> expression) where T : IOdooModel, new()
        {
            if (TableName == null && TryGetOdooTableName(expression, out var tableName))
                TableName = tableName;

            if (!(expression.Body is MemberInitExpression body)) throw new ArgumentException("invalid func");

            foreach (var memberExpression in body.Bindings)
            {
                if (memberExpression is MemberAssignment memberExp)
                {
                    var property = (PropertyInfo)memberExpression.Member;
                    var attribute = property.GetCustomAttributes<JsonPropertyAttribute>();

                    var odooName = attribute.FirstOrDefault()?.PropertyName;

                    if (odooName != null)
                    {
                        if (memberExp.Expression is ConstantExpression constantExpression)
                        {
                            var value = constantExpression.Value;
                            Add(odooName, value);
                            continue;
                        }
                        else if (memberExp.Expression is MemberExpression memberExpr)
                        {
                            var value = Expression.Lambda(memberExpr).Compile().DynamicInvoke();
                            Add(odooName, value);
                            continue;
                        }
                        else if (memberExp.Expression is UnaryExpression unaryExpression)
                        {
                            var value = Expression.Lambda(unaryExpression).Compile().DynamicInvoke();
                            Add(odooName, value);
                            continue;
                        }
                    }
                }
                throw new ArgumentException("invalid func");
            }
            return this;
        }


        private static bool TryGetOdooTableName<T>(Expression<Func<T>> expression, out string result)
        {
            result = null;
            var tableNameAttribute = expression.ReturnType.GetCustomAttributes(typeof(OdooTableNameAttribute), true).FirstOrDefault() as OdooTableNameAttribute;
            if (tableNameAttribute == null) return false;

            result = tableNameAttribute.Name;
            return true;
        }
    }
}