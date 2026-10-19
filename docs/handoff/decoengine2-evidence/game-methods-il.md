# Method: SetPosition (2 overloads)
**Type**: `scrDecoration` | **Assembly**: `Assembly-CSharp`

### Overload 1

```il
	.method public hidebysig 
		instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 SetPosition (
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 position
		) cil managed 
	{
		// Method begins at RVA 0x2d594
		// Header size: 12
		// Code size: 38 (0x26)
		.maxstack 4
		.locals init (
			[0] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3
		)

		IL_0000: ldloca.s 0
		IL_0002: ldarg.1
		IL_0003: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
		IL_0008: ldarg.1
		IL_0009: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
		IL_000e: ldc.r4 -10
		IL_0013: call instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
		IL_0018: ldarg.0
		IL_0019: call instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_001e: ldloc.0
		IL_001f: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_position(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_0024: ldloc.0
		IL_0025: ret
	} // end of method scrCamera::SetPosition
```

### Overload 2

```il
	.method public hidebysig 
		instance void SetPosition (
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 pivotPos,
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 pivotOffset
		) cil managed 
	{
		// Method begins at RVA 0x5d010
		// Header size: 12
		// Code size: 147 (0x93)
		.maxstack 2
		.locals init (
			[0] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2
		)

		IL_0000: ldarg.0
		IL_0001: ldfld class scrParallax scrDecoration::parallax
		IL_0006: ldnull
		IL_0007: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_000c: brfalse.s IL_000f

		IL_000e: ret

		IL_000f: ldarg.2
		IL_0010: ldarg.0
		IL_0011: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::croppedPivotOffset
		IL_0016: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Addition(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_001b: ldarg.0
		IL_001c: call instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::get_trueScaleMultiplier()
		IL_0021: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Division(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0026: stloc.0
		IL_0027: ldarg.0
		IL_0028: ldarg.1
		IL_0029: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::pivotPosVec
		IL_002e: ldarg.0
		IL_002f: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrDecoration::childTransform
		IL_0034: ldloc.0
		IL_0035: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_003a: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_localPosition(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_003f: call class scnEditor ADOBase::get_editor()
		IL_0044: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Implicit(class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_0049: brfalse.s IL_007f

		IL_004b: ldarg.0
		IL_004c: ldfld class [UnityEngine.CoreModule]UnityEngine.GameObject scrDecoration::selectionBordersObject
		IL_0051: ldnull
		IL_0052: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_0057: brfalse.s IL_006f

		IL_0059: ldarg.0
		IL_005a: ldfld class [UnityEngine.CoreModule]UnityEngine.GameObject scrDecoration::selectionBordersObject
		IL_005f: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.GameObject::get_transform()
		IL_0064: ldloc.0
		IL_0065: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_006a: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_localPosition(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)

		IL_006f: call class scnEditor ADOBase::get_editor()
		IL_0074: ldfld class ADOFAI.DecorationPivot scnEditor::decPivot
		IL_0079: ldc.i4.1
		IL_007a: callvirt instance void ADOFAI.DecorationPivot::UpdatePivotCrossImage(bool)

		IL_007f: ldarg.0
		IL_0080: ldarg.2
		IL_0081: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::pivotOffsetVec
		IL_0086: ldarg.0
		IL_0087: call instance void scrDecoration::UpdateScreenClamp()
		IL_008c: ldarg.0
		IL_008d: call instance void scrDecoration::UpdatePosition()
		IL_0092: ret
	} // end of method scrDecoration::SetPosition
```

# Method: SetRotation (2 overloads)
**Type**: `scrDecoration` | **Assembly**: `Assembly-CSharp`

### Overload 1

```il
	.method public hidebysig 
		instance void SetRotation (
			float32 angle
		) cil managed 
	{
		// Method begins at RVA 0x5d14c
		// Header size: 12
		// Code size: 122 (0x7a)
		.maxstack 4
		.locals init (
			[0] float32,
			[1] valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion
		)

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: stfld float32 scrDecoration::rotAngle
		IL_0007: ldarg.0
		IL_0008: ldfld float32 scrDecoration::startRot
		IL_000d: ldarg.1
		IL_000e: add
		IL_000f: stloc.0
		IL_0010: ldarg.0
		IL_0011: ldfld bool scrDecoration::stickToFloor
		IL_0016: brfalse.s IL_0032

		IL_0018: ldloc.0
		IL_0019: ldarg.0
		IL_001a: ldfld class scrFloor scrDecoration::parentFloor
		IL_001f: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_0024: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_eulerAngles()
		IL_0029: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
		IL_002e: add
		IL_002f: stloc.0
		IL_0030: br.s IL_005e

		IL_0032: ldarg.0
		IL_0033: ldfld bool scrDecoration::lockRotation
		IL_0038: brfalse.s IL_005e

		IL_003a: ldloc.0
		IL_003b: call class scrController ADOBase::get_controller()
		IL_0040: ldfld class scrCamera scrController::camy
		IL_0045: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_004a: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion [UnityEngine.CoreModule]UnityEngine.Transform::get_rotation()
		IL_004f: stloc.1
		IL_0050: ldloca.s 1
		IL_0052: call instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Quaternion::get_eulerAngles()
		IL_0057: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
		IL_005c: add
		IL_005d: stloc.0

		IL_005e: ldarg.0
		IL_005f: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrDecoration::pivotTrans
		IL_0064: ldc.r4 0.0
		IL_0069: ldc.r4 0.0
		IL_006e: ldloc.0
		IL_006f: call valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion [UnityEngine.CoreModule]UnityEngine.Quaternion::Euler(float32, float32, float32)
		IL_0074: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_rotation(valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion)
		IL_0079: ret
	} // end of method scrDecoration::SetRotation
```

### Overload 2

```il
	.method public hidebysig 
		instance void SetRotation (
			float32 angle
		) cil managed 
	{
		// Method begins at RVA 0x63648
		// Header size: 1
		// Code size: 51 (0x33)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldarg.0
		IL_0002: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 scrFloor::startRot
		IL_0007: ldc.r4 0.0
		IL_000c: ldc.r4 0.0
		IL_0011: ldarg.1
		IL_0012: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
		IL_0017: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::op_Addition(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_001c: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 scrFloor::tweenRot
		IL_0021: ldarg.0
		IL_0022: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrFloor::thisTransform
		IL_0027: ldarg.0
		IL_0028: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 scrFloor::tweenRot
		IL_002d: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_eulerAngles(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_0032: ret
	} // end of method scrFloor::SetRotation
```

# Method: SetScale (5 overloads)
**Type**: `scrDecoration` | **Assembly**: `Assembly-CSharp`

### Overload 1

```il
	.method private hidebysig 
		instance void SetScale (
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scale
		) cil managed 
	{
		// Method begins at RVA 0x5ccbf
		// Header size: 1
		// Code size: 34 (0x22)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_0006: ldarg.1
		IL_0007: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
		IL_000c: ldarg.1
		IL_000d: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
		IL_0012: ldc.r4 1
		IL_0017: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
		IL_001c: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_localScale(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_0021: ret
	} // end of method scrCustomBackgroundSprite::SetScale
```

### Overload 2

```il
	.method public hidebysig newslot virtual 
		instance void SetScale (
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scale
		) cil managed 
	{
		// Method begins at RVA 0x5d1d4
		// Header size: 12
		// Code size: 84 (0x54)
		.maxstack 3
		.locals init (
			[0] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3
		)

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::scaleVec
		IL_0007: ldarg.0
		IL_0008: ldfld bool scrDecoration::stickToFloor
		IL_000d: brtrue.s IL_0016

		IL_000f: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::get_one()
		IL_0014: br.s IL_0026

		IL_0016: ldarg.0
		IL_0017: ldfld class scrFloor scrDecoration::parentFloor
		IL_001c: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_0021: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_localScale()

		IL_0026: stloc.0
		IL_0027: ldarg.0
		IL_0028: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrDecoration::pivotTrans
		IL_002d: ldarg.1
		IL_002e: ldarg.0
		IL_002f: call instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::get_camScaleMultiplier()
		IL_0034: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Multiply(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0039: ldloc.0
		IL_003a: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_003f: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Multiply(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0044: ldc.r4 1
		IL_0049: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 ExtensionMethods::WithZ(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32)
		IL_004e: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_localScale(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_0053: ret
	} // end of method scrDecoration::SetScale
```

### Overload 3

```il
	.method public hidebysig virtual 
		instance void SetScale (
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scale
		) cil managed 
	{
		// Method begins at RVA 0x67b58
		// Header size: 12
		// Code size: 158 (0x9e)
		.maxstack 3
		.locals init (
			[0] class FloorMesh,
			[1] class FloorMesh/MeshCache,
			[2] valuetype ObjectDecorationType
		)

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: call instance void scrDecoration::SetScale(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0007: ldarg.0
		IL_0008: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::get_one()
		IL_000d: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrObjectDecoration::bordersScaleMultiplier
		IL_0012: ldarg.0
		IL_0013: ldfld valuetype ObjectDecorationType scrObjectDecoration::objectType
		IL_0018: stloc.2
		IL_0019: ldloc.2
		IL_001a: brfalse.s IL_0041

		IL_001c: ldloc.2
		IL_001d: ldc.i4.1
		IL_001e: bne.un.s IL_009d

		IL_0020: ldarg.0
		IL_0021: ldfld class [UnityEngine.Physics2DModule]UnityEngine.BoxCollider2D scrDecoration::editorCollider
		IL_0026: ldarg.0
		IL_0027: ldfld class PlanetRenderer scrObjectDecoration::planetRenderer
		IL_002c: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
		IL_0031: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_localScale()
		IL_0036: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_003b: callvirt instance void [UnityEngine.Physics2DModule]UnityEngine.BoxCollider2D::set_size(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0040: ret

		IL_0041: ldarg.0
		IL_0042: ldfld class scrFloor scrObjectDecoration::floor
		IL_0047: ldfld class FloorRenderer scrFloor::floorRenderer
		IL_004c: castclass FloorMeshRenderer
		IL_0051: ldfld class FloorMesh FloorMeshRenderer::floorMesh
		IL_0056: stloc.0
		IL_0057: ldloc.0
		IL_0058: ldfld string FloorMesh::cacheKey
		IL_005d: brfalse.s IL_0084

		IL_005f: ldsfld class [mscorlib]System.Collections.Generic.Dictionary`2<string, class FloorMesh/MeshCache> FloorMesh::cache
		IL_0064: ldloc.0
		IL_0065: ldfld string FloorMesh::cacheKey
		IL_006a: ldloca.s 1
		IL_006c: callvirt instance bool class [mscorlib]System.Collections.Generic.Dictionary`2<string, class FloorMesh/MeshCache>::TryGetValue(!0, !1&)
		IL_0071: brfalse.s IL_0084

		IL_0073: ldarg.0
		IL_0074: ldfld class [UnityEngine.Physics2DModule]UnityEngine.PolygonCollider2D scrObjectDecoration::polygonCollider
		IL_0079: ldloc.1
		IL_007a: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2[] FloorMesh/MeshCache::polygon
		IL_007f: callvirt instance void [UnityEngine.Physics2DModule]UnityEngine.PolygonCollider2D::set_points(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2[])

		IL_0084: ldarg.0
		IL_0085: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrObjectDecoration::bordersScaleMultiplier
		IL_008a: ldflda float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
		IL_008f: dup
		IL_0090: ldind.r4
		IL_0091: call class scrController ADOBase::get_controller()
		IL_0096: callvirt instance float32 scrController::get_tileSize()
		IL_009b: mul
		IL_009c: stind.r4

		IL_009d: ret
	} // end of method scrObjectDecoration::SetScale
```

### Overload 4

```il
	.method public hidebysig virtual 
		instance void SetScale (
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scale
		) cil managed 
	{
		// Method begins at RVA 0x69154
		// Header size: 12
		// Code size: 147 (0x93)
		.maxstack 3
		.locals init (
			[0] valuetype [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem/ShapeModule
		)

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::scaleVec
		IL_0007: ldarg.0
		IL_0008: ldfld class [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem scrParticleDecoration::particleSystem
		IL_000d: callvirt instance valuetype [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem/ShapeModule [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem::get_shape()
		IL_0012: stloc.0
		IL_0013: ldloca.s 0
		IL_0015: ldarg.1
		IL_0016: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_001b: call instance void [UnityEngine.ParticleSystemModule]UnityEngine.ParticleSystem/ShapeModule::set_scale(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_0020: ldarg.0
		IL_0021: ldarg.1
		IL_0022: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrParticleDecoration::scale
		IL_0027: ldarg.0
		IL_0028: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrParticleDecoration::particleTransform
		IL_002d: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::get_one()
		IL_0032: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_0037: ldarg.0
		IL_0038: call instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::get_camScaleMultiplier()
		IL_003d: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Multiply(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0042: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0047: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_localScale(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_004c: call bool ADOBase::get_isLevelEditor()
		IL_0051: brfalse.s IL_0092

		IL_0053: ldarg.0
		IL_0054: ldfld class [UnityEngine.Physics2DModule]UnityEngine.BoxCollider2D scrDecoration::editorCollider
		IL_0059: ldarg.1
		IL_005a: callvirt instance void [UnityEngine.Physics2DModule]UnityEngine.BoxCollider2D::set_size(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_005f: ldarg.0
		IL_0060: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrParticleDecoration::gizmoTransform
		IL_0065: ldnull
		IL_0066: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_006b: brfalse.s IL_0092

		IL_006d: ldarg.0
		IL_006e: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform scrParticleDecoration::gizmoTransform
		IL_0073: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::get_one()
		IL_0078: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_007d: ldarg.0
		IL_007e: call instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 scrDecoration::get_camScaleMultiplier()
		IL_0083: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Division(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_0088: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
		IL_008d: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_localScale(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)

		IL_0092: ret
	} // end of method scrParticleDecoration::SetScale
```

### Overload 5

```il
	.method private hidebysig 
		instance void SetScale () cil managed 
	{
		// Method begins at RVA 0x10a59c
		// Header size: 12
		// Code size: 70 (0x46)
		.maxstack 3
		.locals init (
			[0] float32
		)

		IL_0000: call class scnEditor ADOBase::get_editor()
		IL_0005: ldfld class [UnityEngine.CoreModule]UnityEngine.Camera scnEditor::camera
		IL_000a: callvirt instance float32 [UnityEngine.CoreModule]UnityEngine.Camera::get_orthographicSize()
		IL_000f: ldc.r4 5
		IL_0014: div
		IL_0015: stloc.0
		IL_0016: ldarg.0
		IL_0017: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform ADOFAI.EditorGizmo::gizmoTransform
		IL_001c: ldnull
		IL_001d: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_0022: brfalse.s IL_0045

		IL_0024: ldarg.0
		IL_0025: ldfld class [UnityEngine.CoreModule]UnityEngine.Transform ADOFAI.EditorGizmo::gizmoTransform
		IL_002a: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::get_one()
		IL_002f: ldarg.0
		IL_0030: ldfld float32 ADOFAI.EditorGizmo::startScale
		IL_0035: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::op_Multiply(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, float32)
		IL_003a: ldloc.0
		IL_003b: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::op_Multiply(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, float32)
		IL_0040: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_localScale(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)

		IL_0045: ret
	} // end of method EditorGizmo::SetScale
```

# Method: LogicUpdate
**Type**: `scrDecoration` | **Assembly**: `Assembly-CSharp`

```il
	.method public hidebysig 
		instance void LogicUpdate (
			bool disableUpdateShader
		) cil managed 
	{
		// Method begins at RVA 0x5e7b3
		// Header size: 1
		// Code size: 48 (0x30)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: callvirt instance bool scrDecoration::GetVisible()
		IL_0006: brfalse.s IL_000e

		IL_0008: ldarg.0
		IL_0009: call instance void scrDecoration::UpdatePosition()

		IL_000e: call class scnGame ADOBase::get_customLevel()
		IL_0013: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Implicit(class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_0018: brfalse.s IL_0021

		IL_001a: ldarg.0
		IL_001b: ldarg.1
		IL_001c: callvirt instance void scrDecoration::UpdateShader(bool)

		IL_0021: ldarg.0
		IL_0022: call instance bool scrDecoration::get_useHitbox()
		IL_0027: brfalse.s IL_002f

		IL_0029: ldarg.0
		IL_002a: call instance void scrDecoration::UpdateHitboxState()

		IL_002f: ret
	} // end of method scrDecoration::LogicUpdate
```

# Method: GetAlpha (6 overloads)
**Type**: `scrVisualDecoration` | **Assembly**: `Assembly-CSharp`

### Overload 1

```il
	.method public hidebysig virtual 
		instance float32 GetAlpha () cil managed 
	{
		// Method begins at RVA 0x502c6
		// Header size: 1
		// Code size: 6 (0x6)
		.maxstack 8

		IL_0000: ldc.r4 1
		IL_0005: ret
	} // end of method scrPrefabDecoration::GetAlpha
```

### Overload 2

```il
	.method public hidebysig newslot abstract virtual 
		instance float32 GetAlpha () cil managed 
	{
	} // end of method scrDecoration::GetAlpha
```

### Overload 3

```il
	.method public hidebysig virtual 
		instance float32 GetAlpha () cil managed 
	{
		// Method begins at RVA 0x679f0
		// Header size: 12
		// Code size: 60 (0x3c)
		.maxstack 2
		.locals init (
			[0] float32,
			[1] valuetype ObjectDecorationType
		)

		IL_0000: ldarg.0
		IL_0001: ldfld valuetype ObjectDecorationType scrObjectDecoration::objectType
		IL_0006: stloc.1
		IL_0007: ldloc.1
		IL_0008: brfalse.s IL_0026

		IL_000a: ldloc.1
		IL_000b: ldc.i4.1
		IL_000c: bne.un.s IL_0034

		IL_000e: ldarg.0
		IL_000f: ldfld class PlanetRenderer scrObjectDecoration::planetRenderer
		IL_0014: ldfld class PlanetSprite PlanetRenderer::sprite
		IL_0019: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Color PlanetSprite::get_color()
		IL_001e: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::a
		IL_0023: stloc.0
		IL_0024: br.s IL_003a

		IL_0026: ldarg.0
		IL_0027: ldfld class scrFloor scrObjectDecoration::floor
		IL_002c: ldfld float32 scrFloor::opacity
		IL_0031: stloc.0
		IL_0032: br.s IL_003a

		IL_0034: ldc.r4 1
		IL_0039: stloc.0

		IL_003a: ldloc.0
		IL_003b: ret
	} // end of method scrObjectDecoration::GetAlpha
```

### Overload 4

```il
	.method public hidebysig virtual 
		instance float32 GetAlpha () cil managed 
	{
		// Method begins at RVA 0x691f3
		// Header size: 1
		// Code size: 6 (0x6)
		.maxstack 8

		IL_0000: ldc.r4 1
		IL_0005: ret
	} // end of method scrParticleDecoration::GetAlpha
```

### Overload 5

```il
	.method public hidebysig virtual 
		instance float32 GetAlpha () cil managed 
	{
		// Method begins at RVA 0x6c60d
		// Header size: 1
		// Code size: 17 (0x11)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld class [UnityEngine.UI]UnityEngine.UI.Text scrTextDecoration::text
		IL_0006: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Color [UnityEngine.UI]UnityEngine.UI.Graphic::get_color()
		IL_000b: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::a
		IL_0010: ret
	} // end of method scrTextDecoration::GetAlpha
```

### Overload 6

```il
	.method public hidebysig virtual 
		instance float32 GetAlpha () cil managed 
	{
		// Method begins at RVA 0x6e1e6
		// Header size: 1
		// Code size: 12 (0xc)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Color scrDecoration::rendererColor
		IL_0006: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::a
		IL_000b: ret
	} // end of method scrVisualDecoration::GetAlpha
```

# Method: SetOpacity (2 overloads)
**Type**: `scrDecoration` | **Assembly**: `Assembly-CSharp`

### Overload 1

```il
	.method public hidebysig 
		instance void SetOpacity (
			float32 opacity
		) cil managed 
	{
		// Method begins at RVA 0x5d29a
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldarg.1
		IL_0002: stfld float32 scrDecoration::opacity
		IL_0007: ldarg.0
		IL_0008: callvirt instance void scrDecoration::ApplyColor()
		IL_000d: ret
	} // end of method scrDecoration::SetOpacity
```

### Overload 2

```il
	.method public hidebysig 
		instance void SetOpacity (
			float32 opacity
		) cil managed 
	{
		// Method begins at RVA 0x6367c
		// Header size: 1
		// Code size: 23 (0x17)
		.maxstack 8

		IL_0000: call bool [UnityEngine.CoreModule]UnityEngine.Application::get_isPlaying()
		IL_0005: brtrue.s IL_0008

		IL_0007: ret

		IL_0008: ldarg.0
		IL_0009: ldarg.1
		IL_000a: ldc.r4 0.0
		IL_000f: ldc.i4.1
		IL_0010: call instance class [DOTween]DG.Tweening.Tween scrFloor::TweenOpacity(float32, float32, valuetype [DOTween]DG.Tweening.Ease)
		IL_0015: pop
		IL_0016: ret
	} // end of method scrFloor::SetOpacity
```
