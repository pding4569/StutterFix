# Method: UpdatePosition (4 overloads)
**Type**: `scrDecoration` | **Assembly**: `Assembly-CSharp`

### Overload 1

```il
	.method private hidebysig 
		instance void UpdatePosition () cil managed 
	{
		// Method begins at RVA 0x3b090
		// Header size: 12
		// Code size: 166 (0xa6)
		.maxstack 5
		.locals init (
			[0] float32,
			[1] float32,
			[2] float32,
			[3] float32
		)

		IL_0000: ldarg.0
		IL_0001: ldfld float32 dfxCircleMovement::time
		IL_0006: stloc.0
		IL_0007: ldarg.0
		IL_0008: ldfld bool dfxCircleMovement::reverse
		IL_000d: brfalse.s IL_0017

		IL_000f: ldarg.0
		IL_0010: ldfld float32 dfxCircleMovement::time
		IL_0015: neg
		IL_0016: stloc.0

		IL_0017: ldarg.0
		IL_0018: ldfld float32 dfxCircleMovement::period
		IL_001d: call class scrConductor ADOBase::get_conductor()
		IL_0022: ldfld class [UnityEngine.AudioModule]UnityEngine.AudioSource scrConductor::song
		IL_0027: callvirt instance float32 [UnityEngine.AudioModule]UnityEngine.AudioSource::get_pitch()
		IL_002c: div
		IL_002d: stloc.1
		IL_002e: ldarg.0
		IL_002f: ldfld float32 dfxCircleMovement::startx
		IL_0034: ldarg.0
		IL_0035: ldfld float32 dfxCircleMovement::amplitude
		IL_003a: ldloc.0
		IL_003b: ldc.r4 6.2831855
		IL_0040: mul
		IL_0041: ldloc.1
		IL_0042: div
		IL_0043: ldarg.0
		IL_0044: ldfld float32 dfxCircleMovement::startingOffsetPercent
		IL_0049: ldc.r4 3.1415927
		IL_004e: mul
		IL_004f: add
		IL_0050: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Cos(float32)
		IL_0055: mul
		IL_0056: add
		IL_0057: stloc.2
		IL_0058: ldarg.0
		IL_0059: ldfld float32 dfxCircleMovement::starty
		IL_005e: ldarg.0
		IL_005f: ldfld float32 dfxCircleMovement::amplitude
		IL_0064: ldloc.0
		IL_0065: ldc.r4 6.2831855
		IL_006a: mul
		IL_006b: ldloc.1
		IL_006c: div
		IL_006d: ldarg.0
		IL_006e: ldfld float32 dfxCircleMovement::startingOffsetPercent
		IL_0073: ldc.r4 3.1415927
		IL_0078: mul
		IL_0079: add
		IL_007a: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Sin(float32)
		IL_007f: mul
		IL_0080: add
		IL_0081: stloc.3
		IL_0082: ldarg.0
		IL_0083: ldfld bool dfxCircleMovement::useLocalPos
		IL_0088: brfalse.s IL_0098

		IL_008a: ldarg.0
		IL_008b: call instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_0090: ldloc.2
		IL_0091: ldloc.3
		IL_0092: call void RDExtensions::LocalMoveXY(class [UnityEngine.CoreModule]UnityEngine.Transform, float32, float32)
		IL_0097: ret

		IL_0098: ldarg.0
		IL_0099: call instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_009e: ldloc.2
		IL_009f: ldloc.3
		IL_00a0: call void RDExtensions::MoveXY(class [UnityEngine.CoreModule]UnityEngine.Transform, float32, float32)
		IL_00a5: ret
	} // end of method dfxCircleMovement::UpdatePosition
```

### Overload 2

```il
	.method private hidebysig 
		instance void UpdatePosition () cil managed 
	{
		// Method begins at RVA 0x3b444
		// Header size: 12
		// Code size: 99 (0x63)
		.maxstack 5
		.locals init (
			[0] float32,
			[1] float32
		)

		IL_0000: ldarg.0
		IL_0001: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrGfxFloat::start
		IL_0006: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
		IL_000b: stloc.0
		IL_000c: ldarg.0
		IL_000d: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrGfxFloat::start
		IL_0012: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
		IL_0017: ldarg.0
		IL_0018: ldfld float32 scrGfxFloat::amplitude
		IL_001d: ldarg.0
		IL_001e: ldfld float32 scrGfxFloat::c
		IL_0023: ldarg.0
		IL_0024: ldfld float32 scrGfxFloat::period
		IL_0029: div
		IL_002a: ldarg.0
		IL_002b: ldfld float32 scrGfxFloat::swingoffsetpct
		IL_0030: ldc.r4 3.1415927
		IL_0035: mul
		IL_0036: add
		IL_0037: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Sin(float32)
		IL_003c: mul
		IL_003d: add
		IL_003e: stloc.1
		IL_003f: ldarg.0
		IL_0040: ldfld bool scrGfxFloat::useLocalPos
		IL_0045: brfalse.s IL_0055

		IL_0047: ldarg.0
		IL_0048: call instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_004d: ldloc.0
		IL_004e: ldloc.1
		IL_004f: call void RDExtensions::LocalMoveXY(class [UnityEngine.CoreModule]UnityEngine.Transform, float32, float32)
		IL_0054: ret

		IL_0055: ldarg.0
		IL_0056: call instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_005b: ldloc.0
		IL_005c: ldloc.1
		IL_005d: call void RDExtensions::MoveXY(class [UnityEngine.CoreModule]UnityEngine.Transform, float32, float32)
		IL_0062: ret
	} // end of method scrGfxFloat::UpdatePosition
```

### Overload 3

```il
	.method public hidebysig 
		instance void UpdatePosition () cil managed 
	{
		// Method begins at RVA 0x5d438
		// Header size: 12
		// Code size: 242 (0xf2)
		.maxstack 3
		.locals init (
			[0] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
			[1] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2
		)

		IL_0000: call class scrController ADOBase::get_controller()
		IL_0005: callvirt instance bool scrController::get_paused()
		IL_000a: brtrue.s IL_001a

		IL_000c: ldarg.0
		IL_000d: ldfld class scrPlanet scrDecoration::followPlanet
		IL_0012: ldnull
		IL_0013: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_0018: brtrue.s IL_0021

		IL_001a: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::get_zero()
		IL_001f: br.s IL_0036

		IL_0021: ldarg.0
		IL_0022: ldfld class scrPlanet scrDecoration::followPlanet
		IL_0027: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_002c: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
		IL_0031: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)

		IL_0036: stloc.0
		IL_0037: ldarg.0
		IL_0038: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::pivotPosVec
		IL_003d: ldloc.0
		IL_003e: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Addition(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0043: stloc.1
		IL_0044: ldarg.0
		IL_0045: ldfld bool scrDecoration::stickToFloor
		IL_004a: brfalse.s IL_0073

		IL_004c: ldloc.1
		IL_004d: ldarg.0
		IL_004e: ldfld class scrFloor scrDecoration::parentFloor
		IL_0053: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_0058: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
		IL_005d: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_0062: ldarg.0
		IL_0063: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::startPos
		IL_0068: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Subtraction(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_006d: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Addition(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0072: stloc.1

		IL_0073: ldarg.0
		IL_0074: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrDecoration::pivotTrans
		IL_0079: ldloc.1
		IL_007a: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_007f: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_localPosition(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_0084: ldarg.0
		IL_0085: ldfld class scrParallax scrDecoration::parallax
		IL_008a: ldloc.1
		IL_008b: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0090: callvirt instance void scrParallax::SetNewStartPosition(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_0095: ldarg.0
		IL_0096: ldfld class scrParallax scrDecoration::parallax
		IL_009b: ldloc.1
		IL_009c: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_00a1: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 scrParallax::posCamAtStart
		IL_00a6: call class scnGame ADOBase::get_customLevel()
		IL_00ab: ldnull
		IL_00ac: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_00b1: brtrue.s IL_00c0

		IL_00b3: ldarg.0
		IL_00b4: ldfld class scrParallax scrDecoration::parallax
		IL_00b9: callvirt instance bool [UnityEngine.CoreModule]UnityEngine.Behaviour::get_enabled()
		IL_00be: brfalse.s IL_00cb

		IL_00c0: ldarg.0
		IL_00c1: ldfld class scrParallax scrDecoration::parallax
		IL_00c6: callvirt instance void scrParallax::SetTrans()

		IL_00cb: ldarg.0
		IL_00cc: ldarg.0
		IL_00cd: ldfld float32 scrDecoration::rotAngle
		IL_00d2: call instance void scrDecoration::SetRotation(float32)
		IL_00d7: ldarg.0
		IL_00d8: ldarg.0
		IL_00d9: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::scaleVec
		IL_00de: callvirt instance void scrDecoration::SetScale(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_00e3: ldarg.0
		IL_00e4: ldfld bool scrDecoration::stickToFloor
		IL_00e9: brfalse.s IL_00f1

		IL_00eb: ldarg.0
		IL_00ec: callvirt instance void scrDecoration::ApplyColor()

		IL_00f1: ret
	} // end of method scrDecoration::UpdatePosition
```

### Overload 4

```il
	.method private hidebysig 
		instance void UpdatePosition () cil managed 
	{
		// Method begins at RVA 0x63af4
		// Header size: 12
		// Code size: 215 (0xd7)
		.maxstack 3
		.locals init (
			[0] class [mscorlib]System.Collections.Generic.IEnumerator`1<class scrPlayer>,
			[1] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
			[2] float32,
			[3] float32,
			[4] bool,
			[5] float32
		)

		IL_0000: ldarg.0
		IL_0001: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrGem::startPosition
		IL_0006: ldnull
		IL_0007: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_000c: brtrue.s IL_001c

		IL_000e: ldarg.0
		IL_000f: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrGem::endPosition
		IL_0014: ldnull
		IL_0015: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_001a: brfalse.s IL_001d

		IL_001c: ret

		IL_001d: call class scrPlayerManager scrPlayerManager::get_instance()
		IL_0022: callvirt instance class [mscorlib]System.Collections.Generic.IEnumerator`1<class scrPlayer> scrPlayerManager::GetEnumerator()
		IL_0027: stloc.0
		.try
		{
			IL_0028: br IL_00bf
			// loop start (head: IL_00bf)
				IL_002d: ldloc.0
				IL_002e: callvirt instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<class scrPlayer>::get_Current()
				IL_0033: ldfld class PlanetarySystem scrPlayer::planetarySystem
				IL_0038: ldfld class scrPlanet PlanetarySystem::chosenPlanet
				IL_003d: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
				IL_0042: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
				IL_0047: stloc.1
				IL_0048: ldarg.0
				IL_0049: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrGem::startPosition
				IL_004e: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
				IL_0053: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
				IL_0058: ldloc.1
				IL_0059: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
				IL_005e: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Distance(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
				IL_0063: stloc.2
				IL_0064: ldarg.0
				IL_0065: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrGem::endPosition
				IL_006a: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
				IL_006f: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
				IL_0074: ldloc.1
				IL_0075: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
				IL_007a: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Distance(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
				IL_007f: stloc.3
				IL_0080: ldloc.3
				IL_0081: ldloc.2
				IL_0082: cgt
				IL_0084: stloc.s 4
				IL_0086: ldloc.s 4
				IL_0088: brtrue.s IL_008d

				IL_008a: ldloc.3
				IL_008b: br.s IL_008e

				IL_008d: ldloc.2

				IL_008e: stloc.s 5
				IL_0090: ldarg.0
				IL_0091: ldfld bool scrGem::moveToStart
				IL_0096: ldloc.s 4
				IL_0098: beq.s IL_00bf

				IL_009a: ldloc.s 5
				IL_009c: ldc.r4 3
				IL_00a1: bge.un.s IL_00bf

				IL_00a3: ldarg.0
				IL_00a4: ldloc.s 4
				IL_00a6: ldc.i4.0
				IL_00a7: ceq
				IL_00a9: stfld bool scrGem::moveToStart
				IL_00ae: ldarg.0
				IL_00af: ldc.i4.0
				IL_00b0: call instance void scrGem::LocalMove(bool)
				IL_00b5: ldarg.0
				IL_00b6: ldloc.s 4
				IL_00b8: stfld bool scrGem::moveToStart
				IL_00bd: leave.s IL_00d6

				IL_00bf: ldloc.0
				IL_00c0: callvirt instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
				IL_00c5: brtrue IL_002d
			// end loop

			IL_00ca: leave.s IL_00d6
		} // end .try
		finally
		{
			IL_00cc: ldloc.0
			IL_00cd: brfalse.s IL_00d5

			IL_00cf: ldloc.0
			IL_00d0: callvirt instance void [mscorlib]System.IDisposable::Dispose()

			IL_00d5: endfinally
		} // end handler

		IL_00d6: ret
	} // end of method scrGem::UpdatePosition
```
