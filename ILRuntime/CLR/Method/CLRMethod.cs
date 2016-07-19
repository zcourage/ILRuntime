﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Mono.Cecil;
using ILRuntime.Runtime.Intepreter.OpCodes;
using ILRuntime.CLR.TypeSystem;
using ILRuntime.Runtime.Stack;
namespace ILRuntime.CLR.Method
{
    class CLRMethod : IMethod
    {
        MethodInfo def;
        ConstructorInfo cDef;
        List<IType> parameters;
        ILRuntime.Runtime.Enviorment.AppDomain appdomain;
        CLRType declaringType;
        ParameterInfo[] param;
        bool isConstructor;
        Func<object, object[], object> redirect;

        public IType DeclearingType
        {
            get
            {
                return declaringType;
            }
        }
        public string Name
        {
            get
            {
                return def.Name;
            }
        }
        public bool HasThis
        {
            get
            {
                return isConstructor ? !cDef.IsStatic : !def.IsStatic;
            }
        }
        public int GenericParameterCount
        {
            get
            {
                if (def.ContainsGenericParameters && def.IsGenericMethodDefinition)
                {
                    return def.GetGenericArguments().Length;
                }
                return 0;
            }
        }
        public bool IsGenericInstance
        {
            get
            {
                return false;
            }
        }

        public CLRMethod(MethodInfo def, CLRType type, ILRuntime.Runtime.Enviorment.AppDomain domain)
        {
            this.def = def;
            declaringType = type;
            this.appdomain = domain;
            param = def.GetParameters();
            if (!def.IsGenericMethod && !def.ContainsGenericParameters)
            {
                ReturnType = domain.GetType(def.ReturnType.FullName);
                if(def.Name.Contains("InitializeA"))
                {

                }
                InitParameters();
            }
            isConstructor = false;
        }
        public CLRMethod(ConstructorInfo def, CLRType type, ILRuntime.Runtime.Enviorment.AppDomain domain)
        {
            this.cDef = def;
            declaringType = type;
            this.appdomain = domain;
            param = def.GetParameters();
            if (!def.IsGenericMethod && !def.ContainsGenericParameters)
            {
                ReturnType = type;
                InitParameters();
            }
            isConstructor = true;
        }

        public int ParameterCount
        {
            get
            {
                return param != null ? param.Length : 0;
            }
        }


        public List<IType> Parameters
        {
            get
            {
                if (parameters == null)
                {
                    InitParameters();
                }
                return parameters;
            }
        }

        public IType ReturnType
        {
            get;
            private set;
        }

        public bool IsConstructor
        {
            get
            {
                return def.IsConstructor;
            }
        }

        void InitParameters()
        {
            parameters = new List<IType>();
            foreach (var i in param)
            {
                IType type = appdomain.GetType(i.ParameterType.FullName);
                parameters.Add(type);
            }
            if (def != null)
                appdomain.RedirectMap.TryGetValue(def, out redirect);
        }

        public unsafe object Invoke(StackObject* esp, List<object> mStack)
        {
            int paramCount = ParameterCount;
            object[] param = new object[paramCount];
            for (int i = 1; i <= paramCount; i++)
            {
                var p = esp - i;
                var obj = CheckPrimitiveTypes(this.param[i - 1].ParameterType, p->ToObject(appdomain, mStack));

                param[paramCount - i] = obj;
            }

            if (isConstructor)
            {
                var res = cDef.Invoke(param);
                return res;
            }
            else
            {
                object instance = null;

                if (!def.IsStatic)
                {
                    instance = CheckPrimitiveTypes(declaringType.TypeForCLR, (esp - paramCount - 1)->ToObject(appdomain, mStack));
                    if (instance == null)
                        throw new NullReferenceException();
                }
                object res = null;
                if (redirect != null)
                    res = redirect(instance, param);
                else
                    res = def.Invoke(instance, param);
                return res;
            }
        }

        public IMethod MakeGenericMethod(IType[] genericArguments)
        {
            Type[] p = new Type[genericArguments.Length];
            for(int i = 0; i < genericArguments.Length; i++)
            {
                p[i] = genericArguments[i].TypeForCLR;
            }
            var t = def.MakeGenericMethod(p);
            return new CLRMethod(t, declaringType, appdomain);            
        }

        object CheckPrimitiveTypes(Type pt, object obj)
        {
            if (pt.IsPrimitive)
            {
                if (pt == typeof(byte))
                    obj = (byte)(int)obj;
                else if (pt == typeof(short))
                    obj = (short)(int)obj;
                else if (pt == typeof(ushort))
                    obj = (ushort)(int)obj;
                else if (pt == typeof(sbyte))
                    obj = (sbyte)(int)obj;
                else if (pt == typeof(ulong))
                    obj = (ulong)(int)obj;
                else if (pt == typeof(bool))
                    obj = (int)obj == 1;
            }
            return obj;
        }
    }
}