﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Utils;

namespace MonoMod.Cil {

    public static class FastDelegateInvokers {
        private static readonly MethodInfo[] actions;
        private static readonly MethodInfo[] funcs;
        static FastDelegateInvokers() {
            IEnumerable<MethodInfo> invokers = typeof(FastDelegateInvokers).GetMethods().Where(m => m.Name == "Invoke").OrderBy(m => m.GetParameters().Length);
            actions = invokers.Where(m => m.ReturnType == typeof(void)).ToArray();
            funcs = invokers.Where(m => m.ReturnType != typeof(void)).ToArray();
        }

        public static MethodInfo? GetInvoker(MethodInfo signature) {
            if ((signature.ReturnTypeCustomAttributes.GetCustomAttributes(true)?.Length ?? 0) > 0 ||
                signature.ReturnType.IsByRef || signature.ReturnType.IsMarshalByRef)
                return null;

            var isFunc = signature.ReturnType != typeof(void);
            ParameterInfo[] sigParams = signature.GetParameters();
            var numParams = sigParams.Length;
            if (numParams > actions.Length)
                return null;

            var genericParams = new Type[sigParams.Length + (isFunc ? 1 : 0)];
            for (var i = 0; i < numParams; i++) {
                ParameterInfo sigParam = sigParams[i];
                if (sigParam.Attributes != System.Reflection.ParameterAttributes.None ||
                    (sigParam.GetCustomAttributes(true)?.Length ?? 0) > 0 ||
                    // In, Out, Lcid and Retval are ParameterAttributes - ref isn't.
                    sigParam.ParameterType.IsByRef || sigParam.ParameterType.IsMarshalByRef)
                    return null;

                genericParams[i] = sigParam.ParameterType;
            }

            if (isFunc)
                genericParams[numParams] = signature.ReturnType;

            return (isFunc ? funcs : actions)[numParams - 1].MakeGenericMethod(genericParams);
        }

        private static readonly Dictionary<Type, WeakReference?> invokerCache = new();
        public static MethodInfo? GetDelegateInvoker<T>() where T : Delegate {
            Type t = typeof(T);
            MethodInfo? invoker;

            if (invokerCache.TryGetValue(t, out var invokerRef)) {
                if (invokerRef == null)
                    return null;

                invoker = (MethodInfo?) invokerRef.Target;
                if (invoker is not null)
                    return invoker;
            }

            var delInvoke = t.GetMethod("Invoke")!;
            ParameterInfo[] args = delInvoke.GetParameters();
            if (args.Length == 0) {
                invokerCache[t] = null;
                return null;
            }

            invoker = GetInvoker(delInvoke);
            if (invoker != null) {
                invokerCache[t] = new WeakReference(invoker);
                return invoker;
            }

            var argTypes = new Type[args.Length + 1];
            for (var i = 0; i < args.Length; i++)
                argTypes[i] = args[i].ParameterType;
            argTypes[args.Length] = typeof(T);

            using (var dmdInvoke = new DynamicMethodDefinition(
                $"MMIL:Invoke<{delInvoke.DeclaringType?.FullName}>",
                delInvoke.ReturnType, argTypes
            )) {
                ILProcessor il = dmdInvoke.GetILProcessor();

                // Load the delegate reference first.
                il.Emit(OpCodes.Ldarg, args.Length);

                // Load the rest of the args
                for (var i = 0; i < args.Length; i++)
                    il.Emit(OpCodes.Ldarg, i);

                // Invoke the delegate and return its result.
                il.Emit(OpCodes.Callvirt, delInvoke);
                il.Emit(OpCodes.Ret);

                invoker = dmdInvoke.Generate();
                invokerCache[t] = new WeakReference(invoker);
                return invoker;
            }
        }


        public delegate void Action<T1>(T1 arg1);
        public static void Invoke<T1>(T1 arg1, Action<T1> del) => del(arg1);
        public delegate TResult Func<T1, TResult>(T1 arg1);
        public static TResult Invoke<T1, TResult>(T1 arg1, Func<T1, TResult> del) => del(arg1);
        public delegate void Action<T1, T2>(T1 arg1, T2 arg2);
        public static void Invoke<T1, T2>(T1 arg1, T2 arg2, Action<T1, T2> del) => del(arg1, arg2);
        public delegate TResult Func<T1, T2, TResult>(T1 arg1, T2 arg2);
        public static TResult Invoke<T1, T2, TResult>(T1 arg1, T2 arg2, Func<T1, T2, TResult> del) => del(arg1, arg2);
        public delegate void Action<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3);
        public static void Invoke<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3, Action<T1, T2, T3> del) => del(arg1, arg2, arg3);
        public delegate TResult Func<T1, T2, T3, TResult>(T1 arg1, T2 arg2, T3 arg3);
        public static TResult Invoke<T1, T2, T3, TResult>(T1 arg1, T2 arg2, T3 arg3, Func<T1, T2, T3, TResult> del) => del(arg1, arg2, arg3);
        public delegate void Action<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
        public static void Invoke<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, Action<T1, T2, T3, T4> del) => del(arg1, arg2, arg3, arg4);
        public delegate TResult Func<T1, T2, T3, T4, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
        public static TResult Invoke<T1, T2, T3, T4, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, Func<T1, T2, T3, T4, TResult> del) => del(arg1, arg2, arg3, arg4);
        public delegate void Action<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
        public static void Invoke<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, Action<T1, T2, T3, T4, T5> del) => del(arg1, arg2, arg3, arg4, arg5);
        public delegate TResult Func<T1, T2, T3, T4, T5, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
        public static TResult Invoke<T1, T2, T3, T4, T5, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, Func<T1, T2, T3, T4, T5, TResult> del) => del(arg1, arg2, arg3, arg4, arg5);
        public delegate void Action<T1, T2, T3, T4, T5, T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
        public static void Invoke<T1, T2, T3, T4, T5, T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, Action<T1, T2, T3, T4, T5, T6> del) => del(arg1, arg2, arg3, arg4, arg5, arg6);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, Func<T1, T2, T3, T4, T5, T6, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, Action<T1, T2, T3, T4, T5, T6, T7> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, Func<T1, T2, T3, T4, T5, T6, T7, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7, T8>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, Action<T1, T2, T3, T4, T5, T6, T7, T8> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7, T8, T9>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15);
        public delegate void Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16);
        public static void Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16);
        public delegate TResult Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16);
        public static TResult Invoke<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult> del) => del(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16);
    }
}
