# 재질·셰이더·텍스처·정렬 쓰기 전수 인덱스

정적 호출 지점이다. 초기화/편집기 전용/실행 중 경로를 섞어 빈도로 해석하지 않는다.

## Assembly-CSharp/ADOFAI/EventIndicator.cs

```text
55: circle.sortingOrder = 1 + num2 * 2;
56: icon.sortingOrder = 1 + num2 * 2 + 1;
```

## Assembly-CSharp/ADOFAI/RDColorPickerPopup.cs

```text
164: GetComponent<Canvas>().sortingOrder = ADOBase.editor.PushPopupBlocker(Hide);
```

## Assembly-CSharp/ADOFAI.Editor.Components.Gradients/GradientEditor.cs

```text
216: GetComponent<Canvas>().sortingOrder = scnEditor.instance.PushPopupBlocker(Hide);
```

## Assembly-CSharp/ADOStartup.cs

```text
254: scrDecorationManager.tileShader = Shader.Find("Sprites/Tile");
255: scrDecorationManager.visualDecoShader = Shader.Find("Hidden/BlendModes/VisualDeco/Grab");
```

## Assembly-CSharp/BBManager.cs

```text
105: array[i].material.SetTexture("_LavaTex", greenLava);
```

## Assembly-CSharp/BlendModes/BlendModeEffect.cs

```text
435: сomponentExtension.SetPropertyBlock(materialPropertyBlock);
451: block.SetTexture(ShaderUtilities.OverlayTexturePropertyId, (!OverlayTexture) ? Texture2D.whiteTexture : OverlayTexture);
457: material.SetTexture(ShaderUtilities.OverlayTexturePropertyId, OverlayTexture);
483: return new Material(shader)
```

## Assembly-CSharp/BlendModes/ComponentExtension.cs

```text
147: public virtual void SetPropertyBlock(MaterialPropertyBlock propertyBlock)
```

## Assembly-CSharp/BlendModes/RendererExtension.cs

```text
15: Shader shader = Shader.Find(DefaultShaderName);
22: defaultMaterial = new Material(shader);
```

## Assembly-CSharp/BlendModes/ShaderProperty.cs

```text
124: material.SetTexture(Name, TextureValue);
```

## Assembly-CSharp/BlendModes/ShaderUtilities.cs

```text
73: string[] shaderKeywords = material.shaderKeywords;
74: foreach (string text in shaderKeywords)
78: material.DisableKeyword(text);
81: material.EnableKeyword(blendMode.ToShaderKeyword());
100: toMaterial.SetTexture(propertyId, fromMaterial.GetTexture(propertyId));
143: material.SetTexture(name, (Texture)value);
```

## Assembly-CSharp/BlendModes/SharedMaterials.cs

```text
17: if ((bool)material && material.shader == shader && material.IsKeywordEnabled(keyword))
48: Material material = new Material(shader);
```

## Assembly-CSharp/CameraFilterPack_3D_Anomaly.cs

```text
45: SCMaterial = new Material(SCShader);
54: SCShader = Shader.Find("CameraFilterPack/3D_Anomaly");
```

## Assembly-CSharp/CameraFilterPack_3D_Binary.cs

```text
45: SCMaterial = new Material(SCShader);
55: SCShader = Shader.Find("CameraFilterPack/3D_Binary");
76: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_3D_BlackHole.cs

```text
43: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPack/3D_BlackHole");
```

## Assembly-CSharp/CameraFilterPack_3D_Computer.cs

```text
42: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPack/3D_Computer");
72: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_3D_Distortion.cs

```text
46: SCMaterial = new Material(SCShader);
55: SCShader = Shader.Find("CameraFilterPack/3D_Distortion");
```

## Assembly-CSharp/CameraFilterPack_3D_Fog_Smoke.cs

```text
48: SCMaterial = new Material(SCShader);
58: SCShader = Shader.Find("CameraFilterPack/3D_Myst");
94: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_3D_Ghost.cs

```text
51: SCMaterial = new Material(SCShader);
60: SCShader = Shader.Find("CameraFilterPack/3D_Ghost");
```

## Assembly-CSharp/CameraFilterPack_3D_Inverse.cs

```text
40: SCMaterial = new Material(SCShader);
49: SCShader = Shader.Find("CameraFilterPack/3D_Inverse");
```

## Assembly-CSharp/CameraFilterPack_3D_Matrix.cs

```text
42: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPack/3D_Matrix");
72: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_3D_Mirror.cs

```text
43: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPack/3D_Mirror");
```

## Assembly-CSharp/CameraFilterPack_3D_Myst.cs

```text
48: SCMaterial = new Material(SCShader);
58: SCShader = Shader.Find("CameraFilterPack/3D_Myst");
94: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_3D_Scan_Scene.cs

```text
44: SCMaterial = new Material(SCShader);
53: SCShader = Shader.Find("CameraFilterPack/3D_Scan_Scene");
```

## Assembly-CSharp/CameraFilterPack_3D_Shield.cs

```text
55: SCMaterial = new Material(SCShader);
64: SCShader = Shader.Find("CameraFilterPack/3D_Shield");
```

## Assembly-CSharp/CameraFilterPack_3D_Snow.cs

```text
47: SCMaterial = new Material(SCShader);
57: SCShader = Shader.Find("CameraFilterPack/3D_Snow");
80: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_AAA_Blood.cs

```text
36: SCMaterial = new Material(SCShader);
46: SCShader = Shader.Find("CameraFilterPack/AAA_Blood");
64: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_AAA_Blood_Hit.cs

```text
60: SCMaterial = new Material(SCShader);
70: SCShader = Shader.Find("CameraFilterPack/AAA_Blood_Hit");
96: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_AAA_Blood_Plus.cs

```text
60: SCMaterial = new Material(SCShader);
70: SCShader = Shader.Find("CameraFilterPack/AAA_Blood_Plus");
96: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_AAA_BloodOnScreen.cs

```text
38: SCMaterial = new Material(SCShader);
48: SCShader = Shader.Find("CameraFilterPack/AAA_BloodOnScreen");
67: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_AAA_WaterDropPro.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/AAA_WaterDropPro");
60: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Atmosphere_Fog.cs

```text
34: SCMaterial = new Material(SCShader);
44: SCShader = Shader.Find("CameraFilterPack/Atmosphere_Fog");
63: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Atmosphere_Rain_Pro_3D.cs

```text
69: SCMaterial = new Material(SCShader);
79: SCShader = Shader.Find("CameraFilterPack/Atmosphere_Rain_Pro_3D");
124: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Blend2Camera_ColorKey.cs

```text
50: SCMaterial = new Material(SCShader);
64: SCShader = Shader.Find(ShaderName);
78: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp/CameraFilterPack_Blend2Camera_SplitScreen3D.cs

```text
56: SCMaterial = new Material(SCShader);
76: SCShader = Shader.Find(ShaderName);
90: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp/CameraFilterPack_Broken_Screen.cs

```text
27: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/Broken_Screen");
52: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Broken_Simple.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_Broken_Simple");
```

## Assembly-CSharp/CameraFilterPack_Broken_Spliter.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_Broken_Spliter");
```

## Assembly-CSharp/CameraFilterPack_Classic_ThermalVision.cs

```text
40: SCMaterial = new Material(SCShader);
49: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_Classic_ThermalVision");
```

## Assembly-CSharp/CameraFilterPack_Color_Adjust_Levels.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/Color_Levels");
```

## Assembly-CSharp/CameraFilterPack_Color_Chromatic_Plus.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/Color_Chromatic_Plus");
```

## Assembly-CSharp/CameraFilterPack_Convert_Normal.cs

```text
23: SCMaterial = new Material(SCShader);
32: SCShader = Shader.Find("CameraFilterPack/Color_Convert_Normal");
```

## Assembly-CSharp/CameraFilterPack_Distortion_ShockWaveManual.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Distortion_ShockWaveManual");
```

## Assembly-CSharp/CameraFilterPack_EXTRA_SHOWFPS.cs

```text
38: SCMaterial = new Material(SCShader);
49: SCShader = Shader.Find("CameraFilterPack/EXTRA_SHOWFPS");
```

## Assembly-CSharp/CameraFilterPack_Glasses_On.cs

```text
48: SCMaterial = new Material(SCShader);
58: SCShader = Shader.Find("CameraFilterPack/Glasses_On");
81: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Glasses_On_2.cs

```text
48: SCMaterial = new Material(SCShader);
58: SCShader = Shader.Find("CameraFilterPack/Glasses_OnX");
81: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Glasses_On_3.cs

```text
48: SCMaterial = new Material(SCShader);
58: SCShader = Shader.Find("CameraFilterPack/Glasses_On");
81: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Glasses_On_4.cs

```text
48: SCMaterial = new Material(SCShader);
58: SCShader = Shader.Find("CameraFilterPack/Glasses_On");
81: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Glasses_On_5.cs

```text
48: SCMaterial = new Material(SCShader);
58: SCShader = Shader.Find("CameraFilterPack/Glasses_On");
81: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Glasses_On_6.cs

```text
48: SCMaterial = new Material(SCShader);
58: SCShader = Shader.Find("CameraFilterPack/Glasses_On");
81: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Lut_2_Lut.cs

```text
39: SCMaterial = new Material(SCShader);
48: SCShader = Shader.Find("CameraFilterPack/Lut_2_lut");
172: material.SetTexture("_LutTex", converted3DLut);
173: material.SetTexture("_LutTex2", converted3DLut2);
```

## Assembly-CSharp/CameraFilterPack_Lut_2_Lut_Extra.cs

```text
45: SCMaterial = new Material(SCShader);
54: SCShader = Shader.Find("CameraFilterPack/Lut_2_lut_Extra");
180: material.SetTexture("_LutTex", converted3DLut);
181: material.SetTexture("_LutTex2", converted3DLut2);
```

## Assembly-CSharp/CameraFilterPack_Lut_Mask.cs

```text
35: SCMaterial = new Material(SCShader);
44: SCShader = Shader.Find("CameraFilterPack/Lut_Mask");
137: material.SetTexture("_LutTex", converted3DLut);
138: material.SetTexture("_MaskTex", Mask);
```

## Assembly-CSharp/CameraFilterPack_Lut_PlayWith.cs

```text
37: SCMaterial = new Material(SCShader);
46: SCShader = Shader.Find("CameraFilterPack/Lut_PlayWith");
138: material.SetTexture("_LutTex", converted3DLut);
```

## Assembly-CSharp/CameraFilterPack_Lut_Plus.cs

```text
30: SCMaterial = new Material(SCShader);
39: SCShader = Shader.Find("CameraFilterPack/Lut_Plus");
132: material.SetTexture("_LutTex", converted3DLut);
```

## Assembly-CSharp/CameraFilterPack_Lut_Simple.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/Lut_Simple");
126: material.SetTexture("_LutTex", converted3DLut);
```

## Assembly-CSharp/CameraFilterPack_Lut_TestMode.cs

```text
40: SCMaterial = new Material(SCShader);
49: SCShader = Shader.Find("CameraFilterPack/Lut_TestMode");
141: material.SetTexture("_LutTex", converted3DLut);
```

## Assembly-CSharp/CameraFilterPack_NewGlitch1.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_NewGlitch1");
```

## Assembly-CSharp/CameraFilterPack_NewGlitch2.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_NewGlitch2");
```

## Assembly-CSharp/CameraFilterPack_NewGlitch3.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_NewGlitch3");
```

## Assembly-CSharp/CameraFilterPack_NewGlitch4.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_NewGlitch4");
```

## Assembly-CSharp/CameraFilterPack_NewGlitch5.cs

```text
40: SCMaterial = new Material(SCShader);
49: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_NewGlitch5");
```

## Assembly-CSharp/CameraFilterPack_NewGlitch6.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_NewGlitch6");
```

## Assembly-CSharp/CameraFilterPack_NewGlitch7.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_NewGlitch7");
```

## Assembly-CSharp/CameraFilterPack_Pixelisation_DeepOilPaintHQ.cs

```text
40: SCMaterial = new Material(SCShader);
49: SCShader = Shader.Find("CameraFilterPack/Deep_OilPaintHQ");
```

## Assembly-CSharp/CameraFilterPack_Pixelisation_Sweater.cs

```text
30: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Pixelisation_Sweater");
56: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp/CameraFilterPack_Rain_RainFX.cs

```text
36: SCMaterial = new Material(SCShader);
47: SCShader = Shader.Find("CameraFilterPack/RainFX");
84: material.SetTexture("Texture2", Texture2);
85: material.SetTexture("Texture3", Texture3);
```

## Assembly-CSharp/CameraFilterPack_TV_MovieNoise.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_MovieNoise");
```

## Assembly-CSharp/CameraFilterPack_TV_Vignetting.cs

```text
30: SCMaterial = new Material(SCShader);
39: SCShader = Shader.Find("CameraFilterPack/TV_Vignetting");
47: material.SetTexture("Vignette", Vignette);
```

## Assembly-CSharp/CameraFilterPack_Vision_Blood.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Vision_Blood");
```

## Assembly-CSharp/CameraFilterPack_Vision_Blood_Fast.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Vision_Blood_Fast");
```

## Assembly-CSharp/CameraFilterPack_Vision_Hell_Blood.cs

```text
42: SCMaterial = new Material(SCShader);
51: SCShader = Shader.Find("CameraFilterPack/Vision_Hell_Blood");
```

## Assembly-CSharp/CameraFilterPack_Vision_SniperScore.cs

```text
54: SCMaterial = new Material(SCShader);
63: SCShader = Shader.Find("CameraFilterPack/Vision_SniperScore");
```

## Assembly-CSharp/ColorCloud.cs

```text
18: particleSystem.GetComponent<ParticleSystemRenderer>().sortingOrder = order;
```

## Assembly-CSharp/EndingSparkle.cs

```text
148: endCircle.material = new Material(Shader.Find("Sprites/Default"));
150: endCircle.sortingOrder = 150;
```

## Assembly-CSharp/ffxFlashPlus.cs

```text
90: flashRenderer.sortingLayerName = (FG ? "FgFlash" : "Default");
```

## Assembly-CSharp/FloorMeshOld.cs

```text
52: renderers[i].sortingOrder = 1000 - index * 2 + 1;
```

## Assembly-CSharp/FloorMeshRenderer.cs

```text
42: renderer.sortingLayerName = "Floor";
```

## Assembly-CSharp/FloorRenderer.cs

```text
30: renderer.sortingOrder = value;
```

## Assembly-CSharp/LevelRadar.cs

```text
103: SetTexture(iconAnimationTextures[num2]);
150: private void SetTexture(Texture2D texture)
```

## Assembly-CSharp/PauseMenu.cs

```text
271: vignetteMaterial = new Material(vignetteMaterial);
```

## Assembly-CSharp/PlanetRenderer.cs

```text
348: int sortingLayerID = SortingLayer.NameToID(layer);
349: tailParticles.GetComponent<ParticleSystemRenderer>().sortingLayerID = sortingLayerID;
350: coreParticles.GetComponent<ParticleSystemRenderer>().sortingLayerID = sortingLayerID;
351: sparks.GetComponent<ParticleSystemRenderer>().sortingLayerID = sortingLayerID;
352: ring.sortingLayerID = sortingLayerID;
353: sprite.meshRenderer.sortingLayerID = sortingLayerID;
354: glow.sortingLayerID = sortingLayerID;
355: samuraiSprite.sortingLayerID = sortingLayerID;
356: faceDetails.sortingLayerID = sortingLayerID;
357: faceSprite.sortingLayerID = sortingLayerID;
```

## Assembly-CSharp/PlanetSprite.cs

```text
28: meshRenderer.material.SetTexture("_PlanetTex", value);
64: meshRenderer.material.SetTexture("_AlphaMask", value);
```

## Assembly-CSharp/PolarScreen.cs

```text
15: SCMaterial = new Material(SCShader);
24: SCShader = Shader.Find("ADOFAI/PolarScreen");
```

## Assembly-CSharp/PortalQuad.cs

```text
18: material.renderQueue = 2001;
22: public void SetTexture(Texture2D texture)
29: material.mainTexture = texture;
37: material.mainTexture = null;
```

## Assembly-CSharp/PostEffectsBase.cs

```text
21: if (s.isSupported && (bool)m2Create && m2Create.shader == s)
31: m2Create = new Material(s);
47: if ((bool)m2Create && m2Create.shader == s && s.isSupported)
55: m2Create = new Material(s);
```

## Assembly-CSharp/PracticeTimeline.cs

```text
98: waveMaterial = new Material(waveMaterial);
```

## Assembly-CSharp/RDUtils.cs

```text
1293: material.EnableKeyword("USE_POINT_SAMPLING");
1297: material.DisableKeyword("USE_POINT_SAMPLING");
```

## Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUITextMeshProGlyphHelper.cs

```text
594: __tmProShader = Shader.Find("TextMeshPro/Sprite");
617: Material obj = ((baseMaterial != null) ? new Material(baseMaterial) : new Material(tmProShader));
2715: asset.material.SetTexture(ShaderUtilities.ID_MainTex, sprite.texture);
2750: asset.material.SetTexture(ShaderUtilities.ID_MainTex, _stubTexture);
2991: destination.shader = source.shader;
2992: if (source.shaderKeywords != null)
2994: string[] array = new string[source.shaderKeywords.Length];
2995: Array.Copy(source.shaderKeywords, array, source.shaderKeywords.Length);
2996: destination.shaderKeywords = array;
3000: destination.shaderKeywords = null;
3006: destination.renderQueue = source.renderQueue;
```

## Assembly-CSharp/RotateCross.cs

```text
13: meshRenderer.material = new Material(meshRenderer.material);
```

## Assembly-CSharp/scnCLS.cs

```text
1252: portalQuad.SetTexture(texture);
1567: portalQuad.SetTexture(texture);
1575: portalQuad.SetTexture(featuredLevelPortalTexture);
1595: portalQuad.SetTexture(emptyTexture);
```

## Assembly-CSharp/scnEditor.cs

```text
1661: spritesDefaultShader = Shader.Find("Sprites/Default");
1664: lineMaterial = new Material(Shader.Find("ADOFAI/ScrollingSprite"));
1665: lineMaterial.SetTexture("_MainTex", floorConnectorTex);
7705: popupBlocker.GetComponent<Canvas>().sortingOrder = _currentPopupSortOrder;
7732: popupBlocker.GetComponent<Canvas>().sortingOrder = _currentPopupSortOrder - 2;
```

## Assembly-CSharp/scrBlur.cs

```text
35: blurMaterial = new Material(ADOBase.gc.tileBlurShader);
48: blurMaterial.SetTexture("_BaseTex", texture);
49: blurMaterial.SetTexture("_TileTex", tileTexture);
```

## Assembly-CSharp/scrCamera.cs

```text
316: camQuadMesh.material.mainTexture = camRT;
594: camQuadMesh.material.mainTexture = mainTexture;
603: camQuadMesh.material.mainTexture = camRT;
```

## Assembly-CSharp/scrCLSPortalChanger.cs

```text
134: newQuad.SetTexture(texture);
```

## Assembly-CSharp/scrController.cs

```text
841: lineMaterial = new Material(Shader.Find("ADOFAI/ScrollingSprite"));
842: lineMaterial.SetTexture("_MainTex", ADOBase.gc.planetPolygonTex);
```

## Assembly-CSharp/ScreenScroll.cs

```text
21: SCMaterial = new Material(SCShader);
30: SCShader = Shader.Find("ADOFAI/ScreenScroll");
```

## Assembly-CSharp/ScreenTile.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("ADOFAI/ScreenTile");
```

## Assembly-CSharp/scrFloor.cs

```text
639: floorRenderer.SetMaterial(new Material(instance.customWorldMaterial));
1068: floorRenderer.material.SetTexture("_IconTex", sprite.texture);
1074: floorRenderer.material.SetTexture("_IconTex", gc.tex_clear);
1109: floorMeshRenderer.material.SetTexture("_IconOutlineTex", sprite.texture);
1115: floorMeshRenderer.material.SetTexture("_IconOutlineTex", gc.tex_clear);
1218: (floorRenderer as FloorMeshRenderer).renderer.sortingLayerName = "FloorTop";
1529: int sortingOrder = floorRenderer.renderer.sortingOrder;
1530: component.glowSprite.sortingOrder = sortingOrder + 1;
1531: component.GetComponentInChildren<ParticleSystemRenderer>().sortingOrder = sortingOrder + 2;
1532: component.cap.sortingOrder = sortingOrder + 3;
1533: component.icon.sortingOrder = sortingOrder + 4;
1657: material.SetTexture("_MainTex", value5);
1658: material.SetTexture("_TileTex", texture2D);
1661: material.SetTexture("_PerlinTex", value2);
1756: int sortingOrder = floorRenderer.renderer.sortingOrder - 5 * layers;
1762: floorRenderer.sortingOrder = order;
1765: outlineSprite.sortingOrder = order + 1;
1775: topGlow.sortingOrder = floorRenderer.sortingOrder + 10;
1778: topGlow.GetComponent<Renderer>().sortingLayerName = floorRenderer.renderer.sortingLayerName;
1784: bottomGlow.sortingOrder = floorRenderer.sortingOrder - 10 - 2;
1792: iconsprite.sortingOrder = floorRenderer.sortingOrder + 2;
1937: floorRenderer.material.SetTexture("_MainTex", value);
```

## Assembly-CSharp/scrHitTextMesh.cs

```text
84: meshRenderer.sortingOrder = 100;
```

## Assembly-CSharp/scrHoldRenderer.cs

```text
71: m_meshRenderer.sortingLayerName = "FloorCover";
72: m_meshRenderer.sortingOrder = startFloor.floorRenderer.renderer.sortingOrder + 200;
76: m_meshRenderer.material.SetTexture("_MainTex", tex);
77: m_meshRenderer.material.renderQueue = 2900;
360: endCircle.material = new Material(Shader.Find("Sprites/Default"));
362: endCircle.sortingLayerName = "Floor";
363: endCircle.sortingOrder = startFloor.floorRenderer.renderer.sortingOrder + 5;
```

## Assembly-CSharp/scrLevelMaker.cs

```text
794: holdMaterial = new Material(RDConstants.data.holdShader);
795: holdMaterial.SetTexture("_MainTex", lm2.holdTex);
796: holdMaterial.renderQueue = 2900;
817: component.material = new Material(holdMaterial);
889: componentsInChildren[j].sortingOrder = -1;
907: lineRenderer.sortingLayerName = "Floor";
908: lineRenderer.sortingOrder = listFloor.floorRenderer.renderer.sortingOrder + 6;
```

## Assembly-CSharp/scrMisc.cs

```text
40: lineRenderer.material.shader = Shader.Find("ADOFAI/Overlay");
```

## Assembly-CSharp/scrObjectDecoration.cs

```text
141: string sortingLayerName = ((depth >= 0) ? "Bg" : "Default");
143: int sortingOrder = -depth;
146: sortingLayerName = parentFloor.floorRenderer.renderer.sortingLayerName;
148: sortingOrder = parentFloor.floorRenderer.sortingOrder;
153: ((MeshRenderer)floor.floorRenderer.renderer).sortingLayerName = sortingLayerName;
163: obj.sortingLayerName = sortingLayerName;
165: obj.sortingOrder = sortingOrder;
```

## Assembly-CSharp/scrOptionsDot.cs

```text
23: renderer.sortingLayerName = "Bg";
24: renderer.sortingOrder = -1;
```

## Assembly-CSharp/scrParticleDecoration.cs

```text
78: string sortingLayerName = ((depth >= 0) ? "Bg" : "Default");
80: particleRenderer.sortingLayerName = sortingLayerName;
82: int sortingOrder = -depth;
83: particleRenderer.sortingOrder = sortingOrder;
95: particleRenderer.material.mainTexture = sprite?.texture;
```

## Assembly-CSharp/scrTextDecoration.cs

```text
81: string sortingLayerName = ((depth >= 0) ? "Bg" : "Default");
83: canvas.sortingLayerName = sortingLayerName;
85: int sortingOrder = -depth;
86: canvas.sortingOrder = sortingOrder;
```

## Assembly-CSharp/scrVisualDecoration.cs

```text
153: material.SetTexture(SpriteAlphaMask.ShaderProperty_RGB, tempRT2);
154: material.SetTexture(SpriteAlphaMask.ShaderProperty_A, texture);
187: meshRenderer.material.mainTexture = (hasShader ? ((Texture)rt) : ((Texture)texture));
331: spriteRenderer.sortingLayerName = parentFloor.floorRenderer.renderer.sortingLayerName;
333: spriteRenderer.sortingOrder = parentFloor.floorRenderer.sortingOrder;
336: string sortingLayerName = ((depth >= 0) ? "Bg" : "Default");
338: spriteRenderer.sortingLayerName = sortingLayerName;
340: meshRenderer.sortingLayerName = sortingLayerName;
342: int sortingOrder = -depth;
343: spriteRenderer.sortingOrder = sortingOrder;
344: meshRenderer.sortingOrder = sortingOrder;
464: spriteRenderer.material.shader = scrDecorationManager.tileShader;
467: meshRendererMat = new Material(scrDecorationManager.visualDecoShader)
```

## Assembly-CSharp/SetSortingLayer.cs

```text
12: component.sortingLayerName = sortingLayer;
13: component.sortingOrder = orderInLayer;
```

## Assembly-CSharp/SpriteAlphaMaskUtils.cs

```text
97: mergeSprite.SetTexture(SpriteAlphaMask.ShaderProperty_Texture1, flag ? null : renderTexture);
99: mergeSprite.SetTexture(SpriteAlphaMask.ShaderProperty_Texture2, spriteAlphaMask.sprite.texture);
115: mergeSprite.SetTexture(SpriteAlphaMask.ShaderProperty_Texture3, spriteAlphaMask2.sprite.texture);
137: mergeSprite.SetTexture(SpriteAlphaMask.ShaderProperty_Texture4, spriteAlphaMask3.sprite.texture);
159: mergeSprite.SetTexture(SpriteAlphaMask.ShaderProperty_Texture5, spriteAlphaMask4.sprite.texture);
179: spriteAlphaMask5.SetTexture(SpriteAlphaMask.ShaderProperty_RGB, pre);
180: spriteAlphaMask5.SetTexture(SpriteAlphaMask.ShaderProperty_A, renderTexture);
```

## Assembly-CSharp/SpriteInstantiater.cs

```text
15: public string sortingLayerName = "Default";
53: spriteRenderer.sortingLayerName = sortingLayerName;
```

## Assembly-CSharp/SuperSpriteTrail.cs

```text
79: public string sortingLayerName = "";
195: spriteRenderer.sortingOrder = this.spriteRenderer.sortingOrder;
209: spriteRenderer.sharedMaterial = trailMaterial;
210: if (sortingLayerName == "")
212: spriteRenderer.sortingLayerName = this.spriteRenderer.sortingLayerName;
216: spriteRenderer.sortingLayerName = sortingLayerName;
327: component.sortingOrder = component2.sortingOrder + 1;
331: component.sortingOrder = component2.sortingOrder - 1;
353: component3.sortingOrder = spriteRenderer.sortingOrder;
```

## Assembly-CSharp/TakeScreenshot.cs

```text
31: mat = new Material(RDConstants.data.blurEffectConeTap);
46: mat.mainTexture = temporary;
```

## Assembly-CSharp/TileFlash.cs

```text
14: GetComponent<SpriteRenderer>().sortingOrder = base.transform.parent.GetComponent<SpriteRenderer>().sortingOrder;
```

## Assembly-CSharp/TMPro.Examples/Benchmark01.cs

```text
51: m_textMesh.GetComponent<Renderer>().sharedMaterial = m_textMesh.font.material;
56: m_textMesh.GetComponent<Renderer>().sharedMaterial = m_textMesh.font.material;
```

## Assembly-CSharp/TMPro.Examples/Benchmark02.cs

```text
42: textMesh.GetComponent<Renderer>().sharedMaterial = textMesh.font.material;
```

## Assembly-CSharp/TMPro.Examples/Benchmark03.cs

```text
37: tMP_FontAsset.material.shader = Shader.Find("TextMeshPro/Mobile/Distance Field SSD");
41: tMP_FontAsset.material.shader = Shader.Find("TextMeshPro/Distance Field");
75: textMesh.GetComponent<Renderer>().sharedMaterial = SourceFont.material;
```

## Assembly-CSharp/TMPro.Examples/TextMeshProFloatingText.cs

```text
85: m_textMesh.GetComponent<Renderer>().sharedMaterial = m_textMesh.font.material;
```

## Assembly-CSharp/TMPro.Examples/TextMeshSpawner.cs

```text
39: textMesh.GetComponent<Renderer>().sharedMaterial = TheFont.material;
```

## Assembly-CSharp/TNOFilter.cs

```text
23: SCMaterial = new Material(SCShader);
32: SCShader = Shader.Find("ADOFAI/TNOFilter");
40: material.SetTexture("_Palette", palette);
41: material.SetTexture("_Palette2", palette2);
```

## Assembly-CSharp/VideoBloom.cs

```text
138: videoBloomMaterial.SetTexture(MainTex, renderTexture);
148: videoBloomMaterial.SetTexture(MainTex, renderTexture);
161: videoBloomMaterial.SetTexture(MainTex, renderTexture);
171: videoBloomMaterial.SetTexture(MainTex, renderTexture);
226: videoBloomMaterial.SetTexture(MainTex, source);
227: videoBloomMaterial.SetTexture(MediumBloom, temporary2);
228: videoBloomMaterial.SetTexture(LargeBloom, renderTexture);
```

## Assembly-CSharp/WorkshopThumbnailMaker.cs

```text
30: portalQuad.SetTexture(portalImage);
```

## Assembly-CSharp-firstpass/CameraFilterPack_AAA_SuperComputer.cs

```text
39: SCMaterial = new Material(SCShader);
48: SCShader = Shader.Find("CameraFilterPack/AAA_Super_Computer");
```

## Assembly-CSharp-firstpass/CameraFilterPack_AAA_SuperHexagon.cs

```text
37: SCMaterial = new Material(SCShader);
46: SCShader = Shader.Find("CameraFilterPack/AAA_Super_Hexagon");
```

## Assembly-CSharp-firstpass/CameraFilterPack_AAA_WaterDrop.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/AAA_WaterDrop");
60: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Alien_Vision.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Alien_Vision");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Antialiasing_FXAA.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/Antialiasing_FXAA");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Atmosphere_Rain.cs

```text
42: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPack/Atmosphere_Rain");
73: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Atmosphere_Rain_Pro.cs

```text
45: SCMaterial = new Material(SCShader);
55: SCShader = Shader.Find("CameraFilterPack/Atmosphere_Rain_Pro");
77: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Atmosphere_Snow_8bits.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Atmosphere_Snow_8bits");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Blend.cs

```text
28: SCMaterial = new Material(SCShader);
42: SCShader = Shader.Find(ShaderName);
54: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_BlueScreen.cs

```text
48: SCMaterial = new Material(SCShader);
68: SCShader = Shader.Find(ShaderName);
82: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Color.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_ColorBurn.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_ColorDodge.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Darken.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_DarkerColor.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Difference.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Divide.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Exclusion.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_GreenScreen.cs

```text
48: SCMaterial = new Material(SCShader);
62: SCShader = Shader.Find(ShaderName);
76: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_HardLight.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_HardMix.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Hue.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Lighten.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LighterColor.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LinearBurn.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LinearDodge.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LinearLight.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Luminosity.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Multiply.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Overlay.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_PhotoshopFilters.cs

```text
64: SCMaterial = new Material(SCShader);
184: SCShader = Shader.Find(ShaderName);
198: material.SetTexture("_MainTex2", Camera2tex);
217: SCShader = Shader.Find(ShaderName);
221: SCMaterial = new Material(SCShader);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_PinLight.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Saturation.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Screen.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_SoftLight.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_SplitScreen.cs

```text
47: SCMaterial = new Material(SCShader);
67: SCShader = Shader.Find(ShaderName);
81: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Subtract.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_VividLight.cs

```text
31: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
59: material.SetTexture("_MainTex2", Camera2tex);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blizzard.cs

```text
30: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Blizzard");
56: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Bloom.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/Blur_Bloom");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_BlurHole.cs

```text
40: SCMaterial = new Material(SCShader);
49: SCShader = Shader.Find("CameraFilterPack/BlurHole");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Blurry.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/Blur_Blurry");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Dithering2x2.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPack/Blur_Dithering2x2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_DitherOffset.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPack/Blur_DitherOffset");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Focus.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Blur_Focus");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_GaussianBlur.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Blur_GaussianBlur");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Movie.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/Blur_Movie");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Noise.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPack/Blur_Noise");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Radial.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Blur_Radial");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Radial_Fast.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Blur_Radial_Fast");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Regular.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPack/Blur_Regular");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Steam.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/Blur_Steam");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Tilt_Shift.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/BlurTiltShift");
74: material.SetTexture("_MainTex2", temporary2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Tilt_Shift_Hole.cs

```text
37: SCMaterial = new Material(SCShader);
46: SCShader = Shader.Find("CameraFilterPack/BlurTiltShift_Hole");
77: material.SetTexture("_MainTex2", temporary2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Blur_Tilt_Shift_V.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/BlurTiltShift_V");
73: material.SetTexture("_MainTex2", temporary2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_BrightContrastSaturation.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/Color_BrightContrastSaturation");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_Chromatic_Aberration.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Color_Chromatic_Aberration");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_Contrast.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Color_Contrast");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_GrayScale.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Color_GrayScale");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_Invert.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Color_Invert");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_Noise.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Color_Noise");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_RGB.cs

```text
21: SCMaterial = new Material(SCShader);
30: SCShader = Shader.Find("CameraFilterPack/Color_RGB");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_Sepia.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Color_Sepia");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_Switching.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Color_Switching");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Color_YUV.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/Color_YUV");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_Adjust_ColorRGB.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Colors_Adjust_ColorRGB");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_Adjust_FullColors.cs

```text
55: SCMaterial = new Material(SCShader);
64: SCShader = Shader.Find("CameraFilterPack/Colors_Adjust_FullColors");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_Adjust_PreFilters.cs

```text
64: SCMaterial = new Material(SCShader);
334: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_BleachBypass.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Colors_BleachBypass");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_Brightness.cs

```text
20: SCMaterial = new Material(SCShader);
29: SCShader = Shader.Find("CameraFilterPack/Colors_Brightness");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_DarkColor.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Colors_DarkColor");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_HSV.cs

```text
26: SCMaterial = new Material(SCShader);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_HUE_Rotate.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Colors_HUE_Rotate");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_NewPosterize.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Colors_NewPosterize");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_RgbClamp.cs

```text
43: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPack/Colors_RgbClamp");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Colors_Threshold.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Colors_Threshold");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Aspiration.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Distortion_Aspiration");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_BigFace.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPack/Distortion_BigFace");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_BlackHole.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Distortion_BlackHole");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Dissipation.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Distortion_Dissipation");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Dream.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Distortion_Dream");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Dream2.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/Distortion_Dream2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_FishEye.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Distortion_FishEye");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Flag.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPack/Distortion_Flag");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Flush.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Distortion_Flush");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Half_Sphere.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Distortion_Half_Sphere");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Heat.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Distortion_Heat");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Lens.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/Distortion_Lens");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Noise.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Distortion_Noise");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_ShockWave.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Distortion_ShockWave");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Twist.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Distortion_Twist");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Twist_Square.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Distortion_Twist_Square");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Water_Drop.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Distortion_Water_Drop");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Wave_Horizontal.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Distortion_Wave_Horizontal");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_BluePrint.cs

```text
46: SCMaterial = new Material(SCShader);
56: SCShader = Shader.Find("CameraFilterPack/Drawing_BluePrint");
78: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_CellShading.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/Drawing_CellShading");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_CellShading2.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/Drawing_CellShading2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Comics.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Drawing_Comics");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Crosshatch.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Drawing_Crosshatch");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Curve.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Drawing_Curve");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_EnhancedComics.cs

```text
42: SCMaterial = new Material(SCShader);
51: SCShader = Shader.Find("CameraFilterPack/Drawing_EnhancedComics");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Halftone.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/Drawing_Halftone");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Laplacian.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/Drawing_Laplacian");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Lines.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Drawing_Lines");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Drawing_Manga");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga2.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Drawing_Manga2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga3.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Drawing_Manga3");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga4.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Drawing_Manga4");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga5.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Drawing_Manga5");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_Color.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPack/Drawing_Manga_Color");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_Flash.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/Drawing_Manga_Flash");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_Flash_Color.cs

```text
36: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find("CameraFilterPack/Drawing_Manga_Flash_Color");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_FlashWhite.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/Drawing_Manga_FlashWhite");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_NewCellShading.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Drawing_NewCellShading");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Paper.cs

```text
46: SCMaterial = new Material(SCShader);
56: SCShader = Shader.Find("CameraFilterPack/Drawing_Paper");
78: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Paper2.cs

```text
46: SCMaterial = new Material(SCShader);
56: SCShader = Shader.Find("CameraFilterPack/Drawing_Paper2");
78: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Paper3.cs

```text
46: SCMaterial = new Material(SCShader);
56: SCShader = Shader.Find("CameraFilterPack/Drawing_Paper3");
78: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Toon.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/Drawing_Toon");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Edge_BlackLine.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/Edge_BlackLine");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Edge_Edge_filter.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/Edge_Edge_filter");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Edge_Golden.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/Edge_Golden");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Edge_Neon.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Edge_Neon");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Edge_Sigmoid.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Edge_Sigmoid");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Edge_Sobel.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/Edge_Sobel");
```

## Assembly-CSharp-firstpass/CameraFilterPack_EXTRA_Rotation.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/EXTRA_Rotation");
```

## Assembly-CSharp-firstpass/CameraFilterPack_EyesVision_1.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/EyesVision_1");
60: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_EyesVision_2.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/EyesVision_2");
60: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Film_ColorPerfection.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Film_ColorPerfection");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Film_Grain.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Film_Grain");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FlipScreen.cs

```text
21: SCMaterial = new Material(SCShader);
30: SCShader = Shader.Find("CameraFilterPack/FlipScreen");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Fly_Vision.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/Fly_Vision");
61: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_8bits.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/FX_8bits");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_8bits_gb.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/FX_8bits_gb");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Ascii.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/FX_Ascii");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_DarkMatter.cs

```text
37: SCMaterial = new Material(SCShader);
46: SCShader = Shader.Find("CameraFilterPack/FX_DarkMatter");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_DigitalMatrix.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/FX_DigitalMatrix");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_DigitalMatrixDistortion.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/FX_DigitalMatrixDistortion");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Dot_Circle.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/FX_Dot_Circle");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Drunk.cs

```text
53: SCMaterial = new Material(SCShader);
62: SCShader = Shader.Find("CameraFilterPack/FX_Drunk");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Drunk2.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/FX_Drunk2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_EarthQuake.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/FX_EarthQuake");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Funk.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/FX_Funk");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Glitch1.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/FX_Glitch1");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Glitch2.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/FX_Glitch2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Glitch3.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/FX_Glitch3");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Grid.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPack/FX_Grid");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Hexagon.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/FX_Hexagon");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Hexagon_Black.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/FX_Hexagon_Black");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Hypno.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/FX_Hypno");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_InverChromiLum.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/FX_InverChromiLum");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Mirror.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/FX_Mirror");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Plasma.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/FX_Plasma");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Psycho.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/FX_Psycho");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Scan.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/FX_Scan");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Screens.cs

```text
33: SCMaterial = new Material(SCShader);
42: SCShader = Shader.Find("CameraFilterPack/FX_Screens");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_Spot.cs

```text
23: SCMaterial = new Material(SCShader);
32: SCShader = Shader.Find("CameraFilterPack/FX_Spot");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_superDot.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/FX_superDot");
```

## Assembly-CSharp-firstpass/CameraFilterPack_FX_ZebraColor.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/FX_ZebraColor");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Glitch_Mozaic.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Glitch_Mozaic");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Glow_Glow.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/Glow_Glow");
75: material.SetTexture("_MainTex2", temporary);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Glow_Glow_Color.cs

```text
36: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find("CameraFilterPack/Glow_Glow_Color");
78: material.SetTexture("_MainTex2", temporary);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Ansi.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Desert.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_ElectricGradient.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_FireGradient.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Hue.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_NeonGradient.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Rainbow.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Stripe.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Tech.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Therma.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Light_Rainbow.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Light_Rainbow");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Light_Rainbow2.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Light_Rainbow2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Light_Water.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Light_Water");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Light_Water2.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Light_Water2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_NightVision_4.cs

```text
26: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_NightVisionFX.cs

```text
86: SCMaterial = new Material(SCShader);
95: SCShader = Shader.Find("CameraFilterPack/NightVisionFX");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Noise_TV.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/Noise_TV");
61: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Noise_TV_2.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/Noise_TV_2");
61: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Noise_TV_3.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/Noise_TV_3");
61: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision1.cs

```text
27: SCMaterial = new Material(SCShader);
36: SCShader = Shader.Find("CameraFilterPack/Oculus_NightVision1");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision2.cs

```text
26: SCMaterial = new Material(SCShader);
45: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision3.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Oculus_NightVision3");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision5.cs

```text
35: SCMaterial = new Material(SCShader);
54: SCShader = Shader.Find(ShaderName);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Oculus_ThermaVision.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Oculus_ThermaVision");
```

## Assembly-CSharp-firstpass/CameraFilterPack_OldFilm_Cutting1.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/OldFilm_Cutting1");
60: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_OldFilm_Cutting2.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/OldFilm_Cutting2");
60: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Pixel_Pixelisation.cs

```text
26: SCMaterial = new Material(SCShader);
35: SCShader = Shader.Find("CameraFilterPack/Pixel_Pixelisation");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Pixelisation_Dot.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Pixelisation_Dot");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Pixelisation_OilPaint.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Pixelisation_OilPaint");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Pixelisation_OilPaintHQ.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Pixelisation_OilPaintHQ");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Real_VHS.cs

```text
39: SCMaterial = new Material(SCShader);
48: SCShader = Shader.Find("CameraFilterPack/Real_VHS");
66: material.SetTexture("VHS", VHS);
67: material.SetTexture("VHS2", VHS2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_Retro_Loading.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/Retro_Loading");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Sharpen_Sharpen.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/Sharpen_Sharpen");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Special_Bubble.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Special_Bubble");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_50.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_50");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_80.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_80");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_ARCADE");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE_2.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/TV_ARCADE_2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE_3.cs

```text
39: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPack/TV_ARCADE_3");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE_Fast.cs

```text
33: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/TV_ARCADE_Fast");
61: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Artefact.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/TV_Artefact");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_BrokenGlass.cs

```text
36: SCMaterial = new Material(SCShader);
46: SCShader = Shader.Find("CameraFilterPack/TV_BrokenGlass");
64: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_BrokenGlass2.cs

```text
57: SCMaterial = new Material(SCShader);
67: SCShader = Shader.Find("CameraFilterPack/TV_BrokenGlass2");
188: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Chromatical.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/TV_Chromatical");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Chromatical2.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/TV_Chromatical2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_CompressionFX.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_CompressionFX");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Distorted.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/TV_Distorted");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Horror.cs

```text
27: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/TV_Horror");
53: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_LED.cs

```text
28: SCMaterial = new Material(SCShader);
37: SCShader = Shader.Find("CameraFilterPack/TV_LED");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Noise.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_Noise");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Old.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_Old");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Old_Movie.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_Old_Movie");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Old_Movie_2.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/TV_Old_Movie_2");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_PlanetMars.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/TV_PlanetMars");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Posterize.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPack/TV_Posterize");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Rgb.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_Rgb");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Tiles.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/TV_Tiles");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Vcr.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_Vcr");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_VHS.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/TV_VHS");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_VHS_Rewind.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/TV_VHS_Rewind");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Video3D.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/TV_Video3D");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Videoflip.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPack/TV_Videoflip");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_Vintage.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/TV_Vintage");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenCircle.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/TV_WideScreenCircle");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenHorizontal.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/TV_WideScreenHorizontal");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenHV.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/TV_WideScreenHV");
```

## Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenVertical.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/TV_WideScreenVertical");
```

## Assembly-CSharp-firstpass/CameraFilterPack_VHS_Tracking.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPack/VHS_Tracking");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_Aura.cs

```text
33: SCMaterial = new Material(SCShader);
42: SCShader = Shader.Find("CameraFilterPack/Vision_Aura");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_AuraDistortion.cs

```text
33: SCMaterial = new Material(SCShader);
42: SCShader = Shader.Find("CameraFilterPack/Vision_AuraDistortion");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_Crystal.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Vision_Crystal");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_Drost.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Vision_Drost");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_Plasma.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Vision_Plasma");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_Psycho.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Vision_Psycho");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_Rainbow.cs

```text
34: SCMaterial = new Material(SCShader);
43: SCShader = Shader.Find("CameraFilterPack/Vision_Rainbow");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_Tunnel.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Vision_Tunnel");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_Warp.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Vision_Warp");
```

## Assembly-CSharp-firstpass/CameraFilterPack_Vision_Warp2.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPack/Vision_Warp2");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_AAA_WaterDrop.cs

```text
41: SCMaterial = new Material(SCShader);
55: SCShader = Shader.Find("CameraFilterPackLegacy/AAA_WaterDrop");
73: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Atmosphere_Rain.cs

```text
42: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPackLegacy/Atmosphere_Rain");
74: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Atmosphere_Snow_8bits.cs

```text
31: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPackLegacy/Atmosphere_Snow_8bits");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blizzard.cs

```text
30: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPackLegacy/Blizzard");
57: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blur_Blurry.cs

```text
29: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPackLegacy/Blur_Blurry");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blur_Focus.cs

```text
39: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPackLegacy/Blur_Focus");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blur_GaussianBlur.cs

```text
24: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPackLegacy/Blur_GaussianBlur");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Chromatic_Aberration.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPackLegacy/Color_Chromatic_Aberration");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Contrast.cs

```text
24: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPackLegacy/Color_Contrast");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_GrayScale.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPackLegacy/Color_GrayScale");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Invert.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPackLegacy/Color_Invert");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Sepia.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPackLegacy/Color_Sepia");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Distortion_FishEye.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPackLegacy/Distortion_FishEye");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Distortion_Wave_Horizontal.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPackLegacy/Distortion_Wave_Horizontal");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Drawing_Paper.cs

```text
46: SCMaterial = new Material(SCShader);
56: SCShader = Shader.Find("CameraFilterPackLegacy/Drawing_Paper");
79: material.SetTexture("_MainTex2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Edge_BlackLine.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPackLegacy/Edge_BlackLine");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Edge_Neon.cs

```text
24: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPackLegacy/Edge_Neon");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Film_Grain.cs

```text
24: SCMaterial = new Material(SCShader);
33: SCShader = Shader.Find("CameraFilterPackLegacy/Film_Grain");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_8bits_gb.cs

```text
24: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPackLegacy/FX_8bits_gb");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_Funk.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPackLegacy/FX_Funk");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_Glitch1.cs

```text
24: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPackLegacy/FX_Glitch1");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_Hexagon_Black.cs

```text
24: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPackLegacy/FX_Hexagon_Black");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_superDot.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPackLegacy/FX_superDot");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Light_Water2.cs

```text
39: SCMaterial = new Material(SCShader);
52: SCShader = Shader.Find("CameraFilterPackLegacy/Light_Water2");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Noise_TV.cs

```text
41: SCMaterial = new Material(SCShader);
51: SCShader = Shader.Find("CameraFilterPackLegacy/Noise_TV");
78: material.SetTexture("Texture2", Texture2);
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Oculus_NightVision1.cs

```text
31: SCMaterial = new Material(SCShader);
42: SCShader = Shader.Find("CameraFilterPackLegacy/Oculus_NightVision1");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Pixel_Pixelisation.cs

```text
32: SCMaterial = new Material(SCShader);
41: SCShader = Shader.Find("CameraFilterPackLegacy/Pixel_Pixelisation");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Pixelisation_OilPaint.cs

```text
24: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPackLegacy/Pixelisation_OilPaint");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Real_VHS.cs

```text
29: SCMaterial = new Material(SCShader);
38: SCShader = Shader.Find("CameraFilterPackLegacy/Real_VHS");
57: material.SetTexture("VHS", VHS);
58: material.SetTexture("VHS2", VHS2);
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_Sharpen_Sharpen.cs

```text
29: SCMaterial = new Material(SCShader);
40: SCShader = Shader.Find("CameraFilterPackLegacy/Sharpen_Sharpen");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_50.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPackLegacy/TV_50");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_80.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPackLegacy/TV_80");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_ARCADE.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPackLegacy/TV_ARCADE");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_CompressionFX.cs

```text
22: SCMaterial = new Material(SCShader);
31: SCShader = Shader.Find("CameraFilterPackLegacy/TV_CompressionFX");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_LED.cs

```text
25: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPackLegacy/TV_LED");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_Posterize.cs

```text
24: SCMaterial = new Material(SCShader);
34: SCShader = Shader.Find("CameraFilterPackLegacy/TV_Posterize");
```

## Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_Video3D.cs

```text
19: SCMaterial = new Material(SCShader);
28: SCShader = Shader.Find("CameraFilterPackLegacy/TV_Video3D");
```

## Assembly-CSharp-firstpass/ColorSuite.cs

```text
203: _material = new Material(shader);
260: _material.EnableKeyword("COLORSPACE_LINEAR");
264: _material.DisableKeyword("COLORSPACE_LINEAR");
268: _material.EnableKeyword("BALANCING_ON");
273: _material.DisableKeyword("BALANCING_ON");
277: _material.EnableKeyword("TONEMAPPING_ON");
282: _material.DisableKeyword("TONEMAPPING_ON");
284: _material.SetTexture("_Curves", _lutTexture);
288: _material.EnableKeyword("DITHER_ORDERED");
289: _material.DisableKeyword("DITHER_TRIANGULAR");
293: _material.DisableKeyword("DITHER_ORDERED");
294: _material.EnableKeyword("DITHER_TRIANGULAR");
298: _material.DisableKeyword("DITHER_ORDERED");
299: _material.DisableKeyword("DITHER_TRIANGULAR");
```

## Assembly-CSharp-firstpass/CustomLightingManager.cs

```text
112: material.EnableKeyword("LIGHTMAP");
116: material.DisableKeyword("LIGHTMAP");
```

## Assembly-CSharp-firstpass/Kirnu/MarvelousBloom.cs

```text
69: material.SetTexture("_Bloom", renderTexture);
```

## Assembly-CSharp-firstpass/Kirnu/ScreenTextureBlend.cs

```text
67: material.EnableKeyword("B1");
71: material.DisableKeyword("B1");
75: material.EnableKeyword("B2");
79: material.DisableKeyword("B2");
83: material.EnableKeyword("B3");
87: material.DisableKeyword("B3");
91: material.EnableKeyword("B4");
95: material.DisableKeyword("B4");
101: material.SetTexture("_Gradient", gradientTexture);
```

## Assembly-CSharp-firstpass/SA_Ad_EditorUIController.cs

```text
35: GetComponent<Canvas>().sortingOrder = 10001;
```

## Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ShaderLibrary.cs

```text
17: Shader shader = Shader.Find(shaderName);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Bloom.cs

```text
272: screenBlend.SetTexture("_ColorBuffer", source);
319: screenBlend.SetTexture("_ColorBuffer", lensFlareVignetteMask);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/BloomAndFlares.cs

```text
208: screenBlend.SetTexture("_ColorBuffer", source);
248: screenBlend.SetTexture("_ColorBuffer", lensFlareVignetteMask);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/BloomOptimized.cs

```text
92: fastBloomMaterial.SetTexture("_Bloom", renderTexture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Blur.cs

```text
23: m_Material = new Material(blurShader);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/CameraMotionBlur.cs

```text
201: motionBlurMaterial.SetTexture("_NoiseTex", noiseTexture);
202: motionBlurMaterial.SetTexture("_VelTex", temporary);
203: motionBlurMaterial.SetTexture("_NeighbourMaxTex", temporary3);
204: motionBlurMaterial.SetTexture("_TileTexDebug", temporary2);
274: dx11MotionBlurMaterial.SetTexture("_NoiseTex", noiseTexture);
275: dx11MotionBlurMaterial.SetTexture("_VelTex", temporary);
276: dx11MotionBlurMaterial.SetTexture("_NeighbourMaxTex", temporary3);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ColorCorrectionCurves.cs

```text
184: ccDepthMaterial.SetTexture("_RgbTex", rgbChannelTex);
185: ccDepthMaterial.SetTexture("_ZCurve", zCurveTex);
186: ccDepthMaterial.SetTexture("_RgbDepthTex", rgbDepthChannelTex);
192: ccMaterial.SetTexture("_RgbTex", rgbChannelTex);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ColorCorrectionLookup.cs

```text
139: material.SetTexture("_ClutTex", converted3DLut);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ColorCorrectionRamp.cs

```text
13: base.material.SetTexture("_RampTex", textureRamp);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ContrastEnhance.cs

```text
58: contrastCompositeMaterial.SetTexture("_MainTexBlurred", temporary2);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ContrastStretch.cs

```text
41: m_materialLum = new Material(shaderLum);
54: m_materialReduce = new Material(shaderReduce);
67: m_materialAdapt = new Material(shaderAdapt);
80: m_materialApply = new Material(shaderApply);
154: materialApply.SetTexture("_AdaptTex", adaptRenderTex[curAdaptIndex]);
165: materialAdapt.SetTexture("_CurTex", curTexture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/CreaseShading.cs

```text
69: creaseApplyMaterial.SetTexture("_HrDepthTex", temporary);
70: creaseApplyMaterial.SetTexture("_LrDepthTex", renderTexture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/DepthOfFieldDeprecated.cs

```text
256: dofBlurMaterial.SetTexture("_TapLow", lowRezWorkTexture);
257: dofBlurMaterial.SetTexture("_TapMedium", mediumRezWorkTexture);
263: dofMaterial.SetTexture("_TapLowBackground", finalDefocus);
264: dofMaterial.SetTexture("_TapMedium", mediumRezWorkTexture);
283: dofMaterial.SetTexture("_TapLowForeground", finalDefocus);
319: dofBlurMaterial.SetTexture("_TapHigh", from);
375: bokehMaterial.SetTexture("_Source", bokehInfo);
376: bokehMaterial.SetTexture("_MainTex", bokehTexture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/GlobalFog.cs

```text
107: fxMaterial.SetTexture("_MainTex", source);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Grayscale.cs

```text
15: base.material.SetTexture("_RampTex", textureRamp);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ImageEffectBase.cs

```text
19: m_Material = new Material(shader);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/MotionBlur.cs

```text
44: base.material.SetTexture("_MainTex", accumTexture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/NoiseAndGrain.cs

```text
76: dx11NoiseMaterial.SetTexture("_NoiseTex", noiseTexture);
84: dx11NoiseMaterial.SetTexture("_NoiseTex", temporary);
99: noiseMaterial.SetTexture("_NoiseTex", noiseTexture);
108: noiseMaterial.SetTexture("_NoiseTex", temporary2);
123: fxMaterial.SetTexture("_MainTex", source);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/NoiseAndScratches.cs

```text
52: m_MaterialRGB = new Material(shaderRGB);
57: m_MaterialYUV = new Material(shaderYUV);
119: material.SetTexture("_GrainTex", grainTexture);
120: material.SetTexture("_ScratchTex", scratchTexture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/PostEffectsBase.cs

```text
23: if (s.isSupported && (bool)m2Create && m2Create.shader == s)
33: m2Create = new Material(s);
49: if ((bool)m2Create && m2Create.shader == s && s.isSupported)
57: m2Create = new Material(s);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/PostEffectsHelper.cs

```text
18: material.SetTexture("_MainTex", source);
140: material.SetTexture("_MainTex", source);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ScreenOverlay.cs

```text
50: overlayMaterial.SetTexture("_Overlay", texture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ScreenSpaceAmbientObscurance.cs

```text
64: aoMaterial.SetTexture("_Rand", rand);
91: aoMaterial.SetTexture("_AOTex", renderTexture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ScreenSpaceAmbientOcclusion.cs

```text
46: return new Material(shader)
96: m_SSAOMaterial.SetTexture("_RandomTexture", m_RandomTexture);
141: m_SSAOMaterial.SetTexture("_SSAO", renderTexture);
146: m_SSAOMaterial.SetTexture("_SSAO", temporary);
151: m_SSAOMaterial.SetTexture("_SSAO", renderTexture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/SunShafts.cs

```text
96: sunShaftsMaterial.SetTexture("_Skybox", renderTexture);
130: sunShaftsMaterial.SetTexture("_ColorBuffer", temporary);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/TiltShift.cs

```text
71: tiltShiftMaterial.SetTexture("_Blurred", renderTexture);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Tonemapping.cs

```text
143: tonemapMaterial.SetTexture("_Curve", curveTex);
205: tonemapMaterial.SetTexture("_SmallTex", rt);
```

## Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/VignetteAndChromaticAberration.cs

```text
91: m_VignetteMaterial.SetTexture("_VignetteTex", renderTexture2);
```
