# Method: GetVisible (3 overloads)
**Type**: `scrVisualDecoration` | **Assembly**: `Assembly-CSharp`

### Overload 1

```il
	.method public hidebysig newslot virtual 
		instance bool GetVisible () cil managed 
	{
		// Method begins at RVA 0x5cf3f
		// Header size: 1
		// Code size: 27 (0x1b)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld bool scrDecoration::rendererEnabled
		IL_0006: brfalse.s IL_0019

		IL_0008: ldarg.0
		IL_0009: callvirt instance float32 scrDecoration::GetAlpha()
		IL_000e: ldc.r4 0.0
		IL_0013: ceq
		IL_0015: ldc.i4.0
		IL_0016: ceq
		IL_0018: ret

		IL_0019: ldc.i4.0
		IL_001a: ret
	} // end of method scrDecoration::GetVisible
```

### Overload 2

```il
	.method public hidebysig virtual 
		instance bool GetVisible () cil managed 
	{
		// Method begins at RVA 0x6796f
		// Header size: 1
		// Code size: 35 (0x23)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld bool scrDecoration::rendererEnabled
		IL_0006: brfalse.s IL_0021

		IL_0008: ldarg.0
		IL_0009: callvirt instance float32 scrDecoration::GetAlpha()
		IL_000e: ldc.r4 0.0
		IL_0013: bne.un.s IL_001f

		IL_0015: ldarg.0
		IL_0016: ldfld valuetype ObjectDecorationType scrObjectDecoration::objectType
		IL_001b: ldc.i4.1
		IL_001c: ceq
		IL_001e: ret

		IL_001f: ldc.i4.1
		IL_0020: ret

		IL_0021: ldc.i4.0
		IL_0022: ret
	} // end of method scrObjectDecoration::GetVisible
```

### Overload 3

```il
	.method public hidebysig virtual 
		instance bool GetVisible () cil managed 
	{
		// Method begins at RVA 0x6e285
		// Header size: 1
		// Code size: 53 (0x35)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld bool scrDecoration::rendererEnabled
		IL_0006: brfalse.s IL_0033

		IL_0008: ldarg.0
		IL_0009: callvirt instance float32 scrDecoration::GetAlpha()
		IL_000e: ldc.r4 0.0
		IL_0013: bne.un.s IL_0031

		IL_0015: ldarg.0
		IL_0016: call instance bool scrVisualDecoration::isMask()
		IL_001b: brtrue.s IL_0031

		IL_001d: ldarg.0
		IL_001e: call instance bool scrDecoration::get_useHitbox()
		IL_0023: brfalse.s IL_002f

		IL_0025: ldarg.0
		IL_0026: ldfld bool scrDecoration::hitOnce
		IL_002b: ldc.i4.0
		IL_002c: ceq
		IL_002e: ret

		IL_002f: ldc.i4.0
		IL_0030: ret

		IL_0031: ldc.i4.1
		IL_0032: ret

		IL_0033: ldc.i4.0
		IL_0034: ret
	} // end of method scrVisualDecoration::GetVisible
```
