# Method: ApplyColor (6 overloads)
**Type**: `scrVisualDecoration` | **Assembly**: `Assembly-CSharp`

### Overload 1

```il
	.method family hidebysig virtual 
		instance void ApplyColor () cil managed 
	{
		// Method begins at RVA 0x502c0
		// Header size: 1
		// Code size: 1 (0x1)
		.maxstack 8

		IL_0000: ret
	} // end of method scrPrefabDecoration::ApplyColor
```

### Overload 2

```il
	.method family hidebysig newslot abstract virtual 
		instance void ApplyColor () cil managed 
	{
	} // end of method scrDecoration::ApplyColor
```

### Overload 3

```il
	.method family hidebysig virtual 
		instance void ApplyColor () cil managed 
	{
		// Method begins at RVA 0x67b35
		// Header size: 1
		// Code size: 33 (0x21)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld valuetype ObjectDecorationType scrObjectDecoration::objectType
		IL_0006: brtrue.s IL_0020

		IL_0008: ldarg.0
		IL_0009: ldarg.0
		IL_000a: ldfld float32 scrDecoration::opacity
		IL_000f: call instance void scrObjectDecoration::SetFloorOpacity(float32)
		IL_0014: ldarg.0
		IL_0015: ldarg.0
		IL_0016: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Color scrObjectDecoration::floorGlowColor
		IL_001b: call instance void scrObjectDecoration::SetFloorGlowColor(valuetype [UnityEngine.CoreModule]UnityEngine.Color)

		IL_0020: ret
	} // end of method scrObjectDecoration::ApplyColor
```

### Overload 4

```il
	.method family hidebysig virtual 
		instance void ApplyColor () cil managed 
	{
		// Method begins at RVA 0x6925c
		// Header size: 12
		// Code size: 26 (0x1a)
		.maxstack 2
		.locals init (
			[0] valuetype [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem/MainModule
		)

		IL_0000: ldarg.0
		IL_0001: ldfld class [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem scrParticleDecoration::particleSystem
		IL_0006: callvirt instance valuetype [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem/MainModule [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem::get_main()
		IL_000b: stloc.0
		IL_000c: ldloca.s 0
		IL_000e: ldarg.0
		IL_000f: ldfld valuetype [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem/MinMaxGradient scrParticleDecoration::colorGradient
		IL_0014: call instance void [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem/MainModule::set_startColor(valuetype [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem/MinMaxGradient)
		IL_0019: ret
	} // end of method scrParticleDecoration::ApplyColor
```

### Overload 5

```il
	.method family hidebysig virtual 
		instance void ApplyColor () cil managed 
	{
		// Method begins at RVA 0x6c5d7
		// Header size: 1
		// Code size: 53 (0x35)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldarg.0
		IL_0002: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Color scrDecoration::color
		IL_0007: ldarg.0
		IL_0008: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Color scrDecoration::color
		IL_000d: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::a
		IL_0012: ldarg.0
		IL_0013: ldfld float32 scrDecoration::opacity
		IL_0018: mul
		IL_0019: call valuetype [UnityEngine.CoreModule]UnityEngine.Color ExtensionMethods::WithAlpha(valuetype [UnityEngine.CoreModule]UnityEngine.Color, float32)
		IL_001e: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Color scrDecoration::rendererColor
		IL_0023: ldarg.0
		IL_0024: ldfld class [UnityEngine.UI]UnityEngine.UI.Text scrTextDecoration::text
		IL_0029: ldarg.0
		IL_002a: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Color scrDecoration::rendererColor
		IL_002f: callvirt instance void [UnityEngine.UI]UnityEngine.UI.Graphic::set_color(valuetype [UnityEngine.CoreModule]UnityEngine.Color)
		IL_0034: ret
	} // end of method scrTextDecoration::ApplyColor
```

### Overload 6

```il
	.method family hidebysig virtual 
		instance void ApplyColor () cil managed 
	{
		// Method begins at RVA 0x6e188
		// Header size: 12
		// Code size: 82 (0x52)
		.maxstack 4
		.locals init (
			[0] float32
		)

		IL_0000: ldarg.0
		IL_0001: ldfld bool scrDecoration::stickToFloor
		IL_0006: brtrue.s IL_000f

		IL_0008: ldc.r4 1
		IL_000d: br.s IL_001a

		IL_000f: ldarg.0
		IL_0010: ldfld class scrFloor scrDecoration::parentFloor
		IL_0015: ldfld float32 scrFloor::opacity

		IL_001a: stloc.0
		IL_001b: ldarg.0
		IL_001c: ldarg.0
		IL_001d: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Color scrDecoration::color
		IL_0022: ldarg.0
		IL_0023: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Color scrDecoration::color
		IL_0028: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::a
		IL_002d: ldarg.0
		IL_002e: ldfld float32 scrDecoration::opacity
		IL_0033: mul
		IL_0034: ldloc.0
		IL_0035: mul
		IL_0036: call valuetype [UnityEngine.CoreModule]UnityEngine.Color ExtensionMethods::WithAlpha(valuetype [UnityEngine.CoreModule]UnityEngine.Color, float32)
		IL_003b: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Color scrDecoration::rendererColor
		IL_0040: ldarg.0
		IL_0041: ldfld class [UnityEngine.CoreModule]UnityEngine.SpriteRenderer scrVisualDecoration::spriteRenderer
		IL_0046: ldarg.0
		IL_0047: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Color scrDecoration::rendererColor
		IL_004c: callvirt instance void [UnityEngine.CoreModule]UnityEngine.SpriteRenderer::set_color(valuetype [UnityEngine.CoreModule]UnityEngine.Color)
		IL_0051: ret
	} // end of method scrVisualDecoration::ApplyColor
```
