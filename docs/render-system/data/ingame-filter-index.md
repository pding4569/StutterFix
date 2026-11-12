# OnRenderImage 전수 인덱스

Blit/temp는 파일 전체의 정적 호출 지점 수. 분기·루프 때문에 프레임당 횟수와 다르다.

| 파일:줄 | Blit 지점 | GetTemporary 지점 | Update/LateUpdate |
|---|---:|---:|---|
| Assembly-CSharp/BlendModes/BlendModeEffect.cs:528 | 0 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Anomaly.cs:57 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Binary.cs:58 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_BlackHole.cs:55 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Computer.cs:55 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Distortion.cs:58 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Fog_Smoke.cs:61 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Ghost.cs:63 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Inverse.cs:52 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Matrix.cs:55 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Mirror.cs:55 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Myst.cs:61 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Scan_Scene.cs:56 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Shield.cs:67 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_3D_Snow.cs:60 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_AAA_Blood.cs:49 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_AAA_Blood_Hit.cs:73 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_AAA_Blood_Plus.cs:73 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_AAA_BloodOnScreen.cs:51 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_AAA_WaterDropPro.cs:46 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Atmosphere_Fog.cs:47 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Atmosphere_Rain_Pro_3D.cs:82 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Blend2Camera_ColorKey.cs:67 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Blend2Camera_SplitScreen3D.cs:79 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Broken_Screen.cs:40 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Broken_Simple.cs:46 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Broken_Spliter.cs:40 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Classic_ThermalVision.cs:52 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Color_Adjust_Levels.cs:46 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Color_Chromatic_Plus.cs:40 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Convert_Normal.cs:35 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Distortion_ShockWaveManual.cs:43 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_EXTRA_SHOWFPS.cs:52 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Glasses_On.cs:61 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Glasses_On_2.cs:61 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Glasses_On_3.cs:61 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Glasses_On_4.cs:61 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Glasses_On_5.cs:61 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Glasses_On_6.cs:61 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Lut_2_Lut.cs:131 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Lut_2_Lut_Extra.cs:137 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Lut_Mask.cs:122 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Lut_PlayWith.cs:124 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Lut_Plus.cs:117 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Lut_Simple.cs:112 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Lut_TestMode.cs:127 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_NewGlitch1.cs:37 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_NewGlitch2.cs:37 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_NewGlitch3.cs:37 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_NewGlitch4.cs:37 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_NewGlitch5.cs:52 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_NewGlitch6.cs:40 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_NewGlitch7.cs:40 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Pixelisation_DeepOilPaintHQ.cs:52 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Pixelisation_Sweater.cs:43 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Rain_RainFX.cs:50 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_TV_MovieNoise.cs:34 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_TV_Vignetting.cs:43 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Vision_Blood.cs:43 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Vision_Blood_Fast.cs:43 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Vision_Hell_Blood.cs:54 | 2 | 0 | Update |
| Assembly-CSharp/CameraFilterPack_Vision_SniperScore.cs:66 | 2 | 0 | Update |
| Assembly-CSharp/PolarScreen.cs:28 | 2 | 0 | Update |
| Assembly-CSharp/ScreenScroll.cs:33 | 2 | 0 | Update |
| Assembly-CSharp/ScreenTile.cs:31 | 2 | 0 | Update |
| Assembly-CSharp/TNOFilter.cs:36 | 2 | 0 | Update |
| Assembly-CSharp/VideoBloom.cs:177 | 11 | 7 |  |
| Assembly-CSharp-firstpass/CameraFilterPack_AAA_SuperComputer.cs:51 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_AAA_SuperHexagon.cs:49 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_AAA_WaterDrop.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Alien_Vision.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Antialiasing_FXAA.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Atmosphere_Rain.cs:55 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Atmosphere_Rain_Pro.cs:58 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Atmosphere_Snow_8bits.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Blend.cs:45 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_BlueScreen.cs:71 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Color.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_ColorBurn.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_ColorDodge.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Darken.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_DarkerColor.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Difference.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Divide.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Exclusion.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_GreenScreen.cs:65 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_HardLight.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_HardMix.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Hue.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Lighten.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LighterColor.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LinearBurn.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LinearDodge.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LinearLight.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Luminosity.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Multiply.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Overlay.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_PhotoshopFilters.cs:187 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_PinLight.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Saturation.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Screen.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_SoftLight.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_SplitScreen.cs:70 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Subtract.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_VividLight.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blizzard.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Bloom.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_BlurHole.cs:52 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Blurry.cs:37 | 4 | 1 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Dithering2x2.cs:36 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_DitherOffset.cs:36 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Focus.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_GaussianBlur.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Movie.cs:40 | 4 | 1 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Noise.cs:36 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Radial.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Radial_Fast.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Regular.cs:36 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Steam.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Tilt_Shift.cs:46 | 7 | 2 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Tilt_Shift_Hole.cs:49 | 7 | 2 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Tilt_Shift_V.cs:46 | 7 | 2 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_BrightContrastSaturation.cs:40 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Chromatic_Aberration.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Contrast.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_GrayScale.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Invert.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Noise.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_RGB.cs:33 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Sepia.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Switching.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_YUV.cs:40 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Adjust_ColorRGB.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Adjust_FullColors.cs:67 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Adjust_PreFilters.cs:337 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_BleachBypass.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Brightness.cs:32 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_DarkColor.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_HSV.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_HUE_Rotate.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_NewPosterize.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_RgbClamp.cs:55 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Threshold.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Aspiration.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_BigFace.cs:36 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_BlackHole.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Dissipation.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Dream.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Dream2.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_FishEye.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Flag.cs:36 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Flush.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Half_Sphere.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Heat.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Lens.cs:40 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Noise.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_ShockWave.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Twist.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Twist_Square.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Water_Drop.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Wave_Horizontal.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_BluePrint.cs:59 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_CellShading.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_CellShading2.cs:40 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Comics.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Crosshatch.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Curve.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_EnhancedComics.cs:54 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Halftone.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Laplacian.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Lines.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga2.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga3.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga4.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga5.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_Color.cs:36 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_Flash.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_Flash_Color.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_FlashWhite.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_NewCellShading.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Paper.cs:59 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Paper2.cs:59 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Paper3.cs:59 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Toon.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_BlackLine.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Edge_filter.cs:40 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Golden.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Neon.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Sigmoid.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Sobel.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_EXTRA_Rotation.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_EyesVision_1.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_EyesVision_2.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Film_ColorPerfection.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Film_Grain.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FlipScreen.cs:33 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Fly_Vision.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_8bits.cs:40 | 3 | 1 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_8bits_gb.cs:34 | 3 | 1 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Ascii.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_DarkMatter.cs:49 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_DigitalMatrix.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_DigitalMatrixDistortion.cs:40 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Dot_Circle.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Drunk.cs:65 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Drunk2.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_EarthQuake.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Funk.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Glitch1.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Glitch2.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Glitch3.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Grid.cs:36 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Hexagon.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Hexagon_Black.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Hypno.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_InverChromiLum.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Mirror.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Plasma.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Psycho.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Scan.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Screens.cs:45 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Spot.cs:35 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_superDot.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_ZebraColor.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Glitch_Mozaic.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Glow_Glow.cs:46 | 8 | 2 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Glow_Glow_Color.cs:48 | 8 | 2 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Ansi.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Desert.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_ElectricGradient.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_FireGradient.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Hue.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_NeonGradient.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Rainbow.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Stripe.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Tech.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Therma.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Light_Rainbow.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Light_Rainbow2.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Light_Water.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Light_Water2.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_NightVision_4.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_NightVisionFX.cs:98 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Noise_TV.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Noise_TV_2.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Noise_TV_3.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision1.cs:39 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision2.cs:48 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision3.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision5.cs:57 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_ThermaVision.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_OldFilm_Cutting1.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_OldFilm_Cutting2.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Pixel_Pixelisation.cs:38 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Pixelisation_Dot.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Pixelisation_OilPaint.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Pixelisation_OilPaintHQ.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Real_VHS.cs:62 | 3 | 1 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Retro_Loading.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Sharpen_Sharpen.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Special_Bubble.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_50.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_80.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE_2.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE_3.cs:55 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE_Fast.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Artefact.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_BrokenGlass.cs:49 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_BrokenGlass2.cs:70 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Chromatical.cs:40 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Chromatical2.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_CompressionFX.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Distorted.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Horror.cs:40 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_LED.cs:40 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Noise.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Old.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Old_Movie.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Old_Movie_2.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_PlanetMars.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Posterize.cs:37 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Rgb.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Tiles.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Vcr.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_VHS.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_VHS_Rewind.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Video3D.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Videoflip.cs:31 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Vintage.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenCircle.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenHorizontal.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenHV.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenVertical.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_VHS_Tracking.cs:34 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Aura.cs:45 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_AuraDistortion.cs:45 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Crystal.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Drost.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Plasma.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Psycho.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Rainbow.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Tunnel.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Warp.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Warp2.cs:43 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_AAA_WaterDrop.cs:59 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Atmosphere_Rain.cs:56 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Atmosphere_Snow_8bits.cs:44 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blizzard.cs:44 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blur_Blurry.cs:44 | 4 | 1 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blur_Focus.cs:56 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blur_GaussianBlur.cs:38 | 3 | 1 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Chromatic_Aberration.cs:42 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Contrast.cs:38 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_GrayScale.cs:35 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Invert.cs:32 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Sepia.cs:35 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Distortion_FishEye.cs:42 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Distortion_Wave_Horizontal.cs:42 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Drawing_Paper.cs:60 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Edge_BlackLine.cs:32 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Edge_Neon.cs:38 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Film_Grain.cs:42 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_8bits_gb.cs:38 | 3 | 1 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_Funk.cs:32 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_Glitch1.cs:38 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_Hexagon_Black.cs:38 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_superDot.cs:32 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Light_Water2.cs:56 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Noise_TV.cs:63 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Oculus_NightVision1.cs:46 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Pixel_Pixelisation.cs:52 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Pixelisation_OilPaint.cs:38 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Real_VHS.cs:53 | 3 | 1 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Sharpen_Sharpen.cs:44 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_50.cs:32 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_80.cs:32 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_ARCADE.cs:32 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_CompressionFX.cs:35 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_LED.cs:38 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_Posterize.cs:38 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_Video3D.cs:32 | 2 | 0 | Update |
| Assembly-CSharp-firstpass/ColorSuite.cs:254 | 1 | 0 |  |
| Assembly-CSharp-firstpass/Kirnu/MarvelousBloom.cs:41 | 5 | 3 |  |
| Assembly-CSharp-firstpass/Kirnu/ScreenTextureBlend.cs:58 | 2 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Antialiasing.cs:88 | 10 | 1 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Bloom.cs:116 | 29 | 8 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/BloomAndFlares.cs:95 | 20 | 4 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/BloomOptimized.cs:61 | 5 | 3 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Blur.cs:58 | 1 | 2 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/BlurOptimized.cs:50 | 5 | 3 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/CameraMotionBlur.cs:142 | 14 | 3 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ColorCorrectionCurves.cs:144 | 4 | 1 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ColorCorrectionLookup.cs:124 | 2 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ColorCorrectionRamp.cs:11 | 1 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ContrastEnhance.cs:36 | 6 | 4 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ContrastStretch.cs:132 | 4 | 2 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/CreaseShading.cs:41 | 6 | 4 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/DepthOfFieldDeprecated.cs:195 | 25 | 8 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/EdgeDetection.cs:80 | 2 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Fisheye.cs:29 | 2 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/GlobalFog.cs:45 | 1 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Grayscale.cs:13 | 1 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/MotionBlur.cs:27 | 5 | 1 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/NoiseAndGrain.cs:61 | 3 | 2 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/NoiseAndScratches.cs:108 | 1 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/PostEffectsHelper.cs:10 | 0 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ScreenOverlay.cs:40 | 2 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ScreenSpaceAmbientObscurance.cs:52 | 6 | 4 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ScreenSpaceAmbientOcclusion.cs:101 | 4 | 3 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/SepiaTone.cs:9 | 1 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/SunShafts.cs:63 | 6 | 4 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/TiltShift.cs:50 | 3 | 1 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Tonemapping.cs:131 | 14 | 2 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Twirl.cs:15 | 0 | 0 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/VignetteAndChromaticAberration.cs:57 | 6 | 4 |  |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Vortex.cs:15 | 0 | 0 |  |