﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class RecordTests : CompilingTestBase
    {
        private static CSharpCompilation CreateCompilation(CSharpTestSource source)
            => CSharpTestBase.CreateCompilation(new[] { source, IsExternalInitTypeDefinition }, parseOptions: TestOptions.RegularPreview);

        private CompilationVerifier CompileAndVerify(CSharpTestSource src, string? expectedOutput = null)
            => base.CompileAndVerify(new[] { src, IsExternalInitTypeDefinition },
                expectedOutput: expectedOutput,
                parseOptions: TestOptions.RegularPreview,
                // init-only fails verification
                verify: Verification.Skipped);

        [Fact]
        public void GeneratedConstructor()
        {
            var comp = CreateCompilation(@"record C(int x, string y);");
            comp.VerifyDiagnostics();
            var c = comp.GlobalNamespace.GetTypeMember("C");
            var ctor = (MethodSymbol)c.GetMembers(".ctor")[0];
            Assert.Equal(2, ctor.ParameterCount);

            var x = ctor.Parameters[0];
            Assert.Equal(SpecialType.System_Int32, x.Type.SpecialType);
            Assert.Equal("x", x.Name);

            var y = ctor.Parameters[1];
            Assert.Equal(SpecialType.System_String, y.Type.SpecialType);
            Assert.Equal("y", y.Name);
        }

        [Fact]
        public void GeneratedConstructorDefaultValues()
        {
            var comp = CreateCompilation(@"record C<T>(int x, T t = default);");
            comp.VerifyDiagnostics();
            var c = comp.GlobalNamespace.GetTypeMember("C");
            Assert.Equal(1, c.Arity);
            var ctor = (MethodSymbol)c.GetMembers(".ctor")[0];
            Assert.Equal(0, ctor.Arity);
            Assert.Equal(2, ctor.ParameterCount);

            var x = ctor.Parameters[0];
            Assert.Equal(SpecialType.System_Int32, x.Type.SpecialType);
            Assert.Equal("x", x.Name);

            var t = ctor.Parameters[1];
            Assert.Equal(c.TypeParameters[0], t.Type);
            Assert.Equal("t", t.Name);
        }

        [Fact]
        public void RecordExistingConstructor1()
        {
            var comp = CreateCompilation(@"
record C(int x, string y)
{
    public C(int a, string b)
    {
    }
}");
            comp.VerifyDiagnostics(
                // (2,9): error CS8851: There cannot be a primary constructor and a member constructor with the same parameter types.
                // record C(int x, string y)
                Diagnostic(ErrorCode.ERR_DuplicateRecordConstructor, "(int x, string y)").WithLocation(2, 9)
            );
            var c = comp.GlobalNamespace.GetTypeMember("C");
            var ctor = (MethodSymbol)c.GetMembers(".ctor")[0];
            Assert.Equal(2, ctor.ParameterCount);

            var a = ctor.Parameters[0];
            Assert.Equal(SpecialType.System_Int32, a.Type.SpecialType);
            Assert.Equal("a", a.Name);

            var b = ctor.Parameters[1];
            Assert.Equal(SpecialType.System_String, b.Type.SpecialType);
            Assert.Equal("b", b.Name);
        }

        [Fact]
        public void RecordExistingConstructor01()
        {
            var comp = CreateCompilation(@"
record C(int x, string y)
{
    public C(int a, int b) // overload
    {
    }
}");
            comp.VerifyDiagnostics(
                // (4,12): error CS8862: A constructor declared in a record with parameters must have 'this' constructor initializer.
                //     public C(int a, int b) // overload
                Diagnostic(ErrorCode.ERR_UnexpectedOrMissingConstructorInitializerInRecord, "C").WithLocation(4, 12)
                );

            var c = comp.GlobalNamespace.GetTypeMember("C");
            var ctors = c.GetMembers(".ctor");
            Assert.Equal(3, ctors.Length);

            foreach (MethodSymbol ctor in ctors)
            {
                if (ctor.ParameterCount == 2)
                {
                    var p1 = ctor.Parameters[0];
                    Assert.Equal(SpecialType.System_Int32, p1.Type.SpecialType);
                    var p2 = ctor.Parameters[1];
                    if (ctor is SynthesizedRecordConstructor)
                    {
                        Assert.Equal("x", p1.Name);
                        Assert.Equal("y", p2.Name);
                        Assert.Equal(SpecialType.System_String, p2.Type.SpecialType);
                    }
                    else
                    {
                        Assert.Equal("a", p1.Name);
                        Assert.Equal("b", p2.Name);
                        Assert.Equal(SpecialType.System_Int32, p2.Type.SpecialType);
                    }
                }
                else
                {
                    Assert.Equal(1, ctor.ParameterCount);
                    Assert.True(c.Equals(ctor.Parameters[0].Type, TypeCompareKind.ConsiderEverything));
                }
            }
        }

        [Fact]
        public void GeneratedProperties()
        {
            var comp = CreateCompilation("record C(int x, int y);");
            comp.VerifyDiagnostics();
            var c = comp.GlobalNamespace.GetTypeMember("C");

            var x = (SourceOrRecordPropertySymbol)c.GetProperty("x");
            Assert.NotNull(x.GetMethod);
            Assert.Equal(MethodKind.PropertyGet, x.GetMethod.MethodKind);
            Assert.Equal(SpecialType.System_Int32, x.Type.SpecialType);
            Assert.False(x.IsReadOnly);
            Assert.False(x.IsWriteOnly);
            Assert.Equal(Accessibility.Public, x.DeclaredAccessibility);
            Assert.False(x.IsVirtual);
            Assert.False(x.IsStatic);
            Assert.Equal(c, x.ContainingType);
            Assert.Equal(c, x.ContainingSymbol);

            var backing = x.BackingField;
            Assert.Equal(x, backing.AssociatedSymbol);
            Assert.Equal(c, backing.ContainingSymbol);
            Assert.Equal(c, backing.ContainingType);

            var getAccessor = x.GetMethod;
            Assert.Equal(x, getAccessor.AssociatedSymbol);
            Assert.Equal(c, getAccessor.ContainingSymbol);
            Assert.Equal(c, getAccessor.ContainingType);
            Assert.Equal(Accessibility.Public, getAccessor.DeclaredAccessibility);

            var setAccessor = x.SetMethod;
            Assert.Equal(x, setAccessor.AssociatedSymbol);
            Assert.Equal(c, setAccessor.ContainingSymbol);
            Assert.Equal(c, setAccessor.ContainingType);
            Assert.Equal(Accessibility.Public, setAccessor.DeclaredAccessibility);
            Assert.True(setAccessor.IsInitOnly);

            var y = (SourceOrRecordPropertySymbol)c.GetProperty("y");
            Assert.NotNull(y.GetMethod);
            Assert.Equal(MethodKind.PropertyGet, y.GetMethod.MethodKind);
            Assert.Equal(SpecialType.System_Int32, y.Type.SpecialType);
            Assert.False(y.IsReadOnly);
            Assert.False(y.IsWriteOnly);
            Assert.Equal(Accessibility.Public, y.DeclaredAccessibility);
            Assert.False(x.IsVirtual);
            Assert.False(x.IsStatic);
            Assert.Equal(c, y.ContainingType);
            Assert.Equal(c, y.ContainingSymbol);

            backing = y.BackingField;
            Assert.Equal(y, backing.AssociatedSymbol);
            Assert.Equal(c, backing.ContainingSymbol);
            Assert.Equal(c, backing.ContainingType);

            getAccessor = y.GetMethod;
            Assert.Equal(y, getAccessor.AssociatedSymbol);
            Assert.Equal(c, getAccessor.ContainingSymbol);
            Assert.Equal(c, getAccessor.ContainingType);

            setAccessor = y.SetMethod;
            Assert.Equal(y, setAccessor.AssociatedSymbol);
            Assert.Equal(c, setAccessor.ContainingSymbol);
            Assert.Equal(c, setAccessor.ContainingType);
            Assert.Equal(Accessibility.Public, setAccessor.DeclaredAccessibility);
            Assert.True(setAccessor.IsInitOnly);
        }

        [Fact]
        public void RecordEquals_01()
        {
            CompileAndVerify(@"
using System;
record C(int X, int Y)
{
    public static void Main()
    {
        object c = new C(0, 0);
        Console.WriteLine(c.Equals(c));
    }
    public bool Equals(C c) => throw null;
    public override bool Equals(object o) => false;
}", expectedOutput: "False");
        }

        [Fact]
        public void RecordEquals_02()
        {
            CompileAndVerify(@"
using System;
record C(int X, int Y)
{
    public static void Main()
    {
        object c = new C(1, 1);
        var c2 = new C(1, 1);
        Console.WriteLine(c.Equals(c));
        Console.WriteLine(c.Equals(c2));
    }
}", expectedOutput: @"True
True");
        }

        [Fact]
        public void RecordEquals_03()
        {
            var verifier = CompileAndVerify(@"
using System;
record C(int X, int Y)
{
    public static void Main()
    {
        object c = new C(0, 0);
        var c2 = new C(0, 0);
        var c3 = new C(1, 1);
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals(c3));
    }
    public bool Equals(C c) => X == c.X && Y == c.Y;
}", expectedOutput: @"True
False");
            verifier.VerifyIL("C.Equals(object)", @"
{
  // Code size       13 (0xd)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  isinst     ""C""
  IL_0007:  call       ""bool C.Equals(C)""
  IL_000c:  ret
}");
            verifier.VerifyIL("C.Equals(C)", @"
{
  // Code size       31 (0x1f)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  call       ""int C.X.get""
  IL_0006:  ldarg.1
  IL_0007:  callvirt   ""int C.X.get""
  IL_000c:  bne.un.s   IL_001d
  IL_000e:  ldarg.0
  IL_000f:  call       ""int C.Y.get""
  IL_0014:  ldarg.1
  IL_0015:  callvirt   ""int C.Y.get""
  IL_001a:  ceq
  IL_001c:  ret
  IL_001d:  ldc.i4.0
  IL_001e:  ret
}");
        }

        [Fact]
        public void RecordEquals_04()
        {
            var verifier = CompileAndVerify(@"
using System;
record C(int X, int Y)
{
    public static void Main()
    {
        object c = new C(0, 0);
        var c2 = new C(0, 0);
        var c3 = new C(1, 1);
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals(c3));
    }
}", expectedOutput: @"True
False");
            verifier.VerifyIL("C.Equals(object)", @"
{
  // Code size       13 (0xd)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  isinst     ""C""
  IL_0007:  callvirt   ""bool C.Equals(C)""
  IL_000c:  ret
}");
            verifier.VerifyIL("C.Equals(C)", @"
{
  // Code size       66 (0x42)
  .maxstack  3
  IL_0000:  ldarg.1
  IL_0001:  brfalse.s  IL_0040
  IL_0003:  ldarg.0
  IL_0004:  callvirt   ""System.Type C.EqualityContract.get""
  IL_0009:  ldarg.1
  IL_000a:  callvirt   ""System.Type C.EqualityContract.get""
  IL_000f:  bne.un.s   IL_0040
  IL_0011:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_0016:  ldarg.0
  IL_0017:  ldfld      ""int C.<X>k__BackingField""
  IL_001c:  ldarg.1
  IL_001d:  ldfld      ""int C.<X>k__BackingField""
  IL_0022:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_0027:  brfalse.s  IL_0040
  IL_0029:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_002e:  ldarg.0
  IL_002f:  ldfld      ""int C.<Y>k__BackingField""
  IL_0034:  ldarg.1
  IL_0035:  ldfld      ""int C.<Y>k__BackingField""
  IL_003a:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_003f:  ret
  IL_0040:  ldc.i4.0
  IL_0041:  ret
}");
        }

        [Fact]
        public void RecordEquals_06()
        {
            var verifier = CompileAndVerify(@"
using System;
record C(int X, int Y)
{
    public static void Main()
    {
        var c = new C(0, 0);
        object c2 = null;
        C c3 = null;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals(c3));
    }
}", expectedOutput: @"False
False");
        }

        [Fact]
        public void RecordEquals_07()
        {
            var verifier = CompileAndVerify(@"
using System;
record C(int[] X, string Y)
{
    public static void Main()
    {
        var arr = new[] {1, 2};
        var c = new C(arr, ""abc"");
        var c2 = new C(new[] {1, 2}, ""abc"");
        var c3 = new C(arr, ""abc"");
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals(c3));
    }
}", expectedOutput: @"False
True");
        }

        [Fact]
        public void RecordEquals_08()
        {
            var verifier = CompileAndVerify(@"
using System;
record C(int X, int Y)
{
    public int Z;
    public static void Main()
    {
        var c = new C(1, 2);
        c.Z = 3;
        var c2 = new C(1, 2);
        c2.Z = 4;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
        c2.Z = 3;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
    }
}", expectedOutput: @"False
False
True
True");
            verifier.VerifyIL("C.Equals(C)", @"
{
  // Code size       90 (0x5a)
  .maxstack  3
  IL_0000:  ldarg.1
  IL_0001:  brfalse.s  IL_0058
  IL_0003:  ldarg.0
  IL_0004:  callvirt   ""System.Type C.EqualityContract.get""
  IL_0009:  ldarg.1
  IL_000a:  callvirt   ""System.Type C.EqualityContract.get""
  IL_000f:  bne.un.s   IL_0058
  IL_0011:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_0016:  ldarg.0
  IL_0017:  ldfld      ""int C.<X>k__BackingField""
  IL_001c:  ldarg.1
  IL_001d:  ldfld      ""int C.<X>k__BackingField""
  IL_0022:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_0027:  brfalse.s  IL_0058
  IL_0029:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_002e:  ldarg.0
  IL_002f:  ldfld      ""int C.<Y>k__BackingField""
  IL_0034:  ldarg.1
  IL_0035:  ldfld      ""int C.<Y>k__BackingField""
  IL_003a:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_003f:  brfalse.s  IL_0058
  IL_0041:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_0046:  ldarg.0
  IL_0047:  ldfld      ""int C.Z""
  IL_004c:  ldarg.1
  IL_004d:  ldfld      ""int C.Z""
  IL_0052:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_0057:  ret
  IL_0058:  ldc.i4.0
  IL_0059:  ret
}");
        }

        [Fact]
        public void RecordEquals_09()
        {
            var verifier = CompileAndVerify(@"
using System;
record C(int X, int Y)
{
    public int Z { get; set; }
    public static void Main()
    {
        var c = new C(1, 2);
        c.Z = 3;
        var c2 = new C(1, 2);
        c2.Z = 4;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
        c2.Z = 3;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
    }
}", expectedOutput: @"False
False
True
True");
        }

        [Fact]
        public void RecordEquals_10()
        {
            var verifier = CompileAndVerify(@"
using System;
record C(int X, int Y)
{
    public static int Z;
    public static void Main()
    {
        var c = new C(1, 2);
        C.Z = 3;
        var c2 = new C(1, 2);
        C.Z = 4;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
        C.Z = 3;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
    }
}", expectedOutput: @"True
True
True
True");
            verifier.VerifyIL("C.Equals(C)", @"
{
  // Code size       66 (0x42)
  .maxstack  3
  IL_0000:  ldarg.1
  IL_0001:  brfalse.s  IL_0040
  IL_0003:  ldarg.0
  IL_0004:  callvirt   ""System.Type C.EqualityContract.get""
  IL_0009:  ldarg.1
  IL_000a:  callvirt   ""System.Type C.EqualityContract.get""
  IL_000f:  bne.un.s   IL_0040
  IL_0011:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_0016:  ldarg.0
  IL_0017:  ldfld      ""int C.<X>k__BackingField""
  IL_001c:  ldarg.1
  IL_001d:  ldfld      ""int C.<X>k__BackingField""
  IL_0022:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_0027:  brfalse.s  IL_0040
  IL_0029:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_002e:  ldarg.0
  IL_002f:  ldfld      ""int C.<Y>k__BackingField""
  IL_0034:  ldarg.1
  IL_0035:  ldfld      ""int C.<Y>k__BackingField""
  IL_003a:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_003f:  ret
  IL_0040:  ldc.i4.0
  IL_0041:  ret
}");
        }

        [Fact]
        public void RecordEquals_11()
        {
            var verifier = CompileAndVerify(@"
using System;
using System.Collections.Generic;
record C(int X, int Y)
{
    static Dictionary<C, int> s_dict = new Dictionary<C, int>();
    public int Z { get => s_dict[this]; set => s_dict[this] = value; }
    public static void Main()
    {
        var c = new C(1, 2);
        c.Z = 3;
        var c2 = new C(1, 2);
        c2.Z = 4;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
        c2.Z = 3;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
    }
}", expectedOutput: @"True
True
True
True");
            verifier.VerifyIL("C.Equals(C)", @"
{
  // Code size       66 (0x42)
  .maxstack  3
  IL_0000:  ldarg.1
  IL_0001:  brfalse.s  IL_0040
  IL_0003:  ldarg.0
  IL_0004:  callvirt   ""System.Type C.EqualityContract.get""
  IL_0009:  ldarg.1
  IL_000a:  callvirt   ""System.Type C.EqualityContract.get""
  IL_000f:  bne.un.s   IL_0040
  IL_0011:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_0016:  ldarg.0
  IL_0017:  ldfld      ""int C.<X>k__BackingField""
  IL_001c:  ldarg.1
  IL_001d:  ldfld      ""int C.<X>k__BackingField""
  IL_0022:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_0027:  brfalse.s  IL_0040
  IL_0029:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_002e:  ldarg.0
  IL_002f:  ldfld      ""int C.<Y>k__BackingField""
  IL_0034:  ldarg.1
  IL_0035:  ldfld      ""int C.<Y>k__BackingField""
  IL_003a:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_003f:  ret
  IL_0040:  ldc.i4.0
  IL_0041:  ret
}");
        }

        [Fact]
        public void RecordEquals_12()
        {
            var verifier = CompileAndVerify(@"
using System;
using System.Collections.Generic;
record C(int X, int Y)
{
    private event Action E;
    public static void Main()
    {
        var c = new C(1, 2);
        c.E = () => { };
        var c2 = new C(1, 2);
        c2.E = () => { };
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
        c2.E = c.E;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
    }
}", expectedOutput: @"False
False
True
True");
            verifier.VerifyIL("C.Equals(C)", @"
{
  // Code size       90 (0x5a)
  .maxstack  3
  IL_0000:  ldarg.1
  IL_0001:  brfalse.s  IL_0058
  IL_0003:  ldarg.0
  IL_0004:  callvirt   ""System.Type C.EqualityContract.get""
  IL_0009:  ldarg.1
  IL_000a:  callvirt   ""System.Type C.EqualityContract.get""
  IL_000f:  bne.un.s   IL_0058
  IL_0011:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_0016:  ldarg.0
  IL_0017:  ldfld      ""int C.<X>k__BackingField""
  IL_001c:  ldarg.1
  IL_001d:  ldfld      ""int C.<X>k__BackingField""
  IL_0022:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_0027:  brfalse.s  IL_0058
  IL_0029:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_002e:  ldarg.0
  IL_002f:  ldfld      ""int C.<Y>k__BackingField""
  IL_0034:  ldarg.1
  IL_0035:  ldfld      ""int C.<Y>k__BackingField""
  IL_003a:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_003f:  brfalse.s  IL_0058
  IL_0041:  call       ""System.Collections.Generic.EqualityComparer<System.Action> System.Collections.Generic.EqualityComparer<System.Action>.Default.get""
  IL_0046:  ldarg.0
  IL_0047:  ldfld      ""System.Action C.E""
  IL_004c:  ldarg.1
  IL_004d:  ldfld      ""System.Action C.E""
  IL_0052:  callvirt   ""bool System.Collections.Generic.EqualityComparer<System.Action>.Equals(System.Action, System.Action)""
  IL_0057:  ret
  IL_0058:  ldc.i4.0
  IL_0059:  ret
}");
        }

        [Fact]
        public void RecordClone1()
        {
            var comp = CreateCompilation("record C(int x, int y);");
            comp.VerifyDiagnostics();

            var c = comp.GlobalNamespace.GetTypeMember("C");
            var clone = c.GetMethod(WellKnownMemberNames.CloneMethodName);
            Assert.Equal(0, clone.Arity);
            Assert.Equal(0, clone.ParameterCount);
            Assert.Equal(c, clone.ReturnType);

            var ctor = (MethodSymbol)c.GetMembers(".ctor")[1];
            Assert.Equal(1, ctor.ParameterCount);
            Assert.True(ctor.Parameters[0].Type.Equals(c, TypeCompareKind.ConsiderEverything));

            var verifier = CompileAndVerify(comp, verify: Verification.Fails);
            verifier.VerifyIL("C." + WellKnownMemberNames.CloneMethodName, @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  newobj     ""C..ctor(C)""
  IL_0006:  ret
}
");
            verifier.VerifyIL("C..ctor(C)", @"
{
  // Code size       31 (0x1f)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ldarg.0
  IL_0007:  ldarg.1
  IL_0008:  ldfld      ""int C.<x>k__BackingField""
  IL_000d:  stfld      ""int C.<x>k__BackingField""
  IL_0012:  ldarg.0
  IL_0013:  ldarg.1
  IL_0014:  ldfld      ""int C.<y>k__BackingField""
  IL_0019:  stfld      ""int C.<y>k__BackingField""
  IL_001e:  ret
}");
        }

        [Fact]
        public void RecordClone2_0()
        {
            var comp = CreateCompilation(@"
record C(int x, int y)
{
    public C(C other) : this(other.x, other.y) { }
}");
            comp.VerifyDiagnostics();

            var c = comp.GlobalNamespace.GetTypeMember("C");
            var clone = c.GetMethod(WellKnownMemberNames.CloneMethodName);
            Assert.Equal(0, clone.Arity);
            Assert.Equal(0, clone.ParameterCount);
            Assert.Equal(c, clone.ReturnType);

            var ctor = (MethodSymbol)c.GetMembers(".ctor")[0];
            Assert.Equal(1, ctor.ParameterCount);
            Assert.True(ctor.Parameters[0].Type.Equals(c, TypeCompareKind.ConsiderEverything));

            var verifier = CompileAndVerify(comp, verify: Verification.Fails);
            verifier.VerifyIL("C." + WellKnownMemberNames.CloneMethodName, @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  newobj     ""C..ctor(C)""
  IL_0006:  ret
}
");
            verifier.VerifyIL("C..ctor(C)", @"
{
  // Code size       19 (0x13)
  .maxstack  3
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  callvirt   ""int C.x.get""
  IL_0007:  ldarg.1
  IL_0008:  callvirt   ""int C.y.get""
  IL_000d:  call       ""C..ctor(int, int)""
  IL_0012:  ret
}
");
        }

        [Fact]
        [WorkItem(44781, "https://github.com/dotnet/roslyn/issues/44781")]
        public void RecordClone2_1()
        {
            var comp = CreateCompilation(@"
record C(int x, int y)
{
    public C(C other) { }
}");
            comp.VerifyDiagnostics(
                // (4,12): error CS8862: A constructor declared in a record with parameters must have 'this' constructor initializer.
                //     public C(C other) { }
                Diagnostic(ErrorCode.ERR_UnexpectedOrMissingConstructorInitializerInRecord, "C").WithLocation(4, 12)
                );
        }

        [Fact]
        [WorkItem(44781, "https://github.com/dotnet/roslyn/issues/44781")]
        public void RecordClone2_2()
        {
            var comp = CreateCompilation(@"
record C(int x, int y)
{
    public C(C other) : base() { }
}");
            comp.VerifyDiagnostics(
                // (4,25): error CS8862: A constructor declared in a record with parameters must have 'this' constructor initializer.
                //     public C(C other) : base() { }
                Diagnostic(ErrorCode.ERR_UnexpectedOrMissingConstructorInitializerInRecord, "base").WithLocation(4, 25)
                );
        }

        [Fact]
        [WorkItem(44782, "https://github.com/dotnet/roslyn/issues/44782")]
        public void RecordClone3()
        {
            var comp = CreateCompilation(@"
using System;
public record C(int x, int y)
{
    public event Action E;
    public int Z;
    public int W = 123;
}");
            comp.VerifyDiagnostics();

            var c = comp.GlobalNamespace.GetTypeMember("C");
            var clone = c.GetMethod(WellKnownMemberNames.CloneMethodName);
            Assert.Equal(0, clone.Arity);
            Assert.Equal(0, clone.ParameterCount);
            Assert.Equal(c, clone.ReturnType);

            var ctor = (MethodSymbol)c.GetMembers(".ctor")[1];
            Assert.Equal(1, ctor.ParameterCount);
            Assert.True(ctor.Parameters[0].Type.Equals(c, TypeCompareKind.ConsiderEverything));

            var verifier = CompileAndVerify(comp, verify: Verification.Fails);
            verifier.VerifyIL("C." + WellKnownMemberNames.CloneMethodName, @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  newobj     ""C..ctor(C)""
  IL_0006:  ret
}");
            verifier.VerifyIL("C..ctor(C)", @"
{
  // Code size       67 (0x43)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ldarg.0
  IL_0007:  ldarg.1
  IL_0008:  ldfld      ""int C.<x>k__BackingField""
  IL_000d:  stfld      ""int C.<x>k__BackingField""
  IL_0012:  ldarg.0
  IL_0013:  ldarg.1
  IL_0014:  ldfld      ""int C.<y>k__BackingField""
  IL_0019:  stfld      ""int C.<y>k__BackingField""
  IL_001e:  ldarg.0
  IL_001f:  ldarg.1
  IL_0020:  ldfld      ""System.Action C.E""
  IL_0025:  stfld      ""System.Action C.E""
  IL_002a:  ldarg.0
  IL_002b:  ldarg.1
  IL_002c:  ldfld      ""int C.Z""
  IL_0031:  stfld      ""int C.Z""
  IL_0036:  ldarg.0
  IL_0037:  ldarg.1
  IL_0038:  ldfld      ""int C.W""
  IL_003d:  stfld      ""int C.W""
  IL_0042:  ret
}
");
        }

        [Fact(Skip = "record struct")]
        public void RecordClone4_0()
        {
            var comp = CreateCompilation(@"
using System;
public data struct S(int x, int y)
{
    public event Action E;
    public int Z;
}");
            comp.VerifyDiagnostics(
                // (3,21): error CS0171: Field 'S.E' must be fully assigned before control is returned to the caller
                // public data struct S(int x, int y)
                Diagnostic(ErrorCode.ERR_UnassignedThis, "(int x, int y)").WithArguments("S.E").WithLocation(3, 21),
                // (3,21): error CS0171: Field 'S.Z' must be fully assigned before control is returned to the caller
                // public data struct S(int x, int y)
                Diagnostic(ErrorCode.ERR_UnassignedThis, "(int x, int y)").WithArguments("S.Z").WithLocation(3, 21),
                // (5,25): warning CS0067: The event 'S.E' is never used
                //     public event Action E;
                Diagnostic(ErrorCode.WRN_UnreferencedEvent, "E").WithArguments("S.E").WithLocation(5, 25)
            );

            var s = comp.GlobalNamespace.GetTypeMember("S");
            var clone = s.GetMethod(WellKnownMemberNames.CloneMethodName);
            Assert.Equal(0, clone.Arity);
            Assert.Equal(0, clone.ParameterCount);
            Assert.Equal(s, clone.ReturnType);

            var ctor = (MethodSymbol)s.GetMembers(".ctor")[1];
            Assert.Equal(1, ctor.ParameterCount);
            Assert.True(ctor.Parameters[0].Type.Equals(s, TypeCompareKind.ConsiderEverything));
        }

        [Fact(Skip = "record struct")]
        public void RecordClone4_1()
        {
            var comp = CreateCompilation(@"
using System;
public data struct S(int x, int y)
{
    public event Action E = null;
    public int Z = 0;
}");
            comp.VerifyDiagnostics(
                // (5,25): error CS0573: 'S': cannot have instance property or field initializers in structs
                //     public event Action E = null;
                Diagnostic(ErrorCode.ERR_FieldInitializerInStruct, "E").WithArguments("S").WithLocation(5, 25),
                // (5,25): warning CS0414: The field 'S.E' is assigned but its value is never used
                //     public event Action E = null;
                Diagnostic(ErrorCode.WRN_UnreferencedFieldAssg, "E").WithArguments("S.E").WithLocation(5, 25),
                // (6,16): error CS0573: 'S': cannot have instance property or field initializers in structs
                //     public int Z = 0;
                Diagnostic(ErrorCode.ERR_FieldInitializerInStruct, "Z").WithArguments("S").WithLocation(6, 16)
                );
        }

        [Fact]
        public void NominalRecordEquals()
        {
            var verifier = CompileAndVerify(@"
using System;
record C
{
    private int X;
    private int Y { get; set; }
    private event Action E;

    public static void Main()
    {
        var c = new C { X = 1, Y = 2 };
        c.E = () => { };
        var c2 = new C { X = 1, Y = 2 };
        c2.E = () => { };
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
        c2.E = c.E;
        Console.WriteLine(c.Equals(c2));
        Console.WriteLine(c.Equals((object)c2));
    }
}", expectedOutput: @"False
False
True
True");
            verifier.VerifyIL("C.Equals(object)", @"
{
  // Code size       13 (0xd)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  isinst     ""C""
  IL_0007:  callvirt   ""bool C.Equals(C)""
  IL_000c:  ret
}");
            verifier.VerifyIL("C.Equals(C)", @"
{
  // Code size       90 (0x5a)
  .maxstack  3
  IL_0000:  ldarg.1
  IL_0001:  brfalse.s  IL_0058
  IL_0003:  ldarg.0
  IL_0004:  callvirt   ""System.Type C.EqualityContract.get""
  IL_0009:  ldarg.1
  IL_000a:  callvirt   ""System.Type C.EqualityContract.get""
  IL_000f:  bne.un.s   IL_0058
  IL_0011:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_0016:  ldarg.0
  IL_0017:  ldfld      ""int C.X""
  IL_001c:  ldarg.1
  IL_001d:  ldfld      ""int C.X""
  IL_0022:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_0027:  brfalse.s  IL_0058
  IL_0029:  call       ""System.Collections.Generic.EqualityComparer<int> System.Collections.Generic.EqualityComparer<int>.Default.get""
  IL_002e:  ldarg.0
  IL_002f:  ldfld      ""int C.<Y>k__BackingField""
  IL_0034:  ldarg.1
  IL_0035:  ldfld      ""int C.<Y>k__BackingField""
  IL_003a:  callvirt   ""bool System.Collections.Generic.EqualityComparer<int>.Equals(int, int)""
  IL_003f:  brfalse.s  IL_0058
  IL_0041:  call       ""System.Collections.Generic.EqualityComparer<System.Action> System.Collections.Generic.EqualityComparer<System.Action>.Default.get""
  IL_0046:  ldarg.0
  IL_0047:  ldfld      ""System.Action C.E""
  IL_004c:  ldarg.1
  IL_004d:  ldfld      ""System.Action C.E""
  IL_0052:  callvirt   ""bool System.Collections.Generic.EqualityComparer<System.Action>.Equals(System.Action, System.Action)""
  IL_0057:  ret
  IL_0058:  ldc.i4.0
  IL_0059:  ret
}");
        }

        [Fact]
        public void PositionalAndNominalSameEquals()
        {
            var v1 = CompileAndVerify(@"
using System;
record C(int X, string Y)
{
    public event Action E;
}
");
            var v2 = CompileAndVerify(@"
using System;
record C
{
    public int X { get; }
    public string Y { get; }
    public event Action E;
}");
            Assert.Equal(v1.VisualizeIL("C.Equals(C)"), v2.VisualizeIL("C.Equals(C)"));
            Assert.Equal(v1.VisualizeIL("C.Equals(object)"), v2.VisualizeIL("C.Equals(object)"));
        }

        [Fact]
        public void NominalRecordMembers()
        {
            var comp = CreateCompilation(@"
#nullable enable
record C
{
    public int X { get; init; }
    public string Y { get; init; }
}");
            var members = comp.GlobalNamespace.GetTypeMember("C").GetMembers();
            AssertEx.Equal(new[] {
                "C! C.<>Clone()",
                "System.Type! C.EqualityContract.get",
                "System.Type! C.EqualityContract { get; }",
                "System.Int32 C.<X>k__BackingField",
                "System.Int32 C.X { get; init; }",
                "System.Int32 C.X.get",
                "void C.X.init",
                "System.String! C.<Y>k__BackingField",
                "System.String! C.Y { get; init; }",
                "System.String! C.Y.get",
                "void C.Y.init",
                "System.Int32 C.GetHashCode()",
                "System.Boolean C.Equals(System.Object? )",
                "System.Boolean C.Equals(C? )",
                "C.C(C! )",
                "C.C()",
            }, members.Select(m => m.ToTestDisplayString(includeNonNullable: true)));
        }

        [Fact]
        public void PartialTypes_01()
        {
            var src = @"
using System;
partial record C(int X, int Y)
{
    public static void Main()
    {
        var c = new C(1, 2);
        Console.WriteLine(c.X);
        Console.WriteLine(c.Y);
    }
}

partial record C(int X, int Y)
{
}
";
            var comp = CreateCompilation(src);
            comp.VerifyDiagnostics(
                // (13,17): error CS8863: Only a single record partial declaration may have a parameter list
                // partial record C(int X, int Y)
                Diagnostic(ErrorCode.ERR_MultipleRecordParameterLists, "(int X, int Y)").WithLocation(13, 17)
                );

            Assert.Equal(new[] { "C..ctor(System.Int32 X, System.Int32 Y)", "C..ctor(C )" }, comp.GetTypeByMetadataName("C")!.Constructors.Select(m => m.ToTestDisplayString()));
        }

        [Fact]
        public void PartialTypes_02()
        {
            var src = @"
using System;
partial record C(int X, int Y)
{
    public static void Main()
    {
        var c = new C(1, 2);
        Console.WriteLine(c.X);
        Console.WriteLine(c.Y);
    }
}

partial record C(int X)
{
}
";
            var comp = CreateCompilation(src);
            comp.VerifyDiagnostics(
                // (13,17): error CS8863: Only a single record partial declaration may have a parameter list
                // partial record C(int X)
                Diagnostic(ErrorCode.ERR_MultipleRecordParameterLists, "(int X)").WithLocation(13, 17)
                );

            Assert.Equal(new[] { "C..ctor(System.Int32 X, System.Int32 Y)", "C..ctor(C )" }, comp.GetTypeByMetadataName("C")!.Constructors.Select(m => m.ToTestDisplayString()));
        }

        [Fact]
        public void PartialTypes_03()
        {
            var src = @"
using System;
partial record C
{
    public int X = 1;
}
partial record C(int Y);
partial record C
{
    public int Z { get; } = 2;
}";
            var verifier = CompileAndVerify(src);
            verifier.VerifyIL("C..ctor(int)", @"
{
  // Code size       28 (0x1c)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldc.i4.1
  IL_0002:  stfld      ""int C.X""
  IL_0007:  ldarg.0
  IL_0008:  ldarg.1
  IL_0009:  stfld      ""int C.<Y>k__BackingField""
  IL_000e:  ldarg.0
  IL_000f:  ldc.i4.2
  IL_0010:  stfld      ""int C.<Z>k__BackingField""
  IL_0015:  ldarg.0
  IL_0016:  call       ""object..ctor()""
  IL_001b:  ret
}");
        }

        [Fact]
        public void DataClassAndStruct()
        {
            var src = @"
data class C1 { }
data class C2(int X, int Y);
data struct S1 { }
data struct S2(int X, int Y);";
            var comp = CreateCompilation(src);
            comp.VerifyDiagnostics(
                // error CS8805: Program using top-level statements must be an executable.
                Diagnostic(ErrorCode.ERR_SimpleProgramNotAnExecutable).WithLocation(1, 1),
                // (2,1): error CS0116: A namespace cannot directly contain members such as fields or methods
                // data class C1 { }
                Diagnostic(ErrorCode.ERR_NamespaceUnexpected, "data").WithLocation(2, 1),
                // (3,1): error CS0116: A namespace cannot directly contain members such as fields or methods
                // data class C2(int X, int Y);
                Diagnostic(ErrorCode.ERR_NamespaceUnexpected, "data").WithLocation(3, 1),
                // (3,14): error CS1514: { expected
                // data class C2(int X, int Y);
                Diagnostic(ErrorCode.ERR_LbraceExpected, "(").WithLocation(3, 14),
                // (3,14): error CS1513: } expected
                // data class C2(int X, int Y);
                Diagnostic(ErrorCode.ERR_RbraceExpected, "(").WithLocation(3, 14),
                // (3,14): error CS8803: Top-level statements must precede namespace and type declarations.
                // data class C2(int X, int Y);
                Diagnostic(ErrorCode.ERR_TopLevelStatementAfterNamespaceOrType, "(int X, int Y);").WithLocation(3, 14),
                // (3,14): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
                // data class C2(int X, int Y);
                Diagnostic(ErrorCode.ERR_IllegalStatement, "(int X, int Y)").WithLocation(3, 14),
                // (3,15): error CS8185: A declaration is not allowed in this context.
                // data class C2(int X, int Y);
                Diagnostic(ErrorCode.ERR_DeclarationExpressionNotPermitted, "int X").WithLocation(3, 15),
                // (3,15): error CS0165: Use of unassigned local variable 'X'
                // data class C2(int X, int Y);
                Diagnostic(ErrorCode.ERR_UseDefViolation, "int X").WithArguments("X").WithLocation(3, 15),
                // (3,22): error CS8185: A declaration is not allowed in this context.
                // data class C2(int X, int Y);
                Diagnostic(ErrorCode.ERR_DeclarationExpressionNotPermitted, "int Y").WithLocation(3, 22),
                // (3,22): error CS0165: Use of unassigned local variable 'Y'
                // data class C2(int X, int Y);
                Diagnostic(ErrorCode.ERR_UseDefViolation, "int Y").WithArguments("Y").WithLocation(3, 22),
                // (4,1): error CS0116: A namespace cannot directly contain members such as fields or methods
                // data struct S1 { }
                Diagnostic(ErrorCode.ERR_NamespaceUnexpected, "data").WithLocation(4, 1),
                // (5,1): error CS0116: A namespace cannot directly contain members such as fields or methods
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_NamespaceUnexpected, "data").WithLocation(5, 1),
                // (5,15): error CS1514: { expected
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_LbraceExpected, "(").WithLocation(5, 15),
                // (5,15): error CS1513: } expected
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_RbraceExpected, "(").WithLocation(5, 15),
                // (5,15): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_IllegalStatement, "(int X, int Y)").WithLocation(5, 15),
                // (5,16): error CS8185: A declaration is not allowed in this context.
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_DeclarationExpressionNotPermitted, "int X").WithLocation(5, 16),
                // (5,16): error CS0165: Use of unassigned local variable 'X'
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_UseDefViolation, "int X").WithArguments("X").WithLocation(5, 16),
                // (5,20): error CS0128: A local variable or function named 'X' is already defined in this scope
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_LocalDuplicate, "X").WithArguments("X").WithLocation(5, 20),
                // (5,23): error CS8185: A declaration is not allowed in this context.
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_DeclarationExpressionNotPermitted, "int Y").WithLocation(5, 23),
                // (5,23): error CS0165: Use of unassigned local variable 'Y'
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_UseDefViolation, "int Y").WithArguments("Y").WithLocation(5, 23),
                // (5,27): error CS0128: A local variable or function named 'Y' is already defined in this scope
                // data struct S2(int X, int Y);
                Diagnostic(ErrorCode.ERR_LocalDuplicate, "Y").WithArguments("Y").WithLocation(5, 27)
            );
        }

        [Fact]
        public void RecordInheritance()
        {
            var src = @"
class A { }
record B : A { }
record C : B { }
class D : C { }
interface E : C { }
struct F : C { }
enum G : C { }";

            var comp = CreateCompilation(src);
            comp.VerifyDiagnostics(
                // (3,12): error CS8864: Records may only inherit from object or another record
                // record B : A { }
                Diagnostic(ErrorCode.ERR_BadRecordBase, "A").WithLocation(3, 12),
                // (5,11): error CS8865: Only records may inherit from records.
                // class D : C { }
                Diagnostic(ErrorCode.ERR_BadInheritanceFromRecord, "C").WithLocation(5, 11),
                // (6,15): error CS0527: Type 'C' in interface list is not an interface
                // interface E : C { }
                Diagnostic(ErrorCode.ERR_NonInterfaceInInterfaceList, "C").WithArguments("C").WithLocation(6, 15),
                // (7,12): error CS0527: Type 'C' in interface list is not an interface
                // struct F : C { }
                Diagnostic(ErrorCode.ERR_NonInterfaceInInterfaceList, "C").WithArguments("C").WithLocation(7, 12),
                // (8,10): error CS1008: Type byte, sbyte, short, ushort, int, uint, long, or ulong expected
                // enum G : C
                Diagnostic(ErrorCode.ERR_IntegralTypeExpected, "C").WithLocation(8, 10)
            );
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RecordInheritance2(bool emitReference)
        {
            var src = @"
public class A { }
public record B { }
public record C : B { }";
            var comp = CreateCompilation(src);

            var src2 = @"
record D : C { }
record E : A { }
interface F : C { }
struct G : C { }
enum H : C { }
";

            var comp2 = CreateCompilation(src2,
                parseOptions: TestOptions.RegularPreview,
                references: new[] {
                emitReference ? comp.EmitToImageReference() : comp.ToMetadataReference()
            });

            comp2.VerifyDiagnostics(
                // (3,12): error CS8864: Records may only inherit from object or another record
                // record E : A { }
                Diagnostic(ErrorCode.ERR_BadRecordBase, "A").WithLocation(3, 12),
                // (4,15): error CS0527: Type 'C' in interface list is not an interface
                // interface E : C { }
                Diagnostic(ErrorCode.ERR_NonInterfaceInInterfaceList, "C").WithArguments("C").WithLocation(4, 15),
                // (5,12): error CS0527: Type 'C' in interface list is not an interface
                // struct F : C { }
                Diagnostic(ErrorCode.ERR_NonInterfaceInInterfaceList, "C").WithArguments("C").WithLocation(5, 12),
                // (6,10): error CS1008: Type byte, sbyte, short, ushort, int, uint, long, or ulong expected
                // enum G : C
                Diagnostic(ErrorCode.ERR_IntegralTypeExpected, "C").WithLocation(6, 10)
            );
        }

        [Fact]
        public void GenericRecord()
        {
            var src = @"
using System;
record A<T>
{
    public T Prop { get; init; }
}
record B : A<int>;
record C<T>(T Prop2) : A<T>;
class P
{
    public static void Main()
    {
        var a = new A<int>() { Prop = 1 };
        var a2 = a with { Prop = 2 };
        Console.WriteLine(a.Prop + "" "" + a2.Prop);

        var b = new B() { Prop = 3 };
        var b2 = b with { Prop = 4 };
        Console.WriteLine(b.Prop + "" "" + b2.Prop);

        var c = new C<int>(5) { Prop = 6 };
        var c2 = c with { Prop = 7, Prop2 = 8 };
        Console.WriteLine(c.Prop + "" "" + c.Prop2);
        Console.WriteLine(c2.Prop2 + "" "" + c2.Prop);
    }
}";
            CompileAndVerify(src, expectedOutput: @"
1 2
3 4
6 5
8 7");
        }

        [Fact]
        public void RecordCloneSymbol()
        {
            var src = @"
record R;
record R2 : R";
            var comp = CreateCompilation(src);
            var r = comp.GlobalNamespace.GetTypeMember("R");
            var clone = (MethodSymbol)r.GetMembers(WellKnownMemberNames.CloneMethodName).Single();
            Assert.False(clone.IsOverride);
            Assert.True(clone.IsVirtual);
            Assert.False(clone.IsAbstract);
            Assert.Equal(0, clone.ParameterCount);
            Assert.Equal(0, clone.Arity);

            var r2 = comp.GlobalNamespace.GetTypeMember("R2");
            var clone2 = (MethodSymbol)r2.GetMembers(WellKnownMemberNames.CloneMethodName).Single();
            Assert.True(clone2.IsOverride);
            Assert.False(clone2.IsVirtual);
            Assert.False(clone2.IsAbstract);
            Assert.Equal(0, clone2.ParameterCount);
            Assert.Equal(0, clone2.Arity);
            Assert.True(clone2.OverriddenMethod.Equals(clone, TypeCompareKind.ConsiderEverything));
        }
    }
}
