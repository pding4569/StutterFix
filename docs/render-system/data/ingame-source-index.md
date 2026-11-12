# 게임 소스 전수 정적 인덱스

자동 생성: `python tools/analysis/ingame_audit.py source`.
파일 전체를 어휘 검색한 인덱스다. 숫자는 호출 지점이 있는 줄 수이며 실행 횟수/할당량/실행 시간은 아니다.
실제 재생 시 활성 여부와 함수 간 도달성은 본문 분석과 런타임 자료로 따로 판정한다.

| 파일 | 줄 | SHA256 앞16자리 | 콜백(줄) | 반복문 | 할당 표현 | Transform | 재질 | 검색 | 렌더 |
|---|---:|---|---|---:|---:|---:|---:|---:|---:|
| Assembly-CSharp/AbsoluteRotationShadow.cs | 22 | 8bf72d3d2c336526 | LateUpdate:14 | 0 | 2 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/AchievementDisplay.cs | 302 | bbc04a76af72cb84 | Update:229 | 2 | 7 | 11 | 0 | 2 | 0 |
| Assembly-CSharp/ActionEntry.cs | 17 | 1afc2a429b65e5e6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ActionEntryArg.cs | 22 | 9c96d67878e80b50 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOBase.cs | 472 | a0e7e9b6077fddf6 |  | 1 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOClass.cs | 49 | fa49ceb64e695480 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/CategoryTab.cs | 63 | f41872412f0d335e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/ControlType.cs | 16 | 99f9cde62dd384fc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/DecorationPivot.cs | 18 | d1b81ce37a30beaa |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/DynamicallyOrderedFont.cs | 40 | 6b3434d5656d7885 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/EditorConstants.cs | 281 | eae0ef4b1fee059d |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/EditorGizmo.cs | 32 | 38203c5abcf1e014 | LateUpdate:19 | 0 | 0 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/ErrorMeterTick.cs | 67 | c1ec88426f253a5a |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/EventIndicator.cs | 59 | e872e9d8d9ee72d9 | LateUpdate:46 | 0 | 1 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/FileType.cs | 8 | 62fb5f8756e26065 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/FloorDirectionButton.cs | 79 | a5e73c6f26570afb |  | 0 | 5 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/FloorHelper.cs | 366 | d77e013cf175f2a4 |  | 1 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/FolderDataCLS.cs | 112 | c49e8e1a59a6051c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/FPSCounter.cs | 135 | 24b1447ad8c94ce3 | Update:59 | 2 | 3 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/ADOFAI/GenericDataCLS.cs | 48 | 8263180b3fb5c8a2 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/InspectorPanel.cs | 910 | e5ef90b6c0512c4c | Update:206, LateUpdate:844 | 21 | 14 | 8 | 0 | 13 | 0 |
| Assembly-CSharp/ADOFAI/InspectorTab.cs | 104 | 6f64f1b8d664fc8d |  | 0 | 1 | 3 | 0 | 1 | 0 |
| Assembly-CSharp/ADOFAI/KeyIndicator.cs | 81 | d1cd5e5bb2f821d2 | LateUpdate:27 | 2 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ADOFAI/LevelData.cs | 918 | 2a8b565a0577c8fc |  | 13 | 45 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/LevelDataCLS.cs | 94 | 361fe1f8b36928db |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/LevelEvent.cs | 678 | 60dfc8fdda51f048 |  | 9 | 21 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/LevelEventButton.cs | 119 | 589f2ca927cd50bc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/LevelEventCategory.cs | 13 | b744734d6e7b99f7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/LevelEventExecutionTime.cs | 8 | 4ce8e0adcf3b687a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/LevelEventInfo.cs | 69 | df17709c156b4da0 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/LevelEventType.cs | 71 | e9fa946a1966d829 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/ListItem.cs | 160 | 7c3c8d54e21c0558 | LateUpdate:76 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/ListItem_Decoration.cs | 142 | 9e565be0150ff388 | LateUpdate:56 | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/ListItem_Event.cs | 20 | bbb8d6e914e86723 |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/LoadResult.cs | 12 | f7b9ed785098814e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/MinimizedFloorData.cs | 31 | e289f3ba85c5a1db |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/PropertiesPanel.cs | 654 | 8b8a85fca58e4c25 | Update:80 | 10 | 16 | 8 | 0 | 18 | 0 |
| Assembly-CSharp/ADOFAI/Property.cs | 80 | b0e60243b3fa7b72 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/PropertyInfo.cs | 653 | 5f17d8dbbb551318 |  | 6 | 22 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/PropertyType.cs | 26 | c77af4020402f900 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI/RDColorPickerPopup.cs | 192 | db146fcc480d796c | Update:93 | 1 | 4 | 1 | 0 | 4 | 0 |
| Assembly-CSharp/ADOFAI/TransformGizmo.cs | 87 | 4e39767e7af33110 |  | 0 | 9 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.CLS/scrClickableBadgeCLS.cs | 72 | 11cceb20b41f6f35 | Update:28 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Common.Platform/AudioDevice.cs | 14 | 76d86e9bb696de11 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Common.Platform/IPlatformHelper.cs | 12 | f637239ab23c4808 | Update:9 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Common.Platform/PlatformHelper.cs | 23 | 25ff12cae11b832a |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Common.Platform/PlatformHelperDefault.cs | 25 | 8b6a8b5fb29d092a | Update:22 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Common.Platform.Linux/PlatformHelperLinux.cs | 69 | f6a2a3f74dfc0a6c | Update:66 | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Common.Platform.Mac/PlatformHelperMac.cs | 64 | 0ab2bb8861eff128 | Update:61 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Common.Platform.Windows/PlatformHelperWindows.cs | 90 | f6c93da6f539c5a8 | Update:79 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.DOTweenExt/MinMaxCurveTween.cs | 73 | 2cda6e04c389acee |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor/EditorKeybind.cs | 113 | 9683eb83476cf8df |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor/EditorKeybindManager.cs | 60 | 6067910fbc2a01f9 |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor/KeyModifier.cs | 13 | 831d465e596b9255 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor/PropertiesSubTabButton.cs | 36 | 17dc96320e72cc67 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/AddNumberedEventEditorAction.cs | 24 | 7d6e4eb4cab176cf |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CompositeEditorAction.cs | 22 | b1bb5b6b6343ef7c |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ConditionalEditorAction.cs | 35 | 4145961dae8f6783 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CopyAllSameTypeEventsEditorAction.cs | 14 | 4b5fdec0b28b500f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CopyEventsEditorAction.cs | 14 | b540739969a7449e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CopyFloorEditorAction.cs | 32 | 86fc3a2d300a897f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CopyHitSoundEditorAction.cs | 14 | b63f488ca6a5ba18 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CopyTrackColorEditorAction.cs | 14 | d237bf664342c419 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/Create360FloorEditorAction.cs | 16 | cd5fc0b11e98ba65 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CreateArbitraryFloorAction.cs | 17 | d2614381270c4c11 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CreateFloorWithCharOrAngleEditorAction.cs | 48 | 9bb3d9662863d0ed |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CreateHeptagonEditorAction.cs | 18 | df46634084587d8e |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CreateMidspinFloorEditorAction.cs | 20 | 477ee0552a9a45a2 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CreatePentagonEditorAction.cs | 18 | 83e440c55c472d0f |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CreateUpsideDownHeptagonEditorAction.cs | 18 | 8ecc7da6574fea74 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CreateUpsideDownPentagonEditorAction.cs | 18 | ef8ae58b827a6707 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CutAllSameTypeEventsEditorAction.cs | 14 | ea59ff160bfc4852 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CutEventsEditorAction.cs | 14 | 70bc7750694da3aa |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CutFloorEditorAction.cs | 32 | e99bdc1fbe204217 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CycleNextEventTabEditorAction.cs | 16 | c09ea6a94761df53 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CycleNextSelectedEventEditorAction.cs | 16 | 831774264a4a6b71 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CyclePreviousEventTabEditorAction.cs | 16 | ccf768fa19d4e3d7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/CyclePreviousSelectedEventEditorAction.cs | 16 | b1b868837cdd6dd4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/DeleteDecorationsEditorAction.cs | 14 | 2d6adf03912aaab4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/DeleteFloorsEditorAction.cs | 25 | 396dae2b069e8b3f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/DeletePrecedingFloorsEditorAction.cs | 11 | 7673c0abccfa2e62 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/DeleteSubsequentFloorsEditorAction.cs | 11 | f302c47c87aa555b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/DeselectAllEditorAction.cs | 15 | 05592a0d79db8a0e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/DuplicateDecorationsEditorAction.cs | 14 | 4d8771700faa7260 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/EditorAction.cs | 10 | b1219c54677408d0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/EditorTabKey.cs | 14 | a9dc3167092082ed |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/FlipFloorsHorizontalEditorAction.cs | 21 | 050647deaad562fd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/FlipFloorsVerticalEditorAction.cs | 21 | de7fb1f6801f5ae7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/LockSelectedDecorationsEditorAction.cs | 38 | 75e7c02b02c44703 |  | 3 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/MoveSelectionLeftEditorAction.cs | 25 | f72f233aaf5a2527 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/MoveSelectionRightEditorAction.cs | 25 | 931222aa53c908f6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/NewLevelEditorAction.cs | 12 | 15f916dd33e2c0bd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/OpenLevelEditorAction.cs | 12 | 46db987cbf1e2d0a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/OpenLogDirectoryEditorAction.cs | 11 | 6b180f0ab17b8164 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/OpenPreferencesEditorAction.cs | 11 | 1076274c18dd9f07 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/OpenRecentEditorAction.cs | 19 | 70bfddc64b2a2346 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/OpenUrlEditorAction.cs | 15 | d441c684411c36d6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/PasteEventsEditorAction.cs | 73 | 598a36d3e4ca47ab |  | 2 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/PasteFloorEditorAction.cs | 55 | fdada68dd2812825 |  | 1 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/PasteFloorWithoutDecorationsEditorAction.cs | 55 | af94134b3768a767 |  | 1 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/PasteHitSoundSingleTileEditorAction.cs | 14 | 48e70ac9f66f9b71 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/PasteTrackColorEditorAction.cs | 14 | c8f162be0814a2a8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/PasteTrackColorSingleTileEditorAction.cs | 14 | 60c692cfb50c18bf |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/PlayEditorAction.cs | 11 | df12418a608c9a27 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/PlayWithSpeedEditorAction.cs | 11 | 0d7505cd8b3609b5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/RedoEditorAction.cs | 11 | 0eb875c7faaf85fb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/RotateFloors180EditorAction.cs | 21 | f39295ca4d839971 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/RotateFloors90ClockwiseEditorAction.cs | 23 | 2f3d0895021d8e7b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/RotateFloors90CounterClockwiseEditorAction.cs | 23 | 68057a8a3f10ab61 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SaveLevelAsEditorAction.cs | 19 | 72ab4a873c06f7d3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SaveLevelEditorAction.cs | 12 | 8d3e784508dde076 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SelectEventCategoryEditorAction.cs | 29 | c8a7eb12ac78730d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SelectFirstFloorEditorAction.cs | 11 | 580899ca3838f0de |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SelectLastFloorEditorAction.cs | 11 | 571d4043faa7ce7f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SelectNextBookmarkEditorAction.cs | 18 | 634dfa68e65f8024 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SelectNextFloorEditorAction.cs | 16 | 7422d74ecdb07088 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SelectPreviousBookmarkEditorAction.cs | 18 | b376142031ca38fd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SelectPreviousFloorEditorAction.cs | 16 | 8910d751c3b66c0b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SelectToFirstFloorEditorAction.cs | 21 | a2adcf312333a3f6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SelectToLastFloorEditorAction.cs | 21 | a5f686a6e7f5a675 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ShowCopyrightPopupEditorAction.cs | 11 | c7a6e7b6d7dca827 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ShowCurrentFloorNumberEditorAction.cs | 17 | 014e790a1b5a9fd2 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ShowFirstEventPageEditorAction.cs | 11 | cad4a983a828854d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ShowLastEventPageEditorAction.cs | 11 | 56cc94d2acce6cdf |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ShowNextEventPageEditorAction.cs | 13 | 1d79da20bab65871 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ShowPreviousEventPageEditorAction.cs | 13 | fbf952aaa2913052 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/SimpleEditorAction.cs | 20 | f56faddfb1141a9c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ToggleAutoEditorAction.cs | 14 | 6d729b035971a56b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ToggleBookmarkEditorAction.cs | 21 | 87a6077a685bc9bd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ToggleFileActionsPanelEditorAction.cs | 14 | 80cd7d7d259a821a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ToggleFindCommentPanelEditorAction.cs | 11 | 00a867b7e9228a6e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ToggleFindFloorPanelEditorAction.cs | 11 | a96d4ed2cea22325 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ToggleFloorNumsEditorAction.cs | 17 | a3c7f56907fbdd56 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ToggleNoFailEditorAction.cs | 14 | 54af0126579a7f4a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ToggleShortcutsPanelEditorAction.cs | 11 | 9e72964d4fd4b97f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/TryQuitToMenuEditorAction.cs | 11 | 8d83ac8d222f2d36 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/UndoEditorAction.cs | 11 | 1c8f37231cd2049a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ZoomInCameraEditorAction.cs | 11 | a7c52ede758c04a4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ZoomInUiEditorAction.cs | 11 | c39eb1ef54e93599 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ZoomOutCameraEditorAction.cs | 11 | cfc00759ae113ddb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Actions/ZoomOutUiEditorAction.cs | 11 | 65e8992c08c06a43 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Components/ColorField.cs | 143 | 7315b28e738107a1 |  | 0 | 3 | 6 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Components/DraggableNumberInputField.cs | 128 | e6fa9f2a4c5b92d1 | Update:72 | 1 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Components/MinMaxControl.cs | 113 | 7b7a305bef9e2bc3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Components/Switch.cs | 66 | 115845b0ea4a16e6 | Update:53 | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Components.Gradients/GradientEditor.cs | 719 | ac0785b514cfdaa2 | Update:131 | 6 | 40 | 15 | 0 | 2 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Components.Gradients/GradientField.cs | 40 | e002a8766c3bcd9a |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Components.Gradients/GradientGenerator.cs | 43 | 0a9b8a45078b7149 |  | 2 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Components.Gradients/GradientMarker.cs | 45 | 49821d75730f6b9f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Components.Gradients/GradientMarkerLine.cs | 15 | 04c8421744ac8e52 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Interfaces/IColorPickerData.cs | 16 | cb17b5e8816966a8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Models/SerializedGradient.cs | 73 | 27e3aec6a997f2d0 |  | 0 | 9 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Models/SerializedMinMaxGradient.cs | 74 | 18055d990a3f291d |  | 0 | 8 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Panels/FindCommentPanel.cs | 121 | 1e4f0be35fbd9cc6 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.ParticleEditor/ParticleEditor.cs | 235 | 352fe20e6f0888e2 | Update:205 | 3 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.ParticleEditor/ParticleEditorTabType.cs | 8 | 3687fc4e799df01b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences/EditorPreferencesControlType.cs | 7 | ffe815a7bd685482 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences/EditorPreferencesField.cs | 13 | 214b85e8b6fe66ec |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences/EditorPreferencesMenu.cs | 159 | 770379b05a54091e | Update:45 | 2 | 14 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences/EditorPreferencesTabButton.cs | 38 | 3209d6b49cdecdd7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences/EditorPreferencesTabContent.cs | 12 | dc7f904ff258a1e5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences.Controls/EditorPreferencesControl.cs | 18 | 96ddb7a0227877bf |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences.Controls/EditorPreferencesToggle.cs | 28 | dab05810d06bacc2 |  | 0 | 0 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences.Controls/EditorPreferencesValueControl.cs | 31 | e439ba4c9b484254 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences.Controls/EditorsPreferencesText.cs | 24 | 8cfe8aa2d120633b |  | 0 | 0 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences.Models/EditorPreferencesCategory.cs | 43 | bb8a7f2b93c82bc0 |  | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Editor.Preferences.Models/EditorPreferencesEntry.cs | 21 | 3b66f7e1cda53c63 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.FloorFX/ffxEmitParticlePlus.cs | 34 | 15d77cc6f2ed2f3e |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.FloorFX/ffxSetParticlePlus.cs | 320 | 71eabe54ea86d38e |  | 2 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor/TMP_InputFieldPolyfill.cs | 31 | 2c7fdfd2da9d3cd9 |  | 1 | 0 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/BrowseButton.cs | 95 | 754fea299ee37950 | Update:41 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl.cs | 171 | 85dbfabf05137209 |  | 6 | 0 | 1 | 0 | 4 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Bool.cs | 88 | 8c14e20210bddf72 |  | 0 | 2 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Color.cs | 61 | 73c139307635351c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_DecorationsList.cs | 351 | 4f1ea3836ac9becf | LateUpdate:99 | 14 | 4 | 6 | 0 | 2 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_EventsList.cs | 160 | 0476481ae8c87438 | LateUpdate:34 | 7 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Export.cs | 37 | 0fde410dd5d5eb03 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_File.cs | 181 | c910d776abbbb9ea | Update:40 | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_FilterProperties.cs | 204 | 2779d2cf82b0bde0 |  | 3 | 11 | 3 | 0 | 8 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_FloatPair.cs | 97 | 5b2c3af4e51ed971 |  | 0 | 4 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_FloatPairBase.cs | 45 | 1195a3d8dab1c957 |  | 0 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_List.cs | 601 | da0fc892d59e65cd | Update:149, LateUpdate:192 | 9 | 8 | 8 | 0 | 2 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_LongText.cs | 56 | a7b52c3849196ee1 |  | 0 | 3 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_MinMaxGradient.cs | 234 | d67cd42a5901c453 |  | 0 | 14 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Note.cs | 13 | df65164571096b30 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_ParticlePlayback.cs | 78 | 4c60856a9f621528 | Update:53 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Rating.cs | 67 | 04f08b571eb73703 |  | 2 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Slider.cs | 84 | e69489214ee8f3b5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Text.cs | 250 | bc4f1045cdec0136 | Update:71 | 0 | 5 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Tile.cs | 69 | 1fff5fc5bc11fefb |  | 2 | 5 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Toggle.cs | 324 | d22d72123d14ccc2 | Update:189 | 6 | 9 | 3 | 0 | 4 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Vector2.cs | 191 | 8031370f5c7f28d6 |  | 0 | 6 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControl_Vector2Range.cs | 140 | 27a14418e73104dd |  | 0 | 3 | 1 | 0 | 4 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/PropertyControlRightClickHandler.cs | 17 | 2c227595e5c7a094 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.LevelEditor.Controls/SliderUtils.cs | 22 | b68b22d0e18d8242 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.ModdingConvenience/PrefabLibrary.cs | 43 | 1be625b6bbef3d20 |  | 0 | 1 | 2 | 0 | 2 | 0 |
| Assembly-CSharp/ADOFAI.ModdingConvenience.DocumentationType/FieldDocumentationAttribute.cs | 10 | a3e1de957e100398 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.ModdingConvenience.DocumentationType/MethodDocumentationAttribute.cs | 10 | 601836e20f4c2d1b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.Serialization/LevelArrayConverter.cs | 70 | fa8f78b272333dff |  | 3 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.SteamIntegration/SteamAchievement.cs | 34 | 2b97699d9e2e3ae4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.SteamIntegration/SteamAchievements.cs | 55 | a03aa584ecc26f1c |  | 1 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.SteamIntegration/SteamController.cs | 61 | 8016b000dec6990b | Update:35 | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.SteamIntegration/SteamStats.cs | 35 | e8acb36b5f7a87b6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.SteamIntegration/SteamWorkshop.cs | 198 | b42eb81e77dd128e |  | 3 | 13 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ADOFAI.SteamIntegration/SteamWorkshopItem.cs | 19 | 5dc4cb742a3810bc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AdofaiEventTrigger.cs | 102 | 7506403c8ff25b85 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AdofaiPointerEventData.cs | 3 | e8728985b4f1a4f3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AdoPackageInstaller.cs | 294 | d49a43488281be39 |  | 4 | 8 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/ADOStartup.cs | 422 | 88a23ce301ebe809 |  | 11 | 14 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/AlphaSlider.cs | 36 | c9dd90431b307891 | LateUpdate:19 | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/Analytics.cs | 86 | f2fdc9a38b4c400e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AnalyticsShowScene.cs | 39 | e02de4653485b000 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AngleCorrectionDirection.cs | 6 | a2e8909a3e170497 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AnyKeyCode.cs | 84 | 3cc9afdada255819 |  | 1 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ApprovalLevel.cs | 9 | 476095185a27f9ab |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ApprovalLevelBadge.cs | 100 | f22c7eb66f397fec | Update:35 | 0 | 5 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/AreaCover.cs | 95 | 88d38d0527e44157 | Update:30 | 0 | 4 | 10 | 0 | 0 | 0 |
| Assembly-CSharp/ArtistData.cs | 55 | b1a219ea20c711bb |  | 2 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ArtistDisclaimer.cs | 20 | 43a5e165c702f639 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ArtistUIDisclaimer.cs | 189 | f7798bbf617ac478 |  | 0 | 3 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ArtistUISettings.cs | 19 | 879d71caf98f1b52 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AsyncInput.cs | 104 | 8d0bd7e49e0122dd |  | 3 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AsyncInputManager.cs | 224 | a657906b62de5a7c | Update:136, OnGUI:178 | 1 | 13 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AsyncInputUtils.cs | 80 | a649eb4b8b0ce4cb |  | 2 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AsyncKeyCode.cs | 70 | 26b9a1defbab2fad |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AudioclipToOggEncoder.cs | 115 | c7463a9b98b88204 |  | 7 | 11 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/AudioManager.cs | 398 | 33812c440a734cad | Update:96 | 6 | 17 | 1 | 0 | 6 | 0 |
| Assembly-CSharp/BarFade.cs | 20 | f238d4edae7f2f84 | Update:14 | 0 | 1 | 2 | 1 | 1 | 0 |
| Assembly-CSharp/BarRotate.cs | 22 | d562d7a45d264241 | LateUpdate:11 | 0 | 0 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/BarState.cs | 6 | fb148815d61b9039 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BBManager.cs | 108 | 7b93490d60249b90 | Update:51 | 1 | 2 | 1 | 2 | 2 | 0 |
| Assembly-CSharp/BgDisplayMode.cs | 6 | 9c0974a201610435 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BGShapeType.cs | 6 | 66f8ee725d7db251 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/BlendMode.cs | 30 | a53eb676ab5d1875 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/BlendModeEffect.cs | 543 | 134edda9a4add067 | OnRenderImage:528, Update:536 | 0 | 3 | 0 | 1 | 1 | 0 |
| Assembly-CSharp/BlendModes/ComponentExtension.cs | 254 | 96734792b179da68 |  | 3 | 8 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/BlendModes/ComponentExtensionState.cs | 38 | 71908973b1f06a8f |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/MaskableGraphicExtension.cs | 55 | 2b4400018521dd12 |  | 0 | 0 | 0 | 2 | 0 | 0 |
| Assembly-CSharp/BlendModes/MaskBehaviour.cs | 7 | 80b314460badbd4f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/MaskMode.cs | 8 | 8a674febdf244075 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/MeshRendererExtension.cs | 35 | a33ab551104956a7 |  | 0 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/MeshRendererVisualDecoExtension.cs | 15 | 1e57a5c7fcaa2607 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/RendererExtension.cs | 72 | 62e358ab5e0f0494 |  | 1 | 3 | 0 | 6 | 1 | 0 |
| Assembly-CSharp/BlendModes/RenderMode.cs | 7 | 9250564e6e1f6c0a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/ShaderProperty.cs | 129 | 626cf1ba4c988b5c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/ShaderPropertyType.cs | 9 | 71024490b5fb4446 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/ShaderResources.cs | 127 | c7a7ca847ce323b8 |  | 0 | 4 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/BlendModes/ShaderUtilities.cs | 164 | 48eda8c7312ddefb |  | 2 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/SharedMaterials.cs | 53 | c8e8cc44221f2579 |  | 2 | 2 | 0 | 1 | 0 | 0 |
| Assembly-CSharp/BlendModes/SpriteRendererExtension.cs | 37 | 4e816b5abdbe9bdc |  | 0 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/BlendModes/UIRawImageExtension.cs | 31 | ae8b24be0da7d792 |  | 0 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ButtonKeybind.cs | 29 | 4367a302651c8b3f | Update:22 | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ButtonState.cs | 7 | 67d5d229f27abf64 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedArgumentCache.cs | 124 | e3af7ff84df52ef1 |  | 3 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedBoolEvent.cs | 8 | bf4626f01b75303a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedColorEvent.cs | 9 | 68dcc4339ed1023f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedEvent.cs | 169 | 1a7cdb4854f5adbb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedEventBase.cs | 110 | 2b0b0768ae1b831f |  | 3 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedFloatEvent.cs | 8 | 81261db18c1c5656 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedIntEvent.cs | 8 | d6f171a490bd2b26 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedObjectEvent.cs | 9 | 26465180d06c1ee6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedPersistentCall.cs | 74 | c4c535084c96a16d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedPersistentCallGroup.cs | 12 | 3c52b3095e90ae09 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedStringEvent.cs | 8 | aa1d490c5390f29c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedSupportedTypes.cs | 15 | 786b93b38111f1d6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedVector2Event.cs | 9 | 95237dce638a43d8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/AdvancedVector3Event.cs | 9 | e5e7bb773a7ad643 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/DynamicArguments.cs | 54 | 3bfbc8d9245d2ada |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/EventFilterAttribute.cs | 26 | 7b6a27c16f258ae0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/GenericMenuData.cs | 17 | 59ae77025aa8976d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickAction.cs | 7 | 969e6af844de39c4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickActionGroup.cs | 143 | ea50dde66f648115 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickArgumentCache.cs | 78 | 8dadd7420170adb4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickBoolEvent.cs | 8 | 2a43c8cffeff543b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickColorEvent.cs | 9 | 461d05722d5d37a4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickEvent.cs | 295 | f7f23406750d86ea |  | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickEventBase.cs | 129 | d19b246ab057ffca |  | 3 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickFloatEvent.cs | 8 | ed0c2259779145fc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickGameObjectEvent.cs | 9 | 59ed87411315787a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickIntEvent.cs | 8 | 351ace7e7b8d04ad |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickObjectEvent.cs | 9 | 709bf57b328d9c19 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickPersistentCall.cs | 64 | c08aca101258c1e4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickPersistentCallGroup.cs | 12 | 4750bb1502bb2c47 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickStringEvent.cs | 8 | a523bcec5da39568 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickSupportedTypes.cs | 16 | 9e0d18f7c95733fa |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickTransformEvent.cs | 9 | db287ba16fdc2254 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickVector2Event.cs | 9 | fd362077dd9bdfb7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/QuickVector3Event.cs | 9 | 16260b267a5a6c0d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/TargetFilter.cs | 16 | 96d048c83e0c4d57 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ByteSheep.Events/TargetTypeFilter.cs | 21 | 0eca4d226159175a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/CalibrationPreset.cs | 118 | fb50f02c7c95bd01 |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/CameraFilterPack_3D_Anomaly.cs | 97 | 2edb7752199c3204 | OnRenderImage:57, Update:86 | 0 | 2 | 0 | 1 | 2 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Binary.cs | 100 | 8d6b2273aa0aee82 | OnRenderImage:58, Update:89 | 0 | 3 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_BlackHole.cs | 110 | a0137567027c5ee2 | OnRenderImage:55, Update:99 | 0 | 2 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Computer.cs | 96 | 9576120959fc02f4 | OnRenderImage:55, Update:85 | 0 | 3 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Distortion.cs | 114 | 9989a0ff70cb0ba5 | OnRenderImage:58, Update:103 | 0 | 2 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Fog_Smoke.cs | 118 | 6f1deedd528bbb0e | OnRenderImage:61, Update:107 | 0 | 2 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Ghost.cs | 105 | 32efb3e3134283c5 | OnRenderImage:63, Update:94 | 0 | 2 | 0 | 1 | 2 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Inverse.cs | 106 | ba81d60ec769bc78 | OnRenderImage:52, Update:95 | 0 | 2 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Matrix.cs | 96 | a13117772b0d479d | OnRenderImage:55, Update:85 | 0 | 3 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Mirror.cs | 110 | d46d25d26277eb84 | OnRenderImage:55, Update:99 | 0 | 2 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Myst.cs | 118 | 6591a39c14e89a58 | OnRenderImage:61, Update:107 | 0 | 2 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Scan_Scene.cs | 111 | c946680e3992a62d | OnRenderImage:56, Update:100 | 0 | 3 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Shield.cs | 126 | 6d1efae938cad4c5 | OnRenderImage:67, Update:115 | 0 | 2 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_3D_Snow.cs | 101 | 5c7bd26ea725baba | OnRenderImage:60, Update:90 | 0 | 2 | 0 | 1 | 2 | 2 |
| Assembly-CSharp/CameraFilterPack_AAA_Blood.cs | 84 | b72d44dfd382fd7f | OnRenderImage:49, Update:73 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_AAA_Blood_Hit.cs | 116 | 928a59dc4560ecee | OnRenderImage:73, Update:105 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_AAA_Blood_Plus.cs | 116 | 9bb1ea53508523de | OnRenderImage:73, Update:105 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_AAA_BloodOnScreen.cs | 87 | 1ee2d53c09ecf7c0 | OnRenderImage:51, Update:76 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_AAA_WaterDropPro.cs | 80 | fd6f31e46f9bb513 | OnRenderImage:46, Update:69 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Atmosphere_Fog.cs | 83 | bf99e763d742581d | OnRenderImage:47, Update:72 | 0 | 3 | 0 | 1 | 2 | 2 |
| Assembly-CSharp/CameraFilterPack_Atmosphere_Rain_Pro_3D.cs | 145 | d0206a93bf941a35 | OnRenderImage:82, Update:134 | 0 | 3 | 1 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_Blend2Camera_ColorKey.cs | 121 | 9800ca8e413781c1 | OnRenderImage:67, Update:97 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Blend2Camera_SplitScreen3D.cs | 134 | 3e13fe53a0679a3e | OnRenderImage:79, Update:112 | 0 | 2 | 0 | 1 | 2 | 2 |
| Assembly-CSharp/CameraFilterPack_Broken_Screen.cs | 72 | eda26b1765184258 | OnRenderImage:40, Update:61 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Broken_Simple.cs | 81 | ae8e86c9ec91a616 | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Broken_Spliter.cs | 73 | 637397b79844bcf9 | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Classic_ThermalVision.cs | 89 | 96ec1a2d83acf634 | OnRenderImage:52, Update:78 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Color_Adjust_Levels.cs | 80 | ceb4b0a63bf62280 | OnRenderImage:46, Update:69 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Color_Chromatic_Plus.cs | 73 | f9368d676381ceb4 | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Convert_Normal.cs | 60 | f2fa3373112880dd | OnRenderImage:35, Update:49 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Distortion_ShockWaveManual.cs | 77 | 184343cc688b0f4e | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_EXTRA_SHOWFPS.cs | 100 | 190e4d7e3c3d8cdf | OnRenderImage:52, Update:87 | 1 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Glasses_On.cs | 101 | e808011450d808f9 | OnRenderImage:61, Update:90 | 0 | 4 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Glasses_On_2.cs | 101 | 3b41fdf7faa5a736 | OnRenderImage:61, Update:90 | 0 | 4 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Glasses_On_3.cs | 101 | 6abeee61157153ca | OnRenderImage:61, Update:90 | 0 | 4 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Glasses_On_4.cs | 101 | 8108f2c909fd334a | OnRenderImage:61, Update:90 | 0 | 4 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Glasses_On_5.cs | 101 | 6a5caa73b2f8604c | OnRenderImage:61, Update:90 | 0 | 4 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Glasses_On_6.cs | 101 | 53c7c372491cabcb | OnRenderImage:61, Update:90 | 0 | 4 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Lut_2_Lut.cs | 197 | 84c59916655816fa | OnRenderImage:131, Update:186 | 6 | 7 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Lut_2_Lut_Extra.cs | 205 | 1e60ead2b048a21a | OnRenderImage:137, Update:194 | 6 | 7 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Lut_Mask.cs | 163 | fa286bc711ac3b84 | OnRenderImage:122, Update:152 | 6 | 6 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Lut_PlayWith.cs | 166 | df609fdfb8c8679f | OnRenderImage:124, Update:155 | 6 | 6 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Lut_Plus.cs | 156 | 5ccf84618a5a7d0c | OnRenderImage:117, Update:145 | 6 | 6 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Lut_Simple.cs | 150 | 89333d696ce9c88f | OnRenderImage:112, Update:139 | 6 | 6 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Lut_TestMode.cs | 170 | 5eb99f337b7f5d76 | OnRenderImage:127, Update:159 | 6 | 6 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_NewGlitch1.cs | 69 | 8587bbba2f593c80 | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_NewGlitch2.cs | 69 | 834c4f4c3c7d47f7 | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_NewGlitch3.cs | 69 | 7306d591584f25a8 | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_NewGlitch4.cs | 69 | 9eb4e2bf3ac98eb3 | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_NewGlitch5.cs | 89 | ead4ab742c5de155 | OnRenderImage:52, Update:78 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_NewGlitch6.cs | 73 | 63049f8f87257945 | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_NewGlitch7.cs | 73 | f5b1e642bce153f0 | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Pixelisation_DeepOilPaintHQ.cs | 106 | cbaf086a73cc2175 | OnRenderImage:52, Update:95 | 0 | 2 | 0 | 1 | 3 | 2 |
| Assembly-CSharp/CameraFilterPack_Pixelisation_Sweater.cs | 76 | 118e8b73a55ba712 | OnRenderImage:43, Update:65 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Rain_RainFX.cs | 105 | b1ade3d987690ee7 | OnRenderImage:50, Update:94 | 1 | 7 | 0 | 1 | 2 | 2 |
| Assembly-CSharp/CameraFilterPack_TV_MovieNoise.cs | 65 | 05dabd4713c9a8b2 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_TV_Vignetting.cs | 71 | b4b0ce12eaf2095c | OnRenderImage:43, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Vision_Blood.cs | 77 | bc115684486492e4 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Vision_Blood_Fast.cs | 77 | 4efe247b445b048b | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Vision_Hell_Blood.cs | 92 | 5c922f8f003393a5 | OnRenderImage:54, Update:81 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CameraFilterPack_Vision_SniperScore.cs | 109 | 72c7170590da6a0a | OnRenderImage:66, Update:98 | 0 | 4 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/CamMovementType.cs | 8 | cf24feadce07feb8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/CharlieAction.cs | 30 | 811bffd394411397 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ChatController.cs | 44 | 4999dd487fb47eb5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ChineseDLCLogo.cs | 11 | 1f78b610a2166aeb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/CircleLineRenderer.cs | 25 | 17493bd3f0c33b1f |  | 1 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/clsExitFolder.cs | 7 | 4ab17054cd20a4a6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/clsGem.cs | 41 | f305c66ca48ad027 | Update:34 | 0 | 0 | 3 | 0 | 1 | 0 |
| Assembly-CSharp/ColorCloud.cs | 20 | 8739ced81dc99812 |  | 0 | 0 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/ColourScheme.cs | 18 | 3ae0fc9cdc4230e9 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ColourSchemeHitMargin.cs | 71 | 3ee1821e760b560d |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ConsoleProDebug.cs | 23 | 30dc27f537aedeb7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ConstantShake.cs | 25 | be76ff1cd9948d0e | Update:19 | 0 | 1 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/ControllerIcon.cs | 56 | 240b3380331827c7 |  | 2 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ControllerIconHand.cs | 18 | af6f27d8844cb886 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ControllerIconTester.cs | 62 | 3218a05b25d375c2 | Update:19 | 4 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ControllerType.cs | 13 | cc2160340f395780 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/CoopPainter.cs | 91 | f0104cb2612e061d |  | 3 | 2 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/CR2024Island.cs | 89 | 94d81596b40f08c0 |  | 2 | 0 | 3 | 0 | 4 | 0 |
| Assembly-CSharp/Crack.cs | 136 | 164f5f5d617681f2 | Update:66 | 4 | 7 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/CrazyMinnow.AmplitudeWebGL/Amplitude.cs | 195 | dfe539f1147ade2e | LateUpdate:105 | 2 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/CrazyMinnow.AmplitudeWebGL/AmplitudeSamplesUI.cs | 124 | 776eb887a6cd55af | Update:54 | 2 | 0 | 2 | 0 | 2 | 0 |
| Assembly-CSharp/CrazyMinnow.AmplitudeWebGL/AmplitudeTester.cs | 29 | 4da098ca7b44235a | Update:12 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/CSVReader.cs | 56 | ff689641b458a3f1 |  | 5 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/CUIColorPicker.cs | 225 | 93687d557dbe4ef8 | Update:221 | 3 | 18 | 6 | 0 | 5 | 0 |
| Assembly-CSharp/CustomFloorIcon.cs | 17 | d83fca436aa0c55e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/CustomLevelTile.cs | 129 | 68151b48744fbd98 |  | 0 | 2 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/CustomStandaloneInputModule.cs | 13 | 26b9109acc4934ae |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Cutscene1.cs | 695 | d0d1d278ca03b1ad | Update:618 | 13 | 14 | 80 | 1 | 4 | 0 |
| Assembly-CSharp/Cutscene2.cs | 108 | 2a170ebe1a14bf9e | Update:99 | 4 | 5 | 9 | 2 | 0 | 0 |
| Assembly-CSharp/Cutscene3.cs | 102 | 21a95d44d145fef2 | Update:93 | 4 | 5 | 9 | 2 | 0 | 0 |
| Assembly-CSharp/Cutscene4.cs | 94 | 7fca4a65760b2772 | Update:89 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene5a.cs | 75 | 1a2e0f43b6c37f5e | Update:67 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene5b.cs | 78 | e49e64f5f226d0fa | Update:70 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene5c.cs | 78 | bf749a777e0c6ed3 | Update:70 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene5d.cs | 75 | 3edddccc3a6a7afd | Update:67 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene5e.cs | 75 | c24004bfcf33a614 | Update:67 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene6.cs | 75 | b29f372cae6380d2 | Update:67 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene7a.cs | 74 | ef26aba44a17ee29 | Update:66 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene7b.cs | 73 | 7bf9e5e70db1a1c8 | Update:65 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene7c.cs | 74 | 8afc05a43cf9bd6f | Update:66 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene7d.cs | 80 | 79348e268c18eff7 | Update:72 | 0 | 5 | 6 | 1 | 0 | 0 |
| Assembly-CSharp/Cutscene8.cs | 276 | 1e6734d7e21f64e4 | Update:263 | 0 | 6 | 18 | 1 | 0 | 0 |
| Assembly-CSharp/CycleButtons.cs | 27 | d6134be36d757a7b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DebugPoint.cs | 17 | 1e1e6911357c8b87 |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/DecorationBlendMode.cs | 10 | 6d6110da9f6fcd08 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DecorationListFilter.cs | 5 | 3738a7911bb49ccf |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DecorationsArray.cs | 24 | b50f7a1db6beae41 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DecorationType.cs | 8 | d067fc2fa1034636 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DecoTransformGizmoHolder.cs | 233 | f6a15406e702de3a | Update:26, LateUpdate:41 | 1 | 0 | 5 | 0 | 5 | 0 |
| Assembly-CSharp/DecPlacementType.cs | 11 | d3facbdcba15cf71 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DetailedResults.cs | 172 | 1d65511d699b036f | Update:141 | 1 | 14 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/dfxCircleMovement.cs | 55 | 4116cffc134fb8e7 | Update:30 | 0 | 0 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/dfxFollowPlanet.cs | 54 | 7b28d1bdbf271e71 | Update:41 | 0 | 1 | 7 | 0 | 3 | 0 |
| Assembly-CSharp/dfxRotate.cs | 14 | 85c14810eee7080f | Update:10 | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/Difficulty.cs | 6 | 345be461181167e9 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DifficultyIndicator.cs | 60 | 15b524df167838a9 | Update:24 | 2 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DisableAtAwake.cs | 7 | 893a4490d5419fc8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DiscordController.cs | 240 | 79821b1c4cd6261c | Update:166 | 2 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Divine_Stats.cs | 20 | 296cf5eb0570555f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DivineIntervention.cs | 2603 | d6a4a281c01f4368 | Update:1252 | 74 | 150 | 148 | 8 | 5 | 0 |
| Assembly-CSharp/DLCManager.cs | 99 | 6ef1efeceaa62a7d |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DOTweenExtensions.cs | 59 | 895c83e8f6d8a23f |  | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/DropdownSample.cs | 19 | b82deb61cd034ab0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EaseGraphRenderer.cs | 62 | ce96924e33e58bb6 |  | 2 | 4 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/EasePartBehavior.cs | 5 | d07d126dc7bfc479 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EditorDecorationManager.cs | 12 | d725b4aafcc965ed | Update:9 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EditorDifficultySelector.cs | 79 | c6440f380615f54a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EditorSelectTarget.cs | 5 | 239dce3f40be1620 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EditorSpeedIndicator.cs | 38 | 16e80b38642fa81b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EditorWebServices.cs | 225 | 4f0ef5588a3a3ecd |  | 3 | 25 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Ember.cs | 29 | d22631fe68ae5b47 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EmojiPaintButton.cs | 45 | eccb3d4a8e81138a | Update:19 | 1 | 0 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/EndingSparkle.cs | 188 | 93e799431bbdc8e7 | Update:170 | 4 | 12 | 2 | 1 | 1 | 0 |
| Assembly-CSharp/EndLevelType.cs | 11 | 0a0298ac2f5f3c00 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EndscreenLanterns.cs | 285 | 5a234ee188f4cd6c | Update:83, LateUpdate:93 | 4 | 5 | 24 | 3 | 5 | 0 |
| Assembly-CSharp/EntitlementsService.cs | 129 | 9fdfc15a8204e844 |  | 0 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EntranceTile.cs | 20 | 57322b93af7540a3 | LateUpdate:5 | 0 | 0 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/EnvMapAnimator.cs | 29 | 8dec8a163abde5f0 |  | 1 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ErrorCanvas.cs | 151 | 20e41303f5435f6c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ErrorMeterShape.cs | 5 | bed91cba00b4fa67 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ErrorMeterSize.cs | 8 | e31bf3ee24ad95fe |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EventCircle.cs | 17 | bf4d2f4a1d14035e | Update:12 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/EventsArray.cs | 9 | a421ea7a67611c12 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ExtensionMethods.cs | 271 | ffcc70fa1a91d671 |  | 6 | 19 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/FallingPetals.cs | 46 | dbe7dc2dc46659aa | LateUpdate:9 | 2 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/FeaturedDLCManager.cs | 36 | 3680f9d7dfccbbbe |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FeaturedLevels.cs | 245 | 9d3c62b3df9f4f43 |  | 2 | 103 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FeaturedPortal.cs | 22 | 497cb771c8f24ca8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxActivateGoat.cs | 24 | d276dde7f3af17e4 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxAddComponent.cs | 68 | f6b955ac7c37fb46 |  | 1 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ffxBgColor.cs | 14 | 96fc6408a87d086f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxBloomPlus.cs | 64 | 6cd20cd26a2ac46f |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ffxButterflyCircle.cs | 41 | dace3527c493a782 |  | 1 | 1 | 3 | 0 | 1 | 0 |
| Assembly-CSharp/ffxCallFunction.cs | 14 | c0859ae1a550c8bd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxCallFunctionPlus.cs | 23 | 1f77cdb8ee78d04d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxCallMethod.cs | 147 | 1562eb8f092709a8 |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxCameraPlus.cs | 290 | 66a8e879218b00d5 |  | 0 | 1 | 14 | 0 | 0 | 0 |
| Assembly-CSharp/ffxCamInst.cs | 9 | 06c5c4ce246a0bda |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ffxCamMove.cs | 11 | 61b62475c9d9949d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxCamMove_Follow.cs | 5 | fffef96f8f4fe606 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxCamRestore.cs | 18 | 14839b4b907f5eb9 |  | 0 | 0 | 1 | 0 | 3 | 0 |
| Assembly-CSharp/ffxCamSetParams.cs | 26 | 62215c705a72a594 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxCamShake.cs | 20 | c73afdba82906b1b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxChangeTrack.cs | 74 | 21b9ed0173789096 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ffxCheckpoint.cs | 72 | 9b6762df238a91a7 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxColdTile.cs | 65 | 784f4da006488d2b |  | 2 | 4 | 8 | 0 | 0 | 0 |
| Assembly-CSharp/ffxCustomBackgroundPlus.cs | 74 | 7caeab15c06897b8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxDamageRat.cs | 66 | cf718da46d73c528 |  | 2 | 2 | 6 | 2 | 2 | 0 |
| Assembly-CSharp/ffxFadeIn.cs | 78 | f17bc03b5f77f522 |  | 2 | 1 | 2 | 0 | 9 | 0 |
| Assembly-CSharp/ffxFadeInPlus.cs | 26 | 6edf6367d51cbc46 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxFearGrowsLightning.cs | 36 | 9ebbcd5bac7f75eb |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxFlash.cs | 47 | a7f8f445eca2bd80 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxFlashPlus.cs | 108 | d7e326daa78ee0e8 |  | 0 | 1 | 0 | 1 | 0 | 0 |
| Assembly-CSharp/ffxFlashSprite.cs | 39 | 67bd53ab95dee730 |  | 2 | 0 | 2 | 0 | 2 | 0 |
| Assembly-CSharp/ffxFlashSpritePlus.cs | 18 | 825bb90c1099a3c1 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxFlashStyle.cs | 8 | 139f68769a1d8750 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxFloorAppearPlus.cs | 175 | ff217d6375b3096a |  | 0 | 2 | 20 | 0 | 0 | 0 |
| Assembly-CSharp/ffxFloorDisappear_ZeroBehind.cs | 12 | f1f76e013ff54e84 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxFloorDisappearPlus.cs | 117 | 796b463b5abe9318 |  | 0 | 2 | 6 | 0 | 0 | 0 |
| Assembly-CSharp/ffxGlitchFilter.cs | 9 | bdfa1d14df9da148 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ffxHallOfMirrorsPlus.cs | 22 | 6ac7231ef4d2f728 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxHotTile.cs | 45 | 55e01a45b9616b54 |  | 1 | 3 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/ffxHueSpriteTween.cs | 43 | bd8f5c0ca7778856 |  | 2 | 0 | 2 | 0 | 2 | 0 |
| Assembly-CSharp/ffxJerkCam.cs | 58 | 8f915fac0e1c0610 | Update:24 | 0 | 0 | 1 | 0 | 9 | 0 |
| Assembly-CSharp/ffxKillPlayer.cs | 36 | 40e77e28ad95d739 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxLightBridge.cs | 58 | 52637ad75cd17283 |  | 2 | 2 | 0 | 1 | 2 | 0 |
| Assembly-CSharp/ffxLowQualityBlizzard.cs | 18 | ed7530843ff1f284 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxMDEnemy.cs | 176 | 843d9ca286baeb73 | Update:96 | 0 | 1 | 6 | 0 | 2 | 0 |
| Assembly-CSharp/ffxMDEnemyHit.cs | 13 | fe42988d8742a3ed |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxMDGhosts.cs | 79 | 10cfff59631c3f00 |  | 0 | 1 | 8 | 0 | 1 | 0 |
| Assembly-CSharp/ffxMDHammer.cs | 75 | fedc715fb0458839 | Update:46 | 0 | 0 | 4 | 0 | 2 | 0 |
| Assembly-CSharp/ffxMenuFoolJoker.cs | 61 | 1110a51c66f44bfe | Update:34 | 0 | 0 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/ffxMenuFoolSwirl.cs | 106 | 1cc116bc61753fae | Update:54 | 4 | 0 | 2 | 0 | 2 | 0 |
| Assembly-CSharp/ffxMenuPlanetSpeedChange.cs | 40 | b01b2e2279cd9622 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxMoveDecorationsPlus.cs | 485 | 28f2da7a9f4cc915 |  | 1 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxMoveFloorPlus.cs | 215 | 20e9403ea56f6d0d |  | 3 | 5 | 8 | 1 | 0 | 0 |
| Assembly-CSharp/ffxOptionsShapeBlink.cs | 44 | 93285b57649485e9 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxPADiamond.cs | 71 | 140265c1279e9254 |  | 0 | 3 | 4 | 0 | 3 | 0 |
| Assembly-CSharp/ffxPlaySound.cs | 105 | 940b572628663692 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxPlusBase.cs | 208 | d82c5168800c1b53 |  | 3 | 2 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/ffxPulseMag.cs | 28 | 7699aa2a94749e75 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxRatPhase.cs | 23 | 577e0c96327094aa |  | 1 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ffxRecolorFloorPlus.cs | 161 | 63fae774a5801671 |  | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxRotateObjPlus.cs | 22 | 105c5cc573fffee4 |  | 0 | 0 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/ffxScalePlanetsPlus.cs | 80 | 9cc4f27b7dd5665d |  | 2 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxScaleSpritePlus.cs | 20 | b6d5bec620420c04 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxScreenScrollPlus.cs | 38 | fd3ea17772292cc0 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ffxScreenTilePlus.cs | 72 | 6a62baa3a0e6b358 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ffxScriptEnable.cs | 25 | c3c3ddd77bbaf761 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetDefaultText.cs | 173 | 5b267fd6b0de9657 |  | 1 | 2 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/ffxSetFilterAdvancedPlus.cs | 457 | 7aeaa252e015f57c |  | 11 | 26 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ffxSetFilterPlus.cs | 233 | e60acb6892661e71 |  | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetFrameRatePlus.cs | 20 | a2166938641a499f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetHitsound.cs | 26 | 1ae152ee024e2aab |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetHoldsound.cs | 38 | 97b09f58d2a1e06b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetInputEventPlus.cs | 54 | a97e10c2de08477f |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetMenuPhase.cs | 9 | 731d169350cbc3d1 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetObjectPlus.cs | 284 | cae0e92aa2c3ed50 |  | 1 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetOffset.cs | 14 | 6065596a93523244 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetOptionsIcons.cs | 58 | bfb5227f326ef6c3 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetOptionsText.cs | 29 | 94138d0da7a84286 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSetTextPlus.cs | 54 | 32cf7c24dfb0207b |  | 2 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxShakeScreenPlus.cs | 77 | 3e8395b851143fe0 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSpawnOptionsShape.cs | 72 | b86f4ceb3ea5fbd9 |  | 0 | 0 | 5 | 0 | 1 | 0 |
| Assembly-CSharp/ffxSpeed.cs | 11 | debeb363166b9d91 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ffxSpriteMove.cs | 24 | 0a0280c2ae734f3b |  | 0 | 0 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/ffxSpriteRotate.cs | 30 | 87b160a6f84a95ce |  | 0 | 0 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/ffxSpriteScale.cs | 27 | ed7f87abef669ab2 |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/ffxTractorbeamFloors.cs | 59 | 8549101f6de86013 |  | 1 | 2 | 5 | 0 | 2 | 0 |
| Assembly-CSharp/ffxTweenBlizzardPlus.cs | 34 | 2c0f19aa9aba9f8a |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/Filter.cs | 44 | d06816bfa1943aaf |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FilterPlane.cs | 5 | 0b87c90156743338 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FilterTargetType.cs | 5 | e718e0dd3b9dde20 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FindFloorType.cs | 5 | b8fa91c1b7c2035b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Fizzd.Scripts.UnityExtension/AdditionalKeyCode.cs | 8 | a486be4d886542a7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FlashPlane.cs | 5 | 4ac0bd769ca8934e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FloorDecorationColorType.cs | 9 | cfcbff6b997ba7f0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FloorDecorationType.cs | 5 | e7760421a4fd5325 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FloorDirectionButtonType.cs | 29 | daad3e6b87abe0f7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FloorIcon.cs | 28 | c45db1ff4092c8e5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FloorMesh.cs | 1228 | a12fd8f56e8f5973 |  | 35 | 123 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/FloorMeshOld.cs | 71 | 63b8145f804c8463 | Update:31 | 1 | 0 | 7 | 1 | 0 | 0 |
| Assembly-CSharp/FloorMeshRenderer.cs | 52 | 8907d47a46e02b5b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FloorRenderer.cs | 58 | a9bbf19222d84261 |  | 0 | 0 | 0 | 3 | 1 | 0 |
| Assembly-CSharp/FloorSpriteRenderer.cs | 37 | 023d4900cef555c5 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/FloorType.cs | 5 | 0707b03cbd15a4f7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FlyingWormConsole3/ConsoleProRemoteServer.cs | 15 | dfaa5c91fb8e0e03 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FontData.cs | 15 | 67c6fa11dba2d513 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FontName.cs | 10 | c31a57050cec201c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FreeroamArea.cs | 71 | bf64ff9ca5abd5c7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/FreeroamShape.cs | 4 | ce43ddb8062063d0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/GameServices.cs | 280 | 8242b4b2520cf9a3 |  | 1 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/GameServicesEmpty.cs | 34 | 024b9967d9d7ef21 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/GameSound.cs | 5 | 5c40cbec9a924be8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/GCNS.cs | 163 | ab435407afae7d00 |  | 0 | 38 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/GCS.cs | 509 | f17fdb99257931b8 |  | 0 | 15 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/GemElevator.cs | 12 | 439c3db6470da4b4 | Update:9 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/GeneralPauseButton.cs | 19 | 1841ac0565c18c60 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/GlitchText.cs | 56 | 4c739070f9a4a90c | Update:17 | 2 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/GOExtensions.cs | 63 | 19d5b36013ee0edc |  | 0 | 0 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/GPGSIds.cs | 96 | 3962c7fb4cdf7bad |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HifiEnabler.cs | 37 | add36bfffeab4398 |  | 2 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Hitbox.cs | 6 | 3472ac8ea3c99107 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitboxDetectTarget.cs | 5 | 8da83568a2754da8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitboxTargetPlanet.cs | 6 | dfe95be695eef4fb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitboxTriggerType.cs | 6 | 4279e5c9fc86a8b6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitboxType.cs | 6 | 4791e70e8a9c2329 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitMargin.cs | 19 | 4ac71666e3ff42cd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitMarginColorPreset.cs | 6 | 6083d892be30c180 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitMarginGeneral.cs | 7 | fb7eb1eeae481327 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitMarginHelper.cs | 205 | 486e203a8e6dcc19 |  | 0 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitMarginHidePreset.cs | 9 | 2e7b09bdb9576fd5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitMarginLimit.cs | 6 | 8ae2c6eb75099379 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitMarginPerfectTextPreset.cs | 9 | 84b92dffac58ca25 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitMarginPerfectTickPreset.cs | 6 | ca531d4f6edc4f3b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitMarginTextPreset.cs | 10 | 6abc3812ebc67735 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitSound.cs | 31 | dfa495d62d90214f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitSoundOffset.cs | 9 | f2db7113add85fa4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HitSoundPreviewPlayer.cs | 41 | d6b45cfca15ca496 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/Hitstop.cs | 52 | 5054eaad7395046b |  | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HoldBehavior.cs | 6 | 71038c2493021faa |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HoldEndSound.cs | 5 | 797da58514b00b48 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HoldLoopSound.cs | 5 | 051569816972474e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HoldMidSound.cs | 6 | 0f356b7c077f6ee5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HoldMidSoundTimingRelativeTo.cs | 5 | 72e76e4ecd8badcb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HoldMidSoundType.cs | 5 | 5f6a1dcd323bb823 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/HoldStartSound.cs | 5 | 2b1b29e2b12b5506 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/IAPManager.cs | 563 | 60ce179300947e37 | Update:73 | 0 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/IAPState.cs | 8 | 0661e9fb470ca0aa |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ImportLevel.cs | 58 | 73f0631ea878fa63 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ImportLevelsCLS.cs | 939 | b9f023d92b208de0 | Update:217 | 12 | 14 | 5 | 0 | 7 | 0 |
| Assembly-CSharp/InputAction.cs | 22 | 77fc79f142e5444c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/InputEventState.cs | 5 | 037b8fd29f99ad20 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/InputEventTarget.cs | 11 | abba50fb755c0e24 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/IntroCLS.cs | 84 | 5f7df208a3b9cfdb | Update:48 | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/KeysSetting.cs | 160 | 4bca420311006c45 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Kickback.cs | 87 | 1084b2d646c16b77 |  | 1 | 1 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/KillSpriteAfterTime.cs | 31 | e83ab0bab39ef98e | Update:15 | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/LaNuitScenery.cs | 19 | 061d7aceb01f74ef |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/LaserSpawner.cs | 237 | d3e0a4e2087df517 | Update:135 | 9 | 3 | 13 | 0 | 4 | 0 |
| Assembly-CSharp/LegacyRandomRotator3D.cs | 79 | 18530faab36fbd6e |  | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/Level.cs | 55 | 52d39c24796d68ff |  | 2 | 0 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/LevelImporterInfoSection.cs | 65 | b8265933f7c29c43 |  | 2 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/LevelML.cs | 45 | 0b3a4c21252c41bf |  | 0 | 0 | 0 | 0 | 7 | 0 |
| Assembly-CSharp/LevelRadar.cs | 226 | a7aa8a85608476e2 | Update:90 | 12 | 7 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/LevelSelectBase.cs | 36 | 90866e8366c144ab |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/LevelSelectCutsceneController.cs | 157 | 93a8b9fd5c0c13bb |  | 3 | 0 | 14 | 1 | 1 | 0 |
| Assembly-CSharp/LevelSelectIsland.cs | 173 | 7a1b7d10704e492c |  | 6 | 4 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/LevelSource.cs | 6 | e8c2ba925059bc40 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/LevelTNO.cs | 26 | 1645cfdce88dba35 |  | 1 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/LevelValidation.cs | 3 | 008cc378a4a0b4ad |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/LinkOpener.cs | 18 | 702862dc6ada2b56 |  | 0 | 0 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/ListItemPool.cs | 57 | 69022e4e79c7a585 |  | 0 | 2 | 6 | 0 | 1 | 0 |
| Assembly-CSharp/MacUtils.cs | 29 | 2556846afee1d5c1 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MaskingType.cs | 7 | 0f969d047f389f69 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MaskLightGradient.cs | 26 | 2d5ccc1d60bc44ef | Update:21 | 0 | 0 | 1 | 0 | 3 | 0 |
| Assembly-CSharp/Mawaru.cs | 4524 | 9cf40d7dacfe26ae | Update:829 | 104 | 295 | 476 | 61 | 8 | 0 |
| Assembly-CSharp/Mawaru2_Stats.cs | 39 | 128976a912c3658f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_Arm.cs | 42 | dbc774b6796a08e8 |  | 0 | 0 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_Bar.cs | 18 | 4d95a6ef5495ff07 |  | 0 | 0 | 0 | 2 | 0 | 0 |
| Assembly-CSharp/Mawaru_Castle.cs | 89 | 4d68eadd85ffcd06 |  | 0 | 2 | 9 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_Charlie.cs | 201 | db5b7637f1937a6a | Update:173 | 2 | 5 | 22 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_Coin.cs | 19 | 6ae1d93a30c2be17 |  | 0 | 0 | 3 | 1 | 0 | 0 |
| Assembly-CSharp/Mawaru_Goat.cs | 83 | 72df08cef7dcfaa6 | Update:74 | 2 | 2 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_Medal.cs | 8 | 01398908b493aac6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_Mesh.cs | 13 | 5b97d14c64a8e12e |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/Mawaru_Oskari.cs | 103 | 8d650f50f65227c7 | Update:84 | 0 | 6 | 20 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_Rock.cs | 111 | b962ef03f215a5fc |  | 0 | 28 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_Soccerball.cs | 11 | e54b0841e3568a40 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_Sprite.cs | 174 | 399296c101df9151 | OnBecameVisible:44, OnBecameInvisible:52, OnGUI:60, Update:92, LateUpdate:167 | 0 | 3 | 1 | 1 | 2 | 0 |
| Assembly-CSharp/Mawaru_Stats.cs | 36 | 5211707c196f87b4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Mawaru_WarpTile.cs | 30 | 4618d274941bad4b |  | 0 | 2 | 2 | 4 | 0 | 0 |
| Assembly-CSharp/MedalLayout.cs | 47 | 40498514861cce46 |  | 3 | 5 | 5 | 0 | 2 | 0 |
| Assembly-CSharp/MenuArrow.cs | 46 | dfc9f2b9fe1cadab |  | 0 | 2 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/MenuMedal.cs | 43 | 686f21c7a770353e | Update:21 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MeshBox.cs | 12 | a426bc105e1f05cf | Update:9 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MeshPoint.cs | 8 | db2da471226880d3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MeshTestBG.cs | 141 | a86eb9c891ec7a02 |  | 6 | 13 | 2 | 0 | 2 | 0 |
| Assembly-CSharp/MixerGroup.cs | 10 | a613961a0e296ddf |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/ArrowAppearance.cs | 10 | 9d5f65f33d297c4b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/CR2024TechSecret.cs | 76 | dcd26a23ef5cfec3 |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/IntroPhase.cs | 11 | 43eabd8a388d6143 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/IntroType.cs | 9 | 10094ce153742144 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuArrow.cs | 102 | 0cce5f4ea5ac3169 |  | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuBlankScreen.cs | 12 | 5650710c60e56c6f |  | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuColorScreen.cs | 12 | 5425d3f6c1d00f12 |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuController.cs | 955 | 82fbed6197fb6fc7 | Update:503 | 10 | 14 | 11 | 0 | 5 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuCoopIntro.cs | 558 | 8dae9ba9070c27da | Update:94 | 23 | 5 | 35 | 0 | 4 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuCreditsPuzzle.cs | 186 | 64101d0fc2238da7 | Update:84 | 0 | 10 | 11 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuCreditsScreen.cs | 40 | 59744ef5f1466682 |  | 0 | 0 | 4 | 0 | 3 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuDebug.cs | 156 | ce87466df594a2c2 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuDescriptionScreen.cs | 25 | 108fbf665a5ecc82 |  | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuDLCTransitionPortal.cs | 188 | b5afa56266211b13 | Update:124, LateUpdate:143 | 2 | 0 | 11 | 3 | 7 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuDLCTransitionScreen.cs | 30 | f9eddd4c95d06d91 |  | 0 | 0 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuFeaturedPortalScreen.cs | 67 | 0314b74a0a71eb8a |  | 0 | 0 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuGalleryScreen.cs | 31 | a31229b712d4c3e7 |  | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuGrabbable.cs | 42 | a3002a261f11e943 |  | 0 | 0 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuGrabbableMine.cs | 51 | b5dd341db58e3287 | Update:34 | 0 | 0 | 3 | 0 | 2 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuGrabbablePig.cs | 10 | c68a6423b4259872 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuGrabbablePlanet.cs | 13 | 103f6e3009da61b3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuGrabbableRose.cs | 5 | 2d39f4ae55a2e06c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuGrabController.cs | 71 | 3f1cf44d5c563c6d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuGroup.cs | 137 | 5f909cfcad134076 |  | 2 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuHare.cs | 51 | fb17ca1be7a6c41e | Update:22 | 0 | 0 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuMap.cs | 263 | ac7ff2f2c90a3bae |  | 12 | 8 | 8 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuMinesweeperPuzzle.cs | 46 | 3d1c3ed2aed4c59d | Update:11 | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuMorePage.cs | 85 | 93687ccc1bfc2daf |  | 0 | 1 | 6 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuMoreScreen.cs | 17 | f30014b03da76b71 |  | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuNeoCosmosBuyPage.cs | 484 | f31eb13990d22939 | Update:376 | 4 | 7 | 7 | 1 | 1 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuPigPuzzle.cs | 132 | b0d488965c90f704 | Update:60 | 2 | 3 | 11 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuPortalScreen.cs | 106 | b0235b18a5ef49d3 |  | 1 | 1 | 6 | 0 | 2 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuReviewPrompt.cs | 91 | 05641518d45f14f3 | LateUpdate:59 | 0 | 2 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuRosePuzzle.cs | 100 | ed0b9407f93e26a2 |  | 1 | 3 | 4 | 0 | 2 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuScreen.cs | 109 | f719cbd016dca478 |  | 0 | 13 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuSublevelBrowserGenerator.cs | 128 | c46a510b0db1c987 |  | 1 | 2 | 9 | 0 | 4 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuTaroRiftScreen.cs | 19 | f251e395034a88e8 |  | 0 | 0 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuTitle.cs | 97 | ddf0f79b3abc770f |  | 1 | 1 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuTitleScreen.cs | 43 | 500573a12ed073b7 |  | 0 | 1 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuTreePuzzle.cs | 60 | e306ab43efb2c927 |  | 0 | 1 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileMenuTutorial_Switch.cs | 45 | c59c3866503a7c14 | Update:28 | 0 | 0 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MobileTrailerPlayer.cs | 164 | 322af58d1f13d68b | Update:150 | 0 | 2 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/MobileMenu/MoveDirection.cs | 9 | 10ccadb8834702f5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/MoveDirectionHelper.cs | 26 | 24870726c9b27f7c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/PlanetColorSwapper.cs | 305 | 507c5c2414094ba6 | Update:200 | 5 | 10 | 9 | 0 | 2 | 0 |
| Assembly-CSharp/MobileMenu/PuzzleRoomsScreen.cs | 32 | e00b9f737307cfbb |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/MobileMenu/scnMobileMenu.cs | 152 | f6bae993654d7203 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MonsterChase.cs | 74 | 9e7c7de2ff1ced44 | Update:68 | 3 | 6 | 7 | 0 | 0 | 0 |
| Assembly-CSharp/MonsterLove.StateMachine/StateBehaviour.cs | 43 | 711975ea0c9cb01b |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/MonsterLove.StateMachine/StateEngine.cs | 270 | 10682d39d2697bfe | FixedUpdate:221, Update:229, LateUpdate:237 | 4 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MonsterLove.StateMachine/StateMapping.cs | 26 | d80f1df12dbb6a01 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MonsterLove.StateMachine/StateTransition.cs | 7 | 1d01f758a85e9c53 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/MultitapTileBehavior.cs | 5 | 80a2c4ca52cf7571 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/NamedColor.cs | 10 | caec9299d44ab6bd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/NeoCosmosManager.cs | 45 | 1481fd3fc2fad802 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/NewBestType.cs | 7 | 27bf24f69fe3923d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/NewLife.cs | 828 | 86cc8fdf508a6aee | Update:440 | 12 | 35 | 30 | 27 | 1 | 0 |
| Assembly-CSharp/NewLife2_Stats.cs | 17 | a354282ca89ad1f4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/NewLife_Stats.cs | 17 | 4ce385d2bbf541ef |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/NewsSign.cs | 230 | fd8d2b040854ba78 | Update:156 | 1 | 4 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/Notification.cs | 174 | 52409d64550552bb |  | 0 | 10 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/NXManager.cs | 30 | 6097936b33a6e1cb |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/NXManager_Editor.cs | 52 | de09228242a8131f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/NXManagerState.cs | 6 | b6cdb618895fa83a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/OBBCollision.cs | 70 | 94ba4af25713c273 |  | 3 | 9 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ObjectDecorationTweenType.cs | 10 | c004cdff87d49661 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ObjectDecorationType.cs | 6 | c1f5e4402951a06c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/OptionsPanelsCLS.cs | 655 | ef53299b337d9935 | LateUpdate:346 | 7 | 7 | 0 | 0 | 6 | 0 |
| Assembly-CSharp/OptionsShape.cs | 7 | 2206151ea621d6bf |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/OttoButtonController.cs | 34 | 2a985ff9623432b3 | Update:16 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/OverseerIdle.cs | 121 | a07e04ac914cf37d | Update:117 | 7 | 6 | 3 | 0 | 1 | 0 |
| Assembly-CSharp/PackageInstallerResult.cs | 24 | 8db38166f106c8d6 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PaintingStretch.cs | 16 | daf83a35d531740c |  | 0 | 0 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/PanelTransformGizmoHolder.cs | 126 | 33fa6379a7c021e0 | Update:61, LateUpdate:70 | 0 | 2 | 3 | 0 | 3 | 0 |
| Assembly-CSharp/ParticlePlayMode.cs | 6 | 18f1e826ea9b7373 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ParticleShape.cs | 5 | 89e84d3a63539be6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ParticleSimulationSpace.cs | 5 | bb23f96b1c9f690e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PathGenerator2.cs | 131 | 8ad205d8340df471 | Update:40 | 5 | 8 | 3 | 0 | 2 | 0 |
| Assembly-CSharp/PathGeneratorOld.cs | 111 | 2f90f56239599b1b | Update:48 | 3 | 4 | 4 | 1 | 2 | 0 |
| Assembly-CSharp/PathMaker.cs | 12 | b736f9d39dc6666d | Update:9 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PauseButton.cs | 127 | eac6dc061622f4b0 | LateUpdate:115 | 0 | 0 | 2 | 0 | 2 | 0 |
| Assembly-CSharp/PauseLevel.cs | 776 | 4afc329ec89fcc64 | Update:690 | 6 | 38 | 15 | 22 | 12 | 0 |
| Assembly-CSharp/PauseLevelButton.cs | 69 | 115beed3de50a5f7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PauseMedals.cs | 228 | ee882da74270b2ce | Update:59 | 6 | 6 | 12 | 0 | 5 | 0 |
| Assembly-CSharp/PauseMenu.cs | 1750 | 767023df5de2586e | OnGUI:716, Update:738, LateUpdate:816 | 10 | 23 | 10 | 1 | 7 | 0 |
| Assembly-CSharp/PauseMenuChain.cs | 142 | 33cc8dbad6bfa2cc | Update:57, LateUpdate:82 | 4 | 5 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/PauseMenuChainLink.cs | 16 | 0b065b2357be1088 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PausePlanets.cs | 167 | 61cd431a7a8000dc | Update:81, LateUpdate:90 | 0 | 2 | 5 | 0 | 1 | 0 |
| Assembly-CSharp/PauseSettingButton.cs | 196 | e338edbab97dadb8 |  | 0 | 4 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/Persistence.cs | 2779 | 26eff4818aedad4c |  | 33 | 19 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PlanetarySystem.cs | 551 | 43cfbf7f7d06b0e9 |  | 17 | 30 | 6 | 0 | 2 | 0 |
| Assembly-CSharp/PlanetColor.cs | 45 | 52dff7e474f4f2fc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PlanetColorPreset.cs | 43 | d3a7a2eb3786200b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PlanetCount.cs | 5 | 0f0fb129122b8755 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PlanetDecorationColorType.cs | 8 | 68db44224c56eb07 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PlanetRenderer.cs | 578 | d582a3e3a27c3f1c | LateUpdate:89 | 1 | 35 | 4 | 0 | 16 | 0 |
| Assembly-CSharp/PlanetSprite.cs | 86 | 678d9e4626af185c | LateUpdate:82 | 0 | 0 | 0 | 5 | 1 | 0 |
| Assembly-CSharp/Platform.cs | 12 | a0123f082acbd86e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PlayerBubble.cs | 147 | 70192acfde048dbc | Update:117 | 0 | 1 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/PlayerPrefsJson.cs | 457 | 2cf04ce5aacd3ddf |  | 2 | 10 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PlayerSelect.cs | 361 | f4f9403188b791f2 | OnGUI:300 | 6 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PlayerSelectButton.cs | 205 | 55dc0f78efb61f58 | LateUpdate:117 | 4 | 3 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/PolarScreen.cs | 52 | c7795dc1ed1119bd | OnRenderImage:28, Update:41 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/PolygonInfo.cs | 14 | bd3f2dc48088839c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Portal.cs | 26 | 2389a82fa2780384 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PortalCredit.cs | 71 | 8638378a969783fd |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/PortalCreditData.cs | 66 | 01485b27009f023b |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PortalQuad.cs | 49 | 86dca913086364de |  | 0 | 0 | 1 | 1 | 1 | 0 |
| Assembly-CSharp/PortalSign.cs | 91 | 147b57ffe3bf8c87 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PositionState.cs | 28 | 2b4c7adb08317852 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PostEffectsBase.cs | 214 | c30999ed340f97e1 |  | 1 | 2 | 0 | 2 | 1 | 0 |
| Assembly-CSharp/PracticeTimeline.cs | 614 | f3746f52d03737fc | Update:230 | 14 | 13 | 1 | 2 | 1 | 0 |
| Assembly-CSharp/PreviewSongPlayer.cs | 114 | 42d095cb02cb9b2a | Update:46 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/Properties/AssemblyInfo.cs | 16 | b7fb72761c31c961 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PropertyInput.cs | 5 | 26bfbb2af2140cd1 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PublicPrefabType.cs | 11 | c301f1ee7440bba4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/PublishWindow.cs | 329 | 118b9d78743ca545 |  | 0 | 4 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/PuzzleRoom.cs | 115 | 8450bbfe97a1d125 | Update:65 | 5 | 3 | 9 | 0 | 3 | 0 |
| Assembly-CSharp/RandomMode.cs | 6 | badbc0016063901d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RandomRotator3D.cs | 25 | 4e31c06ce7a50d97 | Update:20 | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/RDC.cs | 288 | 8da801e8bb0f5aea |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDCheatCode.cs | 81 | bdd7ddfa7fc381a4 |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDConstants.cs | 532 | 1ae73c454d3dcfd3 |  | 2 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDDirectory.cs | 25 | 058d1f41500832df |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDDirectory_Default.cs | 42 | b98967cf8d5e8772 |  | 2 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDDisableOnPlatform.cs | 18 | 61f2c6099c96c41f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDEditorUtils.cs | 737 | 2e5a2ba5144e7f02 |  | 10 | 29 | 5 | 0 | 7 | 0 |
| Assembly-CSharp/RDExtensions.cs | 211 | 098603300d6e9e70 |  | 1 | 26 | 19 | 0 | 0 | 1 |
| Assembly-CSharp/RDFile.cs | 86 | 5c23295b88c8c838 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDFile_Default.cs | 89 | 513d46f011b26cc2 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDInput.cs | 372 | a218dfb45ee8edf3 |  | 10 | 21 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDInputType.cs | 173 | 5c012413415fd98e | Update:127 | 0 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDInputType_AsyncKeyboard.cs | 482 | d23ef6047928508b |  | 3 | 26 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/RDInputType_Joystick.cs | 214 | 40baf29841a5f6dd |  | 3 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDInputType_Keyboard.cs | 400 | e111d60183dbc855 |  | 2 | 11 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDInputType_Mouse.cs | 124 | 949eda0c0bb2a3f4 |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDMP3Stream.cs | 99 | efbfcd03afc454ab |  | 3 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDNote.cs | 6 | bf43b736edef11ed |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RDString.cs | 477 | 7406e1d36ad242c5 |  | 7 | 8 | 0 | 1 | 1 | 0 |
| Assembly-CSharp/RDStringToUIText.cs | 73 | c8b50c4d0f55ba5c |  | 0 | 0 | 0 | 1 | 3 | 0 |
| Assembly-CSharp/RDUtils.cs | 1307 | b2647db4c3824adc |  | 11 | 30 | 2 | 0 | 4 | 0 |
| Assembly-CSharp/RedeemCode.cs | 105 | 254305a9c0c04ce1 | Update:36 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Releases.cs | 8 | 67e10bbcf88aba0b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RepeatType.cs | 5 | 88c3e81b2f1d86cc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/FlightPedalsTemplate.cs | 25 | 9926c2648e2084fd |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/FlightYokeTemplate.cs | 265 | f9c0c41183b2f84c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/GamepadTemplate.cs | 149 | 511b733f35dae568 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/HOTASTemplate.cs | 525 | 288dbdb2ed8d0d2f |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/IFlightPedalsTemplate.cs | 10 | a3884a840b5b03f4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/IFlightYokeTemplate.cs | 102 | ddc4e43455455a12 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/IGamepadTemplate.cs | 62 | b80e223ac1c9a249 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/IHOTASTemplate.cs | 180 | 49a57274e9ff9b83 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/InputManager.cs | 81 | 5e5ce84c1debef33 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/IRacingWheelTemplate.cs | 88 | 615af95738da1533 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/ISixDofControllerTemplate.cs | 86 | 2cadecfe4d29826e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/RacingWheelTemplate.cs | 189 | 694b21435e2fc06e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired/SixDofControllerTemplate.cs | 229 | 824bfc5bf1eff8b6 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Data/UserDataStore_File.cs | 392 | e7403f86cb4554bd |  | 0 | 10 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Data/UserDataStore_KeyValue.cs | 1786 | e30f531effa72079 |  | 46 | 32 | 0 | 0 | 5 | 0 |
| Assembly-CSharp/Rewired.Data/UserDataStore_PlayerPrefs.cs | 1865 | 74f4ce3850d5e68f |  | 46 | 32 | 0 | 0 | 5 | 0 |
| Assembly-CSharp/Rewired.Demos/Bullet.cs | 30 | 14eff0f17e07343e | Update:23 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos/ControlMapperDemoMessage.cs | 54 | 5be8209f414c7007 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos/ControlRemappingDemo1.cs | 1793 | 890d356eeb654e7e | Update:193, OnGUI:613 | 16 | 29 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos/CustomControllerDemo.cs | 144 | cb8e7715e487acc0 | Update:69 | 4 | 2 | 2 | 0 | 2 | 0 |
| Assembly-CSharp/Rewired.Demos/CustomControllerDemo_Player.cs | 60 | a5c560963cec0e01 | Update:38 | 0 | 4 | 3 | 2 | 3 | 0 |
| Assembly-CSharp/Rewired.Demos/CustomControllersTiltDemo.cs | 47 | 26b6b1a124c496dc | Update:24 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos/DualShock4SpecialFeaturesExample.cs | 219 | 0e6ab8b4d2ed6b29 | Update:42, OnGUI:86 | 4 | 8 | 5 | 2 | 3 | 0 |
| Assembly-CSharp/Rewired.Demos/EightPlayersExample_Player.cs | 71 | 61e6e8eddc6b3106 | Update:40 | 0 | 0 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/Rewired.Demos/FallbackJoystickIdentificationDemo.cs | 114 | cc9cc85618c0eff6 | OnGUI:63 | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos/PlayerMouseSpriteExample.cs | 116 | e5a5b44eeb385b7f | Update:74 | 0 | 8 | 5 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.Demos/PlayerPointerEventHandlerExample.cs | 138 | 74be956484afc384 | Update:28 | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos/PressAnyButtonToJoinExample_Assigner.cs | 57 | d3e95291faf1a945 | Update:9 | 2 | 0 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/Rewired.Demos/PressAnyButtonToJoinExample_GamePlayer.cs | 67 | 8634030bf8e6fc98 | Update:40 | 0 | 0 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/Rewired.Demos/PressStartToJoinExample_Assigner.cs | 87 | e20a133391f2305e | Update:57 | 2 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos/PressStartToJoinExample_GamePlayer.cs | 57 | 9e53c49e4b2b6fd9 | Update:30 | 0 | 0 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/Rewired.Demos/SimpleCombinedKeyboardMouseRemapping.cs | 225 | 1048370434586378 |  | 7 | 8 | 4 | 0 | 3 | 0 |
| Assembly-CSharp/Rewired.Demos/SimpleControlRemapping.cs | 257 | 1d3f9fda475294a8 |  | 6 | 5 | 4 | 0 | 3 | 0 |
| Assembly-CSharp/Rewired.Demos/TouchButtonExample.cs | 52 | 674bf2a1b6981d50 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos/TouchControls1_ManipulateCube.cs | 127 | c2ebe6cb9b2741d0 |  | 0 | 5 | 3 | 2 | 2 | 0 |
| Assembly-CSharp/Rewired.Demos/TouchJoystickExample.cs | 110 | f3653e9e9c2849d3 | Update:41 | 0 | 3 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos/UIPointer.cs | 98 | 5046deb0a2f65e98 | Update:59 | 1 | 1 | 8 | 0 | 2 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/CustomPlatformHardwareJoystickMapPlatformDataSet.cs | 12 | 267eae049aadfd38 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/CustomPlatformHardwareJoystickMapProvider.cs | 60 | cdf27c72995c79fa |  | 2 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/CustomPlatformManager.cs | 27 | 88733f97750ab9ef |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/CustomPlatformType.cs | 6 | ecd03e27d64e71c7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/MyPlatformControllerExtension.cs | 64 | 3ab15d24c7f8bc8f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/MyPlatformControllerIdentifier.cs | 8 | b85556ab69fd63d5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/MyPlatformHardwareJoystickMapPlatformMap.cs | 75 | bf1f13fcfaec5db7 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/MyPlatformInputSource.cs | 162 | c77d1af0eba7796b | Update:30, Update:97 | 6 | 8 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/MyPlatformUnifiedKeyboardSource.cs | 48 | 99d01e81b84457e6 | Update:41 | 1 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/MyPlatformUnifiedMouseSource.cs | 23 | 7a7a646344f5f475 | Update:10 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/UnityInputJoystickSource.cs | 222 | ae43bea87268ae80 | Update:125 | 4 | 9 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.CustomPlatform/VibrationTest.cs | 50 | 0acae1c361aa54a5 | Update:20 | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Demos.GamepadTemplateUI/ControllerUIEffect.cs | 57 | 08b27fde41953fc0 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.Demos.GamepadTemplateUI/ControllerUIElement.cs | 186 | 5359f27811c8ae6c |  | 4 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.Demos.GamepadTemplateUI/GamepadTemplateUI.cs | 417 | 8bba5b02fdc95b8e | Update:244 | 10 | 31 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/ActionElementMapPair.cs | 64 | 59fdab7bb2b828ca |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/ControllerElementGlyph.cs | 74 | 6a71728c6f917661 | Update:52 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/ControllerElementGlyphBase.cs | 405 | b82a5226bcb25e10 | Update:165 | 6 | 3 | 9 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.Glyphs/ControllerElementGlyphSelectorOptions.cs | 359 | e59c887a0676473a |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/ControllerElementGlyphSelectorOptionsSO.cs | 13 | 758e971a2e75b233 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/ControllerElementGlyphSelectorOptionsSOBase.cs | 10 | 5451780ca75595fd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/DefaultControllerElementGlyphSettingsBase.cs | 62 | bfe93e22bc4807ea |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/GlyphOrTextBase.cs | 133 | d8556da95a39f655 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/GlyphProvider.cs | 170 | 3ca6b109b74c0436 | Update:75 | 4 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/GlyphSet.cs | 73 | a1b8e9db6f34a655 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/GlyphSetCollection.cs | 100 | 77d111b308fd0050 |  | 3 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/GlyphTools.cs | 2607 | bc6ba322678b9b30 |  | 49 | 58 | 0 | 0 | 71 | 0 |
| Assembly-CSharp/Rewired.Glyphs/Pair.cs | 63 | ef95310c7b5c974e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs/SpriteGlyphSet.cs | 55 | 1fd25af4f0870e61 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUIControllerElementGlyph.cs | 12 | 384c71c42ce64693 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUIControllerElementGlyphBase.cs | 81 | e2948dbb7f48698f |  | 0 | 3 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUIDefaultControllerElementGlyphSettings.cs | 12 | 7c211a7296b2f1af |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUIDefaultGlyphOrTextTMProPrefabProvider.cs | 45 | ce9bd5bf3c17cfdd |  | 0 | 3 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUIGlyphOrText.cs | 45 | 21b74dad2ad0f2cc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUIGlyphOrTextTMPro.cs | 46 | 0151cad1cc00ba99 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUIPlayerControllerElementGlyph.cs | 176 | a1467ef3819f2ca2 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUIPlayerControllerElementGlyphBase.cs | 589 | 2921d506aad66f47 | Update:207 | 1 | 9 | 6 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Glyphs.UnityUI/UnityUITextMeshProGlyphHelper.cs | 3018 | 50c03d2019e65a97 | Update:2183 | 24 | 93 | 1 | 16 | 2 | 0 |
| Assembly-CSharp/Rewired.Integration.UnityUI/PlayerPointerEventData.cs | 41 | a9b4a3222ce3e547 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Integration.UnityUI/PointerEventType.cs | 7 | 32c28c0d9b095a22 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Integration.UnityUI/RewiredEventSystem.cs | 49 | 76c8b5032c12a60d | Update:25 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Integration.UnityUI/RewiredPointerInputModule.cs | 781 | 203cae52629047cc |  | 18 | 20 | 9 | 0 | 2 | 0 |
| Assembly-CSharp/Rewired.Integration.UnityUI/RewiredStandaloneInputModule.cs | 1410 | 6af6b1a2ef5e6796 |  | 19 | 13 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Internal/ControllerTemplateFactory.cs | 59 | 59ff60f998bc15a5 |  | 0 | 8 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Localization/LocalizedStringProvider.cs | 80 | 963dd00bb854dc23 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.Localization/LocalizedStringProviderBase.cs | 86 | ab462d2cb6119d44 | Update:49 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/ButtonInfo.cs | 8 | 47843c7d3ff43857 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/CalibrationWindow.cs | 644 | 75dcf738b60686ff | Update:280 | 5 | 12 | 3 | 0 | 6 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/CanvasScalerExt.cs | 13 | 12232d0dced47b58 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/CanvasScalerFitter.cs | 72 | a352481f4355155f | Update:42 | 1 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/ControlMapper.cs | 6674 | 0b757289ca0fb1fb | Update:3536 | 84 | 93 | 20 | 0 | 32 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/CustomButton.cs | 319 | 41620162c8989100 |  | 1 | 0 | 4 | 0 | 12 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/CustomSlider.cs | 270 | 8716cc68a8df94da |  | 0 | 0 | 4 | 0 | 12 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/CustomToggle.cs | 270 | a4b68af0931d7bf9 |  | 0 | 0 | 4 | 0 | 12 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/ICustomSelectable.cs | 24 | 0deca7b71c586c15 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/InputBehaviorWindow.cs | 341 | 9bd90074bd68dc6d |  | 5 | 5 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/InputFieldInfo.cs | 52 | 1905911f21738324 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/InputRow.cs | 33 | c0ad9a1b7007a1f4 |  | 0 | 0 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/LanguageData.cs | 610 | cf74af75e64a2eae |  | 1 | 13 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/LanguageDataBase.cs | 148 | 3b650e3caaacf349 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/ScrollbarVisibilityHelper.cs | 10 | ce346642870c5fcc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/ScrollRectSelectableChild.cs | 55 | 9937efdb30e194dd |  | 0 | 0 | 3 | 0 | 2 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/ThemedElement.cs | 45 | a3a084b1a669e87b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/ThemeSettings.cs | 872 | 87d1c9fab897a06f |  | 1 | 3 | 3 | 1 | 4 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/ToggleInfo.cs | 8 | c7518a6af912565d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/UIControl.cs | 55 | 093ba5ba1afc0075 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/UIControlSet.cs | 69 | f42e737d5427b731 |  | 0 | 1 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/UIElementInfo.cs | 26 | ee85bdd5653c9cb4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/UIGroup.cs | 43 | 5a7e74b8fb3bce8c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/UIImageHelper.cs | 71 | 4bcf26e1b5ae1994 |  | 0 | 0 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/UISelectionUtility.cs | 82 | 0edcec8ad6970dc9 |  | 1 | 2 | 5 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/UISliderControl.cs | 85 | 08b4f155431e6067 |  | 0 | 3 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/UITools.cs | 162 | 0f8dbd3a43089c84 |  | 0 | 12 | 3 | 0 | 3 | 0 |
| Assembly-CSharp/Rewired.UI.ControlMapper/Window.cs | 457 | 08a84eec97b09121 | Update:183 | 0 | 3 | 3 | 0 | 6 | 0 |
| Assembly-CSharp/Rewired.Utils/ExternalTools.cs | 171 | 111e90bdee5f3d66 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RewiredControllerTest.cs | 15 | 866934447f57fe16 | LateUpdate:10 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/RotateCross.cs | 22 | 6e4c87b57379b1e9 | Update:17 | 0 | 2 | 0 | 2 | 0 | 0 |
| Assembly-CSharp/RotateTest.cs | 19 | c082a6e1652820d4 | Update:10 | 0 | 0 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/SampleMesh.cs | 79 | be03096ac8442ee8 | Update:24 | 4 | 8 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/SaveStateScope.cs | 21 | e9766d5f5b23848b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scnCalibration.cs | 666 | 248ec422fe39a167 | Update:272 | 8 | 25 | 7 | 3 | 0 | 0 |
| Assembly-CSharp/scnCLS.cs | 2403 | 9db255ca773c00a9 | Update:744, LateUpdate:883 | 37 | 60 | 31 | 2 | 19 | 0 |
| Assembly-CSharp/scnEditor.cs | 7761 | b84d692c2e723752 | LateUpdate:2347, Update:2361, OnGUI:6661 | 109 | 325 | 87 | 2 | 101 | 0 |
| Assembly-CSharp/scnGame.cs | 2277 | db5ffa04b61303bd | Update:158 | 66 | 40 | 17 | 3 | 33 | 0 |
| Assembly-CSharp/scnLevelSelect.cs | 1174 | 8afe564335444835 | Update:353, LateUpdate:568 | 7 | 52 | 28 | 0 | 4 | 0 |
| Assembly-CSharp/scnLevelSelectTaro.cs | 187 | ccd7d0a5a73a1e22 | Update:60, LateUpdate:88 | 0 | 2 | 6 | 0 | 0 | 0 |
| Assembly-CSharp/scnMinesweeper.cs | 582 | 8c3fa97854b11c6d | Update:249 | 15 | 16 | 12 | 0 | 4 | 0 |
| Assembly-CSharp/scnSplash.cs | 93 | 84af78556d206070 | Update:35 | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scnTaroMenu0.cs | 116 | bef1625c42364aab | Update:70 | 0 | 1 | 4 | 0 | 1 | 0 |
| Assembly-CSharp/scnTaroMenu1.cs | 58 | 26c1733d5fab3ab3 | Update:38 | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/scnTaroMenu2.cs | 339 | b9fdfe56e5b61f82 | Update:205 | 5 | 5 | 14 | 0 | 2 | 0 |
| Assembly-CSharp/scnTaroMenu3.cs | 199 | 390832b08aa3bd98 | Update:78 | 0 | 2 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/scnVegaMenu.cs | 7 | 783d84a7d6ecf29d |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrACGear.cs | 39 | c557e40ec4c09e56 |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/scrAnimatedAngledLine.cs | 30 | 6677ec89e84f6a90 | Update:21 | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrAnimatedLineCircle.cs | 50 | 44c4391b812b9dda | Update:30 | 2 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrBackgroundBars.cs | 81 | 40494f9d803057b2 |  | 2 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrBackgroundSizeFollowCamera.cs | 22 | 72b94ff2d592030a | Update:17 | 0 | 1 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/scrBadgeContainer.cs | 75 | 3d55ffb0b2c5d94e | Update:28 | 0 | 2 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/scrBarMaker.cs | 107 | 39da4714481c10e4 | Update:70 | 3 | 4 | 5 | 0 | 5 | 0 |
| Assembly-CSharp/scrBenchmark.cs | 109 | 22f7b2912a17b08f | Update:46 | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrBestMultiplierText.cs | 124 | f7b3d97c351dc542 | Update:51 | 0 | 6 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/scrBgbar.cs | 66 | 61c2be7b35e29161 | Update:48 | 0 | 3 | 5 | 3 | 7 | 0 |
| Assembly-CSharp/scrBgbarnew.cs | 52 | 39104e89679c3f45 | Update:35 | 0 | 3 | 5 | 0 | 1 | 0 |
| Assembly-CSharp/scrBGCamNoRotate.cs | 13 | fd21c4ed08504677 | Update:9 | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/scrBirthdayCheck.cs | 21 | 8bfd911a70708c68 | Update:9 | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrBlur.cs | 84 | 183d810f0ab5f8fc |  | 1 | 2 | 0 | 1 | 1 | 7 |
| Assembly-CSharp/scrButterfliesOnFail.cs | 16 | 941fb5e03b4014a2 | Update:5 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrButtonURL.cs | 30 | b617c90e7857c72b |  | 0 | 0 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/scrButtonURL_OSBased.cs | 13 | 63003bc3798dfa7f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrCalibrationLine.cs | 60 | 68e04be0ad20ae17 | Update:33 | 0 | 1 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/scrCalibrax.cs | 52 | 8b5385f7753c6a6f |  | 0 | 1 | 3 | 1 | 0 | 0 |
| Assembly-CSharp/ScrCallibrationBGLines.cs | 84 | f5e92560445c8808 |  | 3 | 0 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/scrCamera.cs | 680 | 66c1245bb1b6e253 | Update:294, LateUpdate:399 | 1 | 29 | 16 | 6 | 4 | 4 |
| Assembly-CSharp/scrCamera3D.cs | 11 | 26f4c4ce97f9bf8d | Update:7 | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/scrClearTaroStoryProg.cs | 39 | 81abb9b45b5f5d04 | Update:11 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrClickableTextmesh.cs | 18 | 879e9dc963f69ba7 |  | 0 | 0 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/scrCLSPortalChanger.cs | 147 | 0fcee42ac5293e0d | Update:114 | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrColorChangeOnBeat.cs | 19 | d81ee34da951fb54 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrColorPlanet.cs | 47 | 95f5cec1f39fdf59 |  | 0 | 2 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrComingSoon.cs | 18 | cd0e7df5f8532b91 | LateUpdate:13 | 0 | 1 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/scrConductor.cs | 1264 | d2a89ec93844fff6 | Update:792 | 27 | 31 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/scrController.cs | 3073 | 8c19704ddebbc020 | Update:1640, LateUpdate:2715 | 45 | 30 | 20 | 1 | 27 | 0 |
| Assembly-CSharp/scrCountdown.cs | 115 | 4d202ae2e9bdde2f | Update:23 | 1 | 2 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/scrCountdownZoom.cs | 33 | 9189c5e627356879 | Update:17 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrCreditsText.cs | 211 | ea54b51e0e128659 | Update:148 | 2 | 1 | 16 | 0 | 0 | 0 |
| Assembly-CSharp/scrCustomBackgroundSprite.cs | 253 | e0faead61ddb87aa | LateUpdate:52 | 4 | 9 | 15 | 2 | 3 | 0 |
| Assembly-CSharp/scrDateCountdown.cs | 32 | 4467bf4e3b19908c | Update:15 | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrDebugHUDMessage.cs | 45 | 2e352a8bb3d2ad52 | Update:17 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrDecoration.cs | 974 | 335839ce75286f5c |  | 8 | 19 | 12 | 0 | 1 | 0 |
| Assembly-CSharp/scrDecorationManager.cs | 676 | 370ceb0ebddef307 | Update:160, LateUpdate:173 | 17 | 24 | 24 | 1 | 8 | 0 |
| Assembly-CSharp/scrDialogBox.cs | 12 | 952880c17e59f646 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDifficultyText.cs | 36 | ad90a67bef582a22 | Update:11 | 0 | 0 | 0 | 0 | 5 | 0 |
| Assembly-CSharp/scrDisableIfBooth.cs | 16 | 0992dcc220ef77a7 | Update:13 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfCoop.cs | 10 | 0d47c084c59efa2c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfMinimumVFX.cs | 10 | b92c939dd6dbc2c9 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfMobile.cs | 10 | 1bb095a100916f78 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfNotAllLevelsComplete.cs | 18 | a58fb85a916fa2ec | Update:7 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfNotEditingLevel.cs | 10 | a697efc2a221d8b5 | Update:3 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfNotGameWorld.cs | 10 | 2486d782551be2fe |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfNotJudge.cs | 16 | e4599a9907d0dcdf | Update:13 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfNotMobile.cs | 10 | 4c56e0f19ad209b6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfNotSwitch.cs | 10 | c7f5e8c540d6c2fd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfNotUnlockedXtra.cs | 12 | 24e70762e708f692 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfOverallProgressStage.cs | 25 | 147548ea54dff90b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfPastDate.cs | 13 | 057308f612c2344c | Update:5 | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfPastProgressStage.cs | 14 | 1b66fa48a6091a6c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfSteamworksDisabled.cs | 12 | 10818f042a64b0a3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfSwitch.cs | 10 | 3c0329331a260ca0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfUnlockedXtra.cs | 12 | e607d1135c0dd94f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfWorldNotComplete.cs | 21 | 239402a449d87ac0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrDisableIfWorldNotCompleteString.cs | 14 | 0f2d3bd157e4ef04 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ScreenScroll.cs | 65 | 7e0e55919e93b94e | OnRenderImage:33, Update:48 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/Screenshake.cs | 89 | 0bf43d3cb5a0e58f |  | 1 | 4 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/ScreenTile.cs | 57 | 33be47461350f1aa | OnRenderImage:31, Update:46 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/scrEnableIfAprilFools.cs | 16 | 7481c410c25a4d59 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrEnableIfBeta.cs | 21 | ce9df153964542fe |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrEnableIfCNY.cs | 27 | 39a614346c8a80f9 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrEnableIfShowTech.cs | 14 | ed83c97591409eb2 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrEnableOnlyForLanguage.cs | 21 | 9aa923da521311ed |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrFailBar.cs | 94 | b5d310fc766b89f6 | Update:37 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrFearGrowsLightningManager.cs | 29 | 3e30d942eb2f83c3 |  | 2 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrFlash.cs | 100 | eb6e04b115349587 | Update:33 | 0 | 0 | 0 | 2 | 2 | 0 |
| Assembly-CSharp/scrFlavourText.cs | 87 | 862c7d561a7d16f0 | Update:30 | 1 | 4 | 1 | 0 | 3 | 0 |
| Assembly-CSharp/scrFlavourTextNC.cs | 146 | 219309597fb23423 | Update:38 | 1 | 4 | 1 | 0 | 4 | 0 |
| Assembly-CSharp/scrFlipOnBeat.cs | 20 | aeaadc7bf65fd6ed |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrFloatTween.cs | 16 | 8cde23040d792396 |  | 0 | 1 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/scrFloor.cs | 1940 | ea36018728557dd5 | Update:1234, LateUpdate:1316, OnBecameVisible:1391, OnBecameInvisible:1412 | 10 | 30 | 24 | 15 | 17 | 0 |
| Assembly-CSharp/scrGem.cs | 122 | 3058fab36990cc53 | Update:54 | 1 | 2 | 7 | 0 | 1 | 0 |
| Assembly-CSharp/scrGfxFloat.cs | 73 | 4d8176763cd35cfe | Update:39 | 0 | 0 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/scrHitErrorMeter.cs | 203 | 5910332d396aecc4 |  | 3 | 11 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/scrHitTextManager.cs | 127 | 49aa3438e7a4d347 |  | 2 | 19 | 7 | 0 | 2 | 0 |
| Assembly-CSharp/scrHitTextMesh.cs | 222 | 8fc8d53df18f18f2 | Update:197 | 1 | 4 | 11 | 0 | 3 | 0 |
| Assembly-CSharp/scrHoldRenderer.cs | 382 | b6c19c740a21aa04 | Update:133 | 4 | 10 | 11 | 28 | 2 | 0 |
| Assembly-CSharp/scrHUDText.cs | 39 | f8b15fc2ad1fe176 | Update:23 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrIconOrient.cs | 16 | 307d1fd3af1f122d | Update:12 | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/scrImageByLanguage.cs | 29 | 5afd63f3ee6991da |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrImageFader.cs | 41 | 024e1e56efaa1c09 | Update:25 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrKongAPI.cs | 55 | af1262e2359e0a92 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrLanternShake.cs | 53 | 969df7c2fd2261a1 | Update:48 | 0 | 0 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/scrLetterPress.cs | 7 | f6b95204b3826244 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrLevelMaker.cs | 966 | 17efd15e48d41c52 |  | 29 | 27 | 23 | 5 | 23 | 0 |
| Assembly-CSharp/scrLevelMaker2.cs | 51 | f911658059b8a55b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrLifePlanet.cs | 35 | fab560249903fa5d |  | 0 | 0 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/scrLivesCounter.cs | 48 | 8187ff2fbd91df5b |  | 2 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrLoader.cs | 265 | ce57887bf31d5a35 | Update:61 | 0 | 12 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/scrLoadingPlanet.cs | 26 | 4dd33486922ceabc | Update:20 | 0 | 2 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/scrLockToCamera.cs | 52 | 80739faf4bbffd9a | LateUpdate:27 | 0 | 2 | 7 | 0 | 1 | 0 |
| Assembly-CSharp/scrLogoText.cs | 106 | 8ee169211332cb10 | LateUpdate:82 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrMarginTracker.cs | 233 | 8de5cb6c8cc79afb |  | 4 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrMatchCameraSize.cs | 20 | 1d0d1c98ecbe80e7 | Update:12 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrMaxLevelTracker.cs | 25 | 821c7a46a8a30168 | Update:14 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrMDController.cs | 82 | 08a6605b9a522f58 |  | 0 | 0 | 4 | 0 | 1 | 0 |
| Assembly-CSharp/scrMenu.cs | 12 | 1af37371eb4e83ff | Update:9 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrMenuContinueDisable.cs | 15 | aaf8dd39a830a573 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrMenuContinueInfo.cs | 31 | c5d9b6e62af27fbf |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrMenuMovingFloor.cs | 245 | d912b6906b826c91 |  | 1 | 4 | 16 | 0 | 5 | 0 |
| Assembly-CSharp/scrMenuWorldStatsText.cs | 144 | 6afb69d904a07890 |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrMetronome.cs | 66 | 17ff2da0da6414f8 |  | 3 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrMisc.cs | 580 | 86af5e53553a936b |  | 2 | 16 | 5 | 2 | 2 | 0 |
| Assembly-CSharp/scrMissIndicator.cs | 60 | 8bfa881dd219fd48 | Update:18 | 0 | 2 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/scrMistakesManager.cs | 370 | 6b0664aaa6cc7d60 |  | 9 | 11 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrMove.cs | 59 | e68bfc527b6ad70d | Update:19 | 0 | 1 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/scrNeoCosmosEntrance.cs | 126 | be2a1f4a029bf303 | Update:47 | 0 | 2 | 6 | 1 | 2 | 0 |
| Assembly-CSharp/scrNewgroundsAPIManager.cs | 64 | 24289657be471a76 | OnGUI:60 | 1 | 0 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/scrObjectDecoration.cs | 341 | e271102111495030 |  | 1 | 3 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/scrOffsetText.cs | 18 | 3c8e3e5e0b8330b7 | Update:11 | 0 | 0 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/ScrollBarSetSize.cs | 28 | 8cda987e7883ecf3 | LateUpdate:16 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/ScrollEvent.cs | 17 | 7645f08e5aec4aca |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrOnBeatColor.cs | 26 | 5094f8f51a3df0b0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrOnSpecificBeat.cs | 20 | 7c6d43d5744197fa | Update:17 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrOpacityChangeOnBeat.cs | 32 | d96ceb241138b186 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrOptionsDot.cs | 84 | b319193d9312c378 | Update:27, Update:72 | 2 | 2 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/scrOptionsExperiencingText.cs | 90 | c1732a1028d8f4d2 | Update:34 | 2 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/scrOptionsWindows.cs | 148 | 1b1c3e3c50f08a5c | Update:101 | 3 | 3 | 9 | 0 | 2 | 0 |
| Assembly-CSharp/scrPAPPart1BG.cs | 74 | bfe74154c522336a | Update:68 | 1 | 0 | 2 | 6 | 0 | 0 |
| Assembly-CSharp/scrPAPPart3BG.cs | 97 | f2a748b9f567f6ef | Update:91 | 0 | 0 | 1 | 4 | 0 | 0 |
| Assembly-CSharp/scrPAPRopeHex.cs | 38 | 14b5709e9563d04b | Update:16 | 0 | 1 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/scrParallax.cs | 116 | b51c28a8733c2a4d | LateUpdate:112 | 0 | 4 | 6 | 0 | 0 | 0 |
| Assembly-CSharp/scrParticleDecoration.cs | 212 | a95bd60ad3bfe70a | Update:116 | 0 | 8 | 2 | 1 | 1 | 0 |
| Assembly-CSharp/scrPercentageComplete.cs | 43 | 87d06691ab6d9ce8 |  | 0 | 1 | 1 | 0 | 3 | 0 |
| Assembly-CSharp/scrPlanet.cs | 1675 | 3b45d8cd7a153ec4 | Update:235, LateUpdate:363 | 26 | 9 | 50 | 1 | 14 | 0 |
| Assembly-CSharp/scrPlanetCopyCam.cs | 45 | 4e213c6329df2bd7 | LateUpdate:33 | 0 | 1 | 3 | 0 | 2 | 0 |
| Assembly-CSharp/scrPlayer.cs | 1033 | 05ff9808ab0d8c9f | Update:259 | 17 | 9 | 6 | 0 | 0 | 0 |
| Assembly-CSharp/scrPlayerManager.cs | 192 | 3a4d9bda88825692 |  | 7 | 13 | 5 | 0 | 1 | 0 |
| Assembly-CSharp/scrPortal.cs | 512 | 0413dc08948d872d | Update:249 | 6 | 5 | 10 | 2 | 2 | 0 |
| Assembly-CSharp/scrPortalParticles.cs | 80 | 4c63be497cc7fd77 | LateUpdate:42 | 0 | 0 | 3 | 1 | 2 | 0 |
| Assembly-CSharp/scrPortalUpperPart.cs | 12 | 8d50ff348bb89736 | Update:9 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrPrefabDecoration.cs | 36 | c0d7f5538d88dedd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrPressToStart.cs | 33 | 287b5afb245195c8 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrPulseOnBeat.cs | 29 | 8b3fca18c723fb03 |  | 0 | 0 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/scrRing.cs | 134 | 57089bfb5d43a12c | Update:46 | 0 | 0 | 3 | 3 | 0 | 0 |
| Assembly-CSharp/scrRotate.cs | 11 | ea6678f0cb49391b | Update:7 | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/scrRotateOnAprilFools.cs | 6 | bfe3a7ae8176f4a2 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrSamurai.cs | 63 | bd16ebd15c578a69 | Update:30 | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/scrSaveLoader.cs | 17 | e7e37fb4e72aa66a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrSaveManager.cs | 5 | 1a94544c5f31517f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrScaleByAspectRatio.cs | 22 | a3de199b15728d75 | Update:14 | 0 | 0 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/scrScaleToScreenSize.cs | 19 | 91a4a42f26698202 |  | 0 | 1 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/scrScrambleText.cs | 44 | 7df55aadc84382b2 | Update:26 | 1 | 2 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrScroller.cs | 47 | f6912b9ee4460351 | Update:31 | 0 | 0 | 7 | 0 | 1 | 0 |
| Assembly-CSharp/scrSetChildrenOrderInLayer.cs | 9 | 9cd67c1d203f7d41 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrSetEmojiMode.cs | 30 | 4afc7631d0b3e188 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrSfx.cs | 113 | 9db6e1fe0d4400f7 |  | 0 | 0 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/scrShortcutText.cs | 48 | 1f608b600fd84afc |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrShowIfDebug.cs | 80 | 5bebeeb6475d016d | Update:20 | 0 | 2 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrShowOnlyInPhase.cs | 110 | dfe903b05598287a | Update:37 | 9 | 1 | 1 | 0 | 5 | 0 |
| Assembly-CSharp/scrSpike.cs | 71 | a5464bbff0452ff8 | Update:45 | 1 | 1 | 6 | 0 | 1 | 0 |
| Assembly-CSharp/scrSpriteAnimator.cs | 30 | abffa52bc995cea3 | Update:18 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrTempEscToQuit.cs | 28 | 8013cd2014c5b474 | Update:9, LateUpdate:13 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrTextBonusColor.cs | 12 | 59015dddaca12bf8 | Update:5 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrTextChanger.cs | 115 | 1918b119b1446bf9 |  | 0 | 0 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/scrTextChangerMultiline.cs | 47 | 53d43df9902eadd5 |  | 1 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrTextDecoration.cs | 157 | 39907d2063843168 |  | 0 | 4 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/scrTextFader.cs | 60 | 0faf0789dc071149 | Update:30 | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrTextWithLink.cs | 78 | 9e5eea4f34f99e5f | Update:47 | 0 | 0 | 0 | 0 | 3 | 0 |
| Assembly-CSharp/scrTextWithLink_TMP.cs | 44 | e9d8974c4c6bcbd9 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrThingShake.cs | 108 | 6a7ca0f497668cb7 | Update:82 | 0 | 0 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/scrUIController.cs | 506 | bfe3bf3fc047ab63 | Update:304 | 5 | 7 | 4 | 2 | 5 | 0 |
| Assembly-CSharp/scrVersionText.cs | 39 | c230de860d039af6 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/scrVfx.cs | 58 | 31da0153bb539a25 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrVfxPlus.cs | 291 | 8396cd822af54fa9 | Update:181 | 5 | 7 | 1 | 0 | 3 | 0 |
| Assembly-CSharp/scrVisualDecoration.cs | 533 | fc43753c37f0f0f9 |  | 3 | 9 | 3 | 9 | 2 | 7 |
| Assembly-CSharp/scrVolumeTrackerFade.cs | 32 | 1fb478c55d97fe3d | Update:26 | 0 | 1 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/scrVolumeTrackerFloat.cs | 29 | b81491728b75009e | Update:24 | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/scrVolumeTrackerScale.cs | 34 | 809613f145ac5aa2 | Update:26 | 0 | 3 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/scrWebTabLanguageSwitch.cs | 14 | f546640aa637f68a | Update:5 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SecretTree.cs | 32 | 54b7a34d52da916f |  | 0 | 2 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/Sef.cs | 139 | 277bc0baec1ae447 | Update:120 | 2 | 3 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/Segment.cs | 43 | ac5f3a47b58b04ca |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SetActiveAtAwake.cs | 15 | 6490b74130507ad0 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SetHalloweenClouds.cs | 15 | 9cb914e0125838e2 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/SetInactiveOnStart.cs | 9 | 7e7c6c9b8426c9e8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SetSortingLayer.cs | 15 | 39b06cbe4cc57bcd |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/SettingsGoat.cs | 108 | f4a4a2cbd27faa33 | Update:40 | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SettingsMenu.cs | 1383 | 69d3719fcad86849 | Update:249 | 17 | 9 | 5 | 0 | 6 | 0 |
| Assembly-CSharp/SettingsTabButton.cs | 52 | 6f8f4a967a71fb4b |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/SfxSound.cs | 81 | e93337b3365961aa |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Shatter.cs | 124 | a64e867bcdfb0832 | Update:109 | 4 | 3 | 12 | 3 | 1 | 0 |
| Assembly-CSharp/SingSing.cs | 1486 | 0d4f6fb6377d9a01 | Update:594 | 37 | 75 | 78 | 12 | 0 | 0 |
| Assembly-CSharp/Singsing2_Stats.cs | 17 | 90164cbeeeb62755 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SingSing_Glider.cs | 42 | 56f3bb72766a286c | LateUpdate:15 | 0 | 2 | 5 | 0 | 0 | 0 |
| Assembly-CSharp/SingSing_Notefield.cs | 271 | 07f194fd33fb81e4 | Update:190 | 4 | 7 | 37 | 1 | 0 | 0 |
| Assembly-CSharp/Singsing_Stats.cs | 17 | de8e2b3bb06e31bf |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SingSingTutorials.cs | 113 | bbc4696e541615f6 | Update:54 | 1 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Skeletons.cs | 39 | d446fcb78621b1b1 |  | 2 | 0 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/SkipIntroBehavior.cs | 6 | 1fa476516425691f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SmoothScrollRect.cs | 77 | 5992d8b76f11a398 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/SmoothSR.cs | 50 | 8c1b75f48e776e4d | Update:35 | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SocialPauseButton.cs | 30 | f6fa3ee26b2d1991 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SpecialArtistType.cs | 6 | 76a4dedcf7cba213 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SpeechEvent.cs | 12 | 2da956cadfacbbe5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SpeedType.cs | 5 | 2cc8756ff7c9c614 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SpinningPlanets.cs | 39 | 248bea077ee73968 | Update:27 | 2 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/SpriteAlphaMask.cs | 77 | 5b7c92f5ff246407 |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/SpriteAlphaMaskUtils.cs | 194 | 60bc7a45646e4602 |  | 3 | 7 | 8 | 0 | 2 | 3 |
| Assembly-CSharp/SpriteInstantiater.cs | 63 | 4c549fea47122733 | Update:41 | 0 | 3 | 7 | 0 | 0 | 0 |
| Assembly-CSharp/SSTSampleMovement.cs | 62 | ac7fbe3478e1332c | Update:20 | 0 | 6 | 10 | 0 | 0 | 0 |
| Assembly-CSharp/States.cs | 11 | 66b051329671d6c8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/StatsData.cs | 14 | 088031dbbd9384cd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Style.cs | 5 | e7c2d85dc6e24219 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/SuperGameFeelEffectsExtentions.cs | 76 | 9117b4003b3ff562 |  | 0 | 0 | 12 | 0 | 6 | 0 |
| Assembly-CSharp/SuperSpriteTrail.cs | 394 | 19a27328604c7848 | Update:268, LateUpdate:276, FixedUpdate:284 | 4 | 9 | 31 | 2 | 7 | 0 |
| Assembly-CSharp/System/MathExtension.cs | 6 | 6dd8e906e0c1574a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/System.Collections.Generic/IEnumerableExtension.cs | 65 | 01500278ba65a9a1 |  | 1 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/System.Collections.Generic/IndexOfForIReadOnlyListExtension.cs | 23 | e82bcd77265f94c2 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/System.Collections.Generic/PriorityQueue.cs | 545 | c5b305dc032abcab |  | 12 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/System.Runtime.CompilerServices/IsExternalInit.cs | 5 | 87b5faa75376153d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TakeScreenshot.cs | 78 | b6e143808b07d8f5 |  | 1 | 5 | 0 | 1 | 0 | 10 |
| Assembly-CSharp/TargetPlanet.cs | 7 | 1aaa363e4773f5cd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TaroBGScript.cs | 596 | 2cdcbb4479e882eb | LateUpdate:261, Update:270 | 13 | 16 | 13 | 16 | 6 | 0 |
| Assembly-CSharp/TaroCutsceneScript.cs | 644 | fc63c01ef6cee4c6 | Update:526 | 9 | 19 | 12 | 4 | 1 | 0 |
| Assembly-CSharp/TemplateBG.cs | 23 | bc7d519d9824ae47 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TemplateCutscene.cs | 22 | 6aac71e4348c1255 | Update:18 | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/testdropdowninstantiator.cs | 163 | cb5090a2861c2cf5 |  | 8 | 8 | 6 | 0 | 1 | 0 |
| Assembly-CSharp/TestRoom.cs | 87 | b6e7e4c68d3b5c2c |  | 1 | 4 | 11 | 0 | 1 | 0 |
| Assembly-CSharp/TextCommand.cs | 18 | 8a189d05c8927755 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TextEvent.cs | 19 | d655787e59836918 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TextHyperlinks.cs | 108 | 01cc1a4055a9635a |  | 2 | 0 | 1 | 0 | 3 | 0 |
| Assembly-CSharp/TextureManager.cs | 325 | c6f2da22394b6999 |  | 7 | 14 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TextureScale.cs | 144 | ff7d52f9753a6072 |  | 6 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ThirdSun.cs | 1358 | aeafba8d8b74353a | Update:568 | 41 | 103 | 101 | 8 | 6 | 0 |
| Assembly-CSharp/ThirdSun2_Stats.cs | 17 | e6531a7901c6d7c7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ThirdSun_Stats.cs | 17 | 865167845b203041 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ThumbnailMaker_bad.cs | 47 | 12714f1cc5bea93b |  | 1 | 3 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TileFlash.cs | 32 | 1fc4e321860fb2ac | Update:19 | 0 | 0 | 2 | 0 | 3 | 0 |
| Assembly-CSharp/TileFlashStyle.cs | 11 | d6893b197796456f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TileOverrideStyle.cs | 6 | c2d5b24e7d254f7d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TileRelativeTo.cs | 6 | 7d25a95a5906212c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TileShape.cs | 6 | 58e2f0b35dfb109d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Timer.cs | 9 | 5a5481864eb7c01f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TMP_TextHyperlinks.cs | 24 | 08cdd83b574d0a7c |  | 0 | 0 | 1 | 0 | 2 | 0 |
| Assembly-CSharp/TMPro/TMP_DigitValidator.cs | 18 | de3cc37c51a6dd4a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TMPro/TMP_PhoneNumberValidator.cs | 123 | 521be61475aa1b2f |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TMPro/TMP_TextEventHandler.cs | 250 | 6bcd41902f81616f | LateUpdate:148 | 1 | 7 | 0 | 0 | 6 | 0 |
| Assembly-CSharp/TMPro.Examples/Benchmark01.cs | 91 | 2728a552558336c9 |  | 1 | 0 | 0 | 3 | 2 | 0 |
| Assembly-CSharp/TMPro.Examples/Benchmark01_UGUI.cs | 84 | 034e5ab4fe39d50d |  | 1 | 0 | 0 | 1 | 0 | 0 |
| Assembly-CSharp/TMPro.Examples/Benchmark02.cs | 67 | 3176da8916a16494 |  | 1 | 12 | 5 | 1 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/Benchmark03.cs | 86 | 84b1e1037c894dfd |  | 1 | 6 | 2 | 3 | 3 | 0 |
| Assembly-CSharp/TMPro.Examples/Benchmark04.cs | 46 | 57dc7c3c87900a33 |  | 1 | 4 | 2 | 0 | 0 | 0 |
| Assembly-CSharp/TMPro.Examples/CameraController.cs | 245 | 17628d1063c5842c | LateUpdate:91 | 0 | 4 | 15 | 0 | 0 | 0 |
| Assembly-CSharp/TMPro.Examples/ObjectSpin.cs | 68 | 36b4bacbeb726b54 | Update:45 | 0 | 2 | 4 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/ShaderPropAnimator.cs | 38 | 607d4b320ee606b8 |  | 1 | 1 | 0 | 1 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/SimpleScript.cs | 27 | 6efb3134e7e4357d | Update:22 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TMPro.Examples/SkewTextExample.cs | 104 | 8f59ab2f5415d941 |  | 2 | 8 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/TeleType.cs | 43 | 0037a1fddebd0fe7 |  | 1 | 4 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/TextConsoleSimulator.cs | 92 | 9f799be43c11531c |  | 2 | 3 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/TextMeshProFloatingText.cs | 165 | ab1526a41b44a09a |  | 2 | 34 | 18 | 1 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/TextMeshSpawner.cs | 50 | d92f9747a7708cf5 |  | 1 | 6 | 2 | 1 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/TMP_ExampleScript_01.cs | 49 | 94a820eef70c92ab | Update:41 | 0 | 1 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/TMPro.Examples/TMP_FrameRateCounter.cs | 119 | 1505ef8260b60dfb | Update:61 | 0 | 10 | 6 | 0 | 0 | 0 |
| Assembly-CSharp/TMPro.Examples/TMP_LetterMovement.cs | 204 | c8984c19f54de3d2 | Update:127 | 4 | 4 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/TMP_TextEventCheck.cs | 64 | 423ffb20c556568f |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/TMP_TextInfoDebugTool.cs | 7 | 684fcae99ea7e764 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TMPro.Examples/TMP_TextSelector_A.cs | 100 | b1044a6db0b3070a | LateUpdate:27 | 1 | 2 | 1 | 0 | 4 | 0 |
| Assembly-CSharp/TMPro.Examples/TMP_TextSelector_B.cs | 254 | 6f32418998662f7a | LateUpdate:72 | 2 | 1 | 3 | 0 | 6 | 0 |
| Assembly-CSharp/TMPro.Examples/TMP_UiFrameRateCounter.cs | 123 | 1bf6b6f838952c75 | Update:58 | 0 | 17 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/TMPro.Examples/TMPro_InstructionOverlay.cs | 69 | 1c14173e4ab2c3fa |  | 0 | 5 | 6 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/VertexColorCycler.cs | 46 | a06cfb3b959f5835 |  | 1 | 3 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/VertexJitter.cs | 117 | 61422da6c741455e |  | 4 | 4 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/VertexShakeA.cs | 113 | 2320069e3572d1a6 |  | 5 | 5 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/VertexShakeB.cs | 125 | 581eeb0d75bef23c |  | 5 | 5 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/VertexZoom.cs | 121 | e72418d15113616c |  | 3 | 5 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TMPro.Examples/WarpTextExample.cs | 97 | 3a6df62d86d596e9 |  | 2 | 7 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/TNOBG_controller.cs | 70 | dbdb3e658c72fcdd | LateUpdate:24 | 0 | 0 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/TNOFilter.cs | 62 | 8f783d443c7b6c85 | OnRenderImage:36, Update:51 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp/TNOJelly.cs | 46 | 83d69360b97144b3 | Update:15 | 1 | 0 | 7 | 0 | 2 | 0 |
| Assembly-CSharp/TNONebula.cs | 48 | 89bb05228d9d6844 | Update:34 | 0 | 2 | 0 | 3 | 0 | 0 |
| Assembly-CSharp/TNOPlanet.cs | 30 | 9117cf90bd6562a3 | Update:21 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TNOShaderHelper.cs | 24 | 64258a95350e744f |  | 0 | 0 | 0 | 1 | 1 | 0 |
| Assembly-CSharp/ToggleVisible.cs | 21 | 9f1c08f95d801104 | OnBecameVisible:9, OnBecameInvisible:14 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Torch.cs | 16 | ae0dea955069dda8 |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/TrackAnimationType.cs | 12 | 16f1d2fef399a6eb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TrackAnimationType2.cs | 10 | dd88fc9ec0af7b45 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TrackColorPulse.cs | 6 | ba329c97a9d8227c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TrackColorType.cs | 10 | 83b74e44687a07cb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TrackStyle.cs | 9 | 5f786298d30b7f5f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TrailerScene1.cs | 121 | 09c607ac1914dcca |  | 3 | 7 | 9 | 0 | 0 | 0 |
| Assembly-CSharp/TrailerScene2.cs | 99 | bcae437a120dfd6f |  | 4 | 7 | 2 | 0 | 1 | 0 |
| Assembly-CSharp/TrailerScene3.cs | 123 | a358d55d3dc914f0 |  | 4 | 7 | 9 | 0 | 1 | 0 |
| Assembly-CSharp/TransformGizmoHolder.cs | 210 | e361d4e764e2285d | LateUpdate:61 | 4 | 9 | 6 | 0 | 1 | 0 |
| Assembly-CSharp/TutorialBackground.cs | 126 | a3f68c5c9d4c2176 | LateUpdate:59 | 6 | 2 | 3 | 3 | 3 | 0 |
| Assembly-CSharp/TweakableDropdown.cs | 781 | 7a4afe01caef84e6 | Update:616 | 8 | 15 | 7 | 0 | 5 | 0 |
| Assembly-CSharp/TweakableDropdownItem.cs | 146 | e19f06373604cc8a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TweakableDropdownScrollDragHandler.cs | 20 | cda0cabc4abd8b4e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/TweenType.cs | 28 | 4eed932da9308548 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/UIGridRenderer.cs | 69 | c18e685e32617ae6 |  | 2 | 9 | 8 | 0 | 0 | 0 |
| Assembly-CSharp/UILineRenderer.cs | 85 | d6b2ea0666af579f | Update:54 | 2 | 8 | 8 | 0 | 0 | 0 |
| Assembly-CSharp/UnityEngine.Purchasing.Security/AppleStoreKitTestTangle.cs | 30 | ee7cf7c21ce74c30 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/UnityEngine.Purchasing.Security/AppleTangle.cs | 32 | 94a6636f96843f9d |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/UnityEngine.Purchasing.Security/GooglePlayTangle.cs | 27 | 5de718bf2da6b467 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/UnityEngine.UI/LetterMovement.cs | 217 | 20b66b670beeed51 | Update:37 | 5 | 6 | 6 | 0 | 1 | 0 |
| Assembly-CSharp/UnityEngine.UI/LetterSpacing.cs | 180 | 0416b0ec44ae66fb |  | 4 | 1 | 6 | 0 | 1 | 0 |
| Assembly-CSharp/UnityLayer.cs | 21 | 80938f71e9691603 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/Utils.cs | 138 | e3b9f922afe2d4b4 |  | 5 | 6 | 0 | 0 | 1 | 0 |
| Assembly-CSharp/VegaDLCManager.cs | 39 | 5b5c199cf375e8b8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/VideoBloom.cs | 239 | ae7cbaa35975dbdf | OnRenderImage:177 | 2 | 7 | 0 | 0 | 0 | 27 |
| Assembly-CSharp/VirtualAvatar.cs | 76 | a7dd2e287e949e5c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/VirtualAvatarCanvas.cs | 79 | 4f202d26098cc435 | Update:22 | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/VisualEffects.cs | 5 | 31cd290392292a34 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/VisualQuality.cs | 5 | 1788181271d12695 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/WagBob.cs | 49 | 32ab4a6ce167ac02 | Update:37 | 0 | 0 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/WebServiceResult.cs | 7 | 5f689e781ea743f8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/WeldingTest.cs | 89 | 42aa5f37782324f4 | Update:24 | 2 | 11 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/WiderScreen.cs | 13 | 6a91e638e74347ea |  | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp/WipeDirection.cs | 7 | 293b003120cd9c81 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/WorkshopLevelList.cs | 114 | 95a6cc7f60120269 | Update:40 | 2 | 0 | 0 | 0 | 2 | 0 |
| Assembly-CSharp/WorkshopThumbnailMaker.cs | 51 | ed308c44a4c384a2 |  | 0 | 3 | 0 | 0 | 1 | 3 |
| Assembly-CSharp/WorldData.cs | 171 | 159d5ec2ffaff9cd |  | 1 | 8 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/WorldLightsDisplay.cs | 37 | 8914a95fe3bd40ef |  | 2 | 1 | 1 | 0 | 1 | 0 |
| Assembly-CSharp/WorldLightsDisplay_Crown.cs | 47 | 9cc06eae6a0b6e21 |  | 1 | 12 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/WorldLightsDisplay_Crystals.cs | 71 | 3ca4b04d48d6d9c6 |  | 1 | 9 | 15 | 0 | 6 | 0 |
| Assembly-CSharp/WorldLightsDisplay_Gems.cs | 47 | 95094f9f3edf44a7 |  | 1 | 12 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/WorldLightsDisplay_Generic.cs | 9 | 27f83138ada661f8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/WorldLightsDisplay_Lanterns.cs | 81 | dd5a3198e23fa074 |  | 1 | 12 | 3 | 0 | 3 | 0 |
| Assembly-CSharp/WorldSelectorTile.cs | 87 | 005c65dc20735b30 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/XMLLevel.cs | 16 | 29be5bcdecddee1e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/XMLLevelContainer.cs | 32 | 605256ebde19e6cb |  | 0 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/XMLWorld.cs | 9 | abc1d3cd25f0b90b |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/XRHare.cs | 53 | a08dc48ea8a67fad |  | 0 | 0 | 4 | 0 | 0 | 0 |
| Assembly-CSharp/ZipUtils.cs | 134 | b510533e5c0c8a79 |  | 6 | 10 | 0 | 0 | 0 | 0 |
| Assembly-CSharp/ZodiacBackground.cs | 198 | 25c3fdf701598277 | Update:158 | 7 | 4 | 3 | 0 | 0 | 0 |
| Assembly-CSharp/ZodiacBackgroundAnimal.cs | 115 | 2e0cfe18a2e1e989 | Update:54 | 2 | 2 | 5 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/AdofaiToolsAndroid.cs | 55 | 98479a07f60f5f37 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/AudioClipData.cs | 83 | 300996fe755695ad |  | 2 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/AudioOutputType.cs | 9 | ad57fd50a5834c6b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/CWPSTRUCT.cs | 14 | cd795d3482dc3153 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/EnumThreadDelegate.cs | 5 | 7e9ffda6478b1be1 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/HookProc.cs | 5 | a3ef16a128b351b3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/HookType.cs | 20 | d0fed96ea162e232 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/MSG.cs | 18 | 8b96a14c97e0c3c5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/POINT.cs | 19 | ac3f76d837812329 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/RECT.cs | 25 | c322a2c8e204fa63 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/UnityDragAndDropHook.cs | 79 | df3f17b985528beb |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/WinAPI.cs | 35 | 1d7a7618d6f2c7b7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/Window.cs | 41 | 3c563d2a5053574d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/B83.Win32/WM.cs | 240 | 215aaacd4e915c25 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/BaseN.cs | 30 | 0115f9287b330cdc |  | 2 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/BLINDED_AM_ME/Math_Functions.cs | 134 | cf76471ac53befcd |  | 1 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/BLINDED_AM_ME/ParticlePathFlow.cs | 73 | 9cfa7f5d7d44bfd4 | LateUpdate:32 | 1 | 1 | 1 | 0 | 2 | 0 |
| Assembly-CSharp-firstpass/BLINDED_AM_ME/Path.cs | 98 | 0ca5f7aaed0b74f2 |  | 2 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/BLINDED_AM_ME/Path_Comp.cs | 80 | 878352a95994dbb0 |  | 2 | 7 | 10 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/BLINDED_AM_ME/Path_Point.cs | 22 | 8b94e24ae640aec8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/CameraFilterPack_AAA_SuperComputer.cs | 90 | bba165edd66e12ce | OnRenderImage:51, Update:79 | 0 | 4 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_AAA_SuperHexagon.cs | 88 | 35c152f9b2bc5d09 | OnRenderImage:49, Update:77 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_AAA_WaterDrop.cs | 80 | cf415094fa149da1 | OnRenderImage:46, Update:69 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Alien_Vision.cs | 77 | c981f14a4cd8abdc | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Antialiasing_FXAA.cs | 61 | a49f36905b92ca49 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Atmosphere_Rain.cs | 93 | 16447ece334b9c72 | OnRenderImage:55, Update:82 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Atmosphere_Rain_Pro.cs | 97 | 530de0bd8bf8d811 | OnRenderImage:58, Update:86 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Atmosphere_Snow_8bits.cs | 77 | ba5efbb3f6a9df8a | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Blend.cs | 99 | 000ed35e5a2e654c | OnRenderImage:45, Update:75 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_BlueScreen.cs | 123 | 47c24e4d23ca51eb | OnRenderImage:71, Update:100 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Color.cs | 106 | 9e78ac6f4f5e6af2 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_ColorBurn.cs | 106 | 70f278f00504462c | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_ColorDodge.cs | 106 | 1d95faa90f60bb00 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Darken.cs | 106 | 59e56130ad16e5ff | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_DarkerColor.cs | 106 | d41d821ea32c2d09 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Difference.cs | 106 | 78c9bede11b6edd5 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Divide.cs | 106 | f4d4a81239f3dd4c | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Exclusion.cs | 106 | b2aab704de4a0b04 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_GreenScreen.cs | 118 | 726edb7ba4eddb14 | OnRenderImage:65, Update:94 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_HardLight.cs | 106 | e27f068b9e0c78ab | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_HardMix.cs | 106 | 4b13f915d7b5c6f4 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Hue.cs | 106 | 8c4cefca44fef4ac | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Lighten.cs | 106 | 7c62b27dcce58ecc | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LighterColor.cs | 106 | dc03fcc48d043d04 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LinearBurn.cs | 106 | 52b0da54badbe53b | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LinearDodge.cs | 106 | 7475d31fc75c9246 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_LinearLight.cs | 106 | d7ba4352999dce3f | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Luminosity.cs | 106 | 43153e0f6e90caad | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Multiply.cs | 106 | 8bbbf5d1ab076aa9 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Overlay.cs | 106 | a3baf41ad44d9cfb | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_PhotoshopFilters.cs | 257 | 59907cdc4d79221f | OnRenderImage:187, Update:233 | 0 | 6 | 0 | 2 | 2 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_PinLight.cs | 106 | aefe0e434c53a70b | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Saturation.cs | 106 | 30b3dfd93b868f33 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Screen.cs | 106 | 39dad5736a03ed0b | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_SoftLight.cs | 106 | 765e1c1725b41dfb | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_SplitScreen.cs | 121 | 5ab0b2319e405929 | OnRenderImage:70, Update:99 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_Subtract.cs | 106 | 0d7ab5a58bccc34d | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blend2Camera_VividLight.cs | 106 | ad108778197eb399 | OnRenderImage:48, Update:82 | 0 | 5 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blizzard.cs | 76 | 2c9685376cb7d41b | OnRenderImage:43, Update:65 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Bloom.cs | 69 | 1d40e665a8c89f59 | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_BlurHole.cs | 89 | fbc3cf95382c3ac8 | OnRenderImage:52, Update:78 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Blurry.cs | 82 | 5de527df62df17e5 | OnRenderImage:37, Update:71 | 0 | 2 | 0 | 1 | 1 | 6 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Dithering2x2.cs | 68 | 1fcf0fb667b38b83 | OnRenderImage:36, Update:57 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_DitherOffset.cs | 68 | ebb390f850460f4f | OnRenderImage:36, Update:57 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Focus.cs | 78 | f8c6cb7dd6dcd78f | OnRenderImage:43, Update:67 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_GaussianBlur.cs | 65 | 26141a1a6b16ce47 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Movie.cs | 85 | caec9288a2ff07d2 | OnRenderImage:40, Update:74 | 0 | 2 | 0 | 1 | 1 | 6 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Noise.cs | 68 | bbd5a8520f3712bd | OnRenderImage:36, Update:57 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Radial.cs | 77 | 355d66c1b314fd3c | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Radial_Fast.cs | 77 | 21b868bd17a43e06 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Regular.cs | 68 | 62b0b4eab713cb6e | OnRenderImage:36, Update:57 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Steam.cs | 69 | 68703b1d945fa076 | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Tilt_Shift.cs | 101 | 7c5c86ed6f43d731 | OnRenderImage:46, Update:90 | 0 | 2 | 0 | 1 | 1 | 11 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Tilt_Shift_Hole.cs | 104 | 890040b35dccfe2a | OnRenderImage:49, Update:93 | 0 | 1 | 0 | 1 | 1 | 11 |
| Assembly-CSharp-firstpass/CameraFilterPack_Blur_Tilt_Shift_V.cs | 100 | ca96292c74807819 | OnRenderImage:46, Update:89 | 0 | 1 | 0 | 1 | 1 | 11 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_BrightContrastSaturation.cs | 73 | c467780dbace0d97 | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Chromatic_Aberration.cs | 65 | 2ac6ebdaf05010ed | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Contrast.cs | 65 | ce1542a2cd244e98 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_GrayScale.cs | 65 | 85a17f1714ba8d54 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Invert.cs | 65 | 08d83b7dfe5cda84 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Noise.cs | 65 | 07fc7137c18035be | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_RGB.cs | 64 | 8ee49c4579bfed2b | OnRenderImage:33, Update:53 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Sepia.cs | 65 | 3718ddca6f3a9981 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_Switching.cs | 65 | 12b0d06dd23a81e0 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Color_YUV.cs | 73 | 9fdf44b744a5d664 | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Adjust_ColorRGB.cs | 77 | 723124429d8ba7a2 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Adjust_FullColors.cs | 110 | 113777c47c2d0982 | OnRenderImage:67, Update:98 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Adjust_PreFilters.cs | 385 | 543fedcdc2795361 | OnRenderImage:337, Update:374 | 0 | 34 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_BleachBypass.cs | 65 | 1c380e6038965435 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Brightness.cs | 56 | 5ce5bcab077ef222 | OnRenderImage:32, Update:45 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_DarkColor.cs | 77 | 8585053641e97de7 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_HSV.cs | 63 | 6af4f3325cabd56e | OnRenderImage:37, Update:52 | 0 | 1 | 0 | 1 | 0 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_HUE_Rotate.cs | 65 | 82a62d034478b0f8 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_NewPosterize.cs | 77 | 93bfc2213f32edc4 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_RgbClamp.cs | 93 | 49812ebb1f00a349 | OnRenderImage:55, Update:82 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Colors_Threshold.cs | 64 | 47390e4b10012c92 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Aspiration.cs | 77 | 2073450a82394fdf | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_BigFace.cs | 68 | 184041a25d717b24 | OnRenderImage:36, Update:57 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_BlackHole.cs | 77 | 0c952c9d9215f62a | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Dissipation.cs | 77 | 179dc8e8d40df58b | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Dream.cs | 64 | 253078701ae945b8 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Dream2.cs | 69 | 9d0049bcfd8c91d1 | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_FishEye.cs | 65 | a0ad3748ab17e020 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Flag.cs | 67 | e9aa41d026af903b | OnRenderImage:36, Update:56 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Flush.cs | 65 | e43d4b3d71c6e468 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Half_Sphere.cs | 77 | b4d0bfb99e003eb6 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Heat.cs | 65 | 044f29d5fb7cb2b1 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Lens.cs | 73 | b9313e38a517f5f4 | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Noise.cs | 65 | ecaa7a93b7b674bd | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_ShockWave.cs | 77 | 4f1949abd7c6812b | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Twist.cs | 77 | cc540af6cd60404e | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Twist_Square.cs | 77 | 75d9090820ab7743 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Water_Drop.cs | 77 | 7031f754ce1f4c0f | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Distortion_Wave_Horizontal.cs | 65 | 6573bc2f4b0531d1 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_BluePrint.cs | 98 | a04737461f48385c | OnRenderImage:59, Update:87 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_CellShading.cs | 69 | 3945878f55894a8e | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_CellShading2.cs | 73 | c7b51ab3992c28cb | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Comics.cs | 64 | c7124f5e2658a9b1 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Crosshatch.cs | 65 | ba77e9f84447b9f9 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Curve.cs | 65 | 090b852e54b962d5 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_EnhancedComics.cs | 91 | 37c05f88cb7c0a41 | OnRenderImage:54, Update:80 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Halftone.cs | 68 | 72498d189f809148 | OnRenderImage:37, Update:57 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Laplacian.cs | 62 | c588f092cba196e7 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Lines.cs | 77 | c4c191c5d4e77267 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga.cs | 64 | 9ab04c9078664d17 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga2.cs | 64 | 61157086b04da604 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga3.cs | 64 | d389d4433ce3d3cb | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga4.cs | 64 | 69eb9c76ca4d4ac6 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga5.cs | 64 | 6c931b5205c27b4a | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_Color.cs | 66 | f34f982bfc08333e | OnRenderImage:36, Update:55 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_Flash.cs | 81 | 529753768cd410e7 | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_Flash_Color.cs | 84 | fd5060050f517fa9 | OnRenderImage:48, Update:73 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Manga_FlashWhite.cs | 81 | 5008dab169cb1a99 | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_NewCellShading.cs | 65 | 22392c91e813e39b | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Paper.cs | 98 | 8b4b9a81279a5b3d | OnRenderImage:59, Update:87 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Paper2.cs | 98 | 0bb44aad00687070 | OnRenderImage:59, Update:87 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Paper3.cs | 98 | 750fe6e8c5e5fc9b | OnRenderImage:59, Update:87 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Drawing_Toon.cs | 68 | 5206cb4bd5595746 | OnRenderImage:37, Update:57 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_BlackLine.cs | 61 | fe8e29df045267a5 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Edge_filter.cs | 73 | 20b310a9093ca4c5 | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Golden.cs | 61 | 5ca706052b437b90 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Neon.cs | 65 | dfbc4d9b0bbaf4a0 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Sigmoid.cs | 65 | 429973153571b496 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Edge_Sobel.cs | 61 | 2dcccac4aa6ed9b4 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_EXTRA_Rotation.cs | 77 | 4ee7093790c685ce | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_EyesVision_1.cs | 80 | 1889fc920621630f | OnRenderImage:46, Update:69 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_EyesVision_2.cs | 80 | 7f2ca6ef8789b94c | OnRenderImage:46, Update:69 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Film_ColorPerfection.cs | 77 | 1a5643e392a80dfa | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Film_Grain.cs | 65 | 8575b369b0ee2903 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FlipScreen.cs | 64 | a3dc0ff3d8cf343b | OnRenderImage:33, Update:48 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Fly_Vision.cs | 81 | eb5a3213fa65620e | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_8bits.cs | 78 | e766da7e590a8a11 | OnRenderImage:40, Update:67 | 0 | 1 | 0 | 1 | 1 | 5 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_8bits_gb.cs | 72 | cb5ed0b1eec42da3 | OnRenderImage:34, Update:61 | 0 | 1 | 0 | 1 | 1 | 5 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Ascii.cs | 69 | d46940918c3fa701 | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_DarkMatter.cs | 85 | 5a7be0e074673bb5 | OnRenderImage:49, Update:74 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_DigitalMatrix.cs | 81 | 0af5fac33f61d8db | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_DigitalMatrixDistortion.cs | 73 | f75ea4fabb38f63c | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Dot_Circle.cs | 65 | a669e2591f89bbc0 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Drunk.cs | 106 | 82c328be36d28607 | OnRenderImage:65, Update:95 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Drunk2.cs | 77 | ddc249aa9a052719 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_EarthQuake.cs | 77 | f16f7c9b457034ff | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Funk.cs | 61 | e3732df38c39641d | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Glitch1.cs | 65 | 3220a3b64944ffda | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Glitch2.cs | 65 | ea01cf5d2586ace0 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Glitch3.cs | 69 | 01972c13da045c1c | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Grid.cs | 66 | cfc71b3e7021b3ce | OnRenderImage:36, Update:55 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Hexagon.cs | 61 | c3aa6e01d01c0fe2 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Hexagon_Black.cs | 65 | d8c81840baf57d1a | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Hypno.cs | 77 | 90e07142edfe34b5 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_InverChromiLum.cs | 61 | 294ded2e07810755 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Mirror.cs | 61 | 7279ef603f1e1c09 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Plasma.cs | 65 | adb934f21ff062a4 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Psycho.cs | 64 | 4a0d61fc514182b0 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Scan.cs | 77 | a3e4a0ff96d2c21f | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Screens.cs | 80 | 07d5f1f18a9b0050 | OnRenderImage:45, Update:69 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_Spot.cs | 68 | bdc41a77a4b188c0 | OnRenderImage:35, Update:57 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_superDot.cs | 61 | 9eb50b0abcee5dcb | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_FX_ZebraColor.cs | 65 | 2937143186412338 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Glitch_Mozaic.cs | 77 | 74912a2cd34e644c | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Glow_Glow.cs | 102 | 227d884a6f75140f | OnRenderImage:46, Update:91 | 0 | 2 | 0 | 1 | 1 | 12 |
| Assembly-CSharp-firstpass/CameraFilterPack_Glow_Glow_Color.cs | 105 | a3fe7dcda73417b6 | OnRenderImage:48, Update:94 | 0 | 3 | 0 | 1 | 1 | 12 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Ansi.cs | 71 | f8d341a6b8f81550 | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Desert.cs | 71 | f649f6aa77d3955e | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_ElectricGradient.cs | 71 | 06805be0beb4c2dc | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_FireGradient.cs | 71 | a502583796f4bc87 | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Hue.cs | 71 | dab13f8afa224483 | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_NeonGradient.cs | 71 | fa40d5b7ba1eb274 | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Rainbow.cs | 71 | d187c14334888c5d | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Stripe.cs | 71 | c23325245dfb21e5 | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Tech.cs | 71 | 710ee2aac68f1c84 | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Gradients_Therma.cs | 71 | dacf107038d6f61b | OnRenderImage:39, Update:60 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Light_Rainbow.cs | 65 | 4899499dc84db1ef | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Light_Rainbow2.cs | 65 | b82367a520262bb7 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Light_Water.cs | 76 | 88a94d95aeb6f635 | OnRenderImage:43, Update:65 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Light_Water2.cs | 77 | 628eadf5c3f36054 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_NightVision_4.cs | 96 | 651cd91defea5449 | OnRenderImage:48, Update:85 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_NightVisionFX.cs | 254 | 5012eb6b99a651bb | OnRenderImage:98, Update:133 | 0 | 13 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Noise_TV.cs | 81 | e6b5a6bf98349144 | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Noise_TV_2.cs | 81 | 3e05780b0da2697d | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Noise_TV_3.cs | 81 | 391ba83203c28270 | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision1.cs | 72 | ff056a4078425013 | OnRenderImage:39, Update:61 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision2.cs | 96 | 69a1b140708315a4 | OnRenderImage:48, Update:85 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision3.cs | 65 | 3979584dc5f80193 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_NightVision5.cs | 108 | e16b8366eda13a46 | OnRenderImage:57, Update:97 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Oculus_ThermaVision.cs | 77 | 4048358183290cb0 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_OldFilm_Cutting1.cs | 80 | b4211854828fbd21 | OnRenderImage:46, Update:69 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_OldFilm_Cutting2.cs | 80 | e5cc7802afe0719f | OnRenderImage:46, Update:69 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Pixel_Pixelisation.cs | 64 | 576963a0fcbf43a2 | OnRenderImage:38, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Pixelisation_Dot.cs | 77 | 13832da9a74377fc | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Pixelisation_OilPaint.cs | 65 | e62dcc26c8205910 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Pixelisation_OilPaintHQ.cs | 65 | d51dedf03f58aef8 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Real_VHS.cs | 98 | d6f85ebb2789f027 | OnRenderImage:62, Update:87 | 0 | 2 | 0 | 1 | 1 | 5 |
| Assembly-CSharp-firstpass/CameraFilterPack_Retro_Loading.cs | 65 | 99cfe98bf8d789dd | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Sharpen_Sharpen.cs | 69 | b7608ad73679667f | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Special_Bubble.cs | 77 | e2ae16960bd3d801 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_50.cs | 65 | 66154e2bc1c66e08 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_80.cs | 65 | 806e9a935803b468 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE.cs | 65 | 37acd00f91a00f76 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE_2.cs | 77 | aedb0e4b54154157 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE_3.cs | 104 | cde21388ba9d7e05 | OnRenderImage:55, Update:86 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_ARCADE_Fast.cs | 81 | 47777af94c5347a8 | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Artefact.cs | 77 | 1d692129aa70a5cf | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_BrokenGlass.cs | 84 | 971f5eeb144bd2d8 | OnRenderImage:49, Update:73 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_BrokenGlass2.cs | 208 | 33013fde175ffd70 | OnRenderImage:70, Update:197 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Chromatical.cs | 73 | 951bc47df7aba0b3 | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Chromatical2.cs | 77 | e6c9eac250265605 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_CompressionFX.cs | 65 | 0b367b2cd545d13a | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Distorted.cs | 68 | 761f7b3f297d0811 | OnRenderImage:37, Update:57 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Horror.cs | 73 | a7e489ca35a784ef | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_LED.cs | 73 | d4176656e9e2e92a | OnRenderImage:40, Update:62 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Noise.cs | 65 | 712a4cb4ea5d6ea5 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Old.cs | 64 | ea581d3af0cc6766 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Old_Movie.cs | 64 | 7ea91aa5b3e49778 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Old_Movie_2.cs | 81 | 6903d30a5cae1c17 | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_PlanetMars.cs | 69 | 21f35435586a09f8 | OnRenderImage:37, Update:58 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Posterize.cs | 68 | 5a2ac9dee357f162 | OnRenderImage:37, Update:57 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Rgb.cs | 65 | b6d2eafe1d65e50d | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Tiles.cs | 81 | 65f5d53b60762cfb | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Vcr.cs | 64 | e919f03a5d47e4c7 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_VHS.cs | 77 | fd1fdeab2feb9016 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_VHS_Rewind.cs | 77 | b703f0e8beab4c08 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Video3D.cs | 61 | 7e676ec1616bc0b1 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Videoflip.cs | 61 | 0b0b651edaa586c1 | OnRenderImage:31, Update:50 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_Vintage.cs | 64 | 212b48b006b58be0 | OnRenderImage:34, Update:53 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenCircle.cs | 77 | a1fbbb5f2d89eb58 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenHorizontal.cs | 77 | 4e1abe251e427359 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenHV.cs | 77 | 37000c1757fa2abb | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_TV_WideScreenVertical.cs | 77 | 2fb005b37a4da1e4 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_VHS_Tracking.cs | 65 | 199b90126d427101 | OnRenderImage:34, Update:54 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Aura.cs | 80 | 9e60d90f9e2ac897 | OnRenderImage:45, Update:69 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_AuraDistortion.cs | 80 | d8643a870685ce01 | OnRenderImage:45, Update:69 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Crystal.cs | 77 | 7f084d5893ad1ccf | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Drost.cs | 77 | 121642bd8b470431 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Plasma.cs | 77 | 0914113f332c643a | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Psycho.cs | 77 | 5128964b374a1c57 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Rainbow.cs | 81 | f9eb14555e383e5f | OnRenderImage:46, Update:70 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Tunnel.cs | 77 | cd29ebcec5b17704 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Warp.cs | 77 | 2f3c682bb45423d8 | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPack_Vision_Warp2.cs | 77 | 3768e89c3c12931d | OnRenderImage:43, Update:66 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_AAA_WaterDrop.cs | 108 | c107148b4a4e4505 | OnRenderImage:59, Update:90 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Atmosphere_Rain.cs | 94 | 6eb693a8023facfe | OnRenderImage:56, Update:83 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Atmosphere_Snow_8bits.cs | 78 | e1fec175c721b73c | OnRenderImage:44, Update:67 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blizzard.cs | 77 | 6317a310a64470f6 | OnRenderImage:44, Update:66 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blur_Blurry.cs | 100 | 74ec40001d591fdc | OnRenderImage:44, Update:84 | 0 | 2 | 0 | 1 | 1 | 6 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blur_Focus.cs | 106 | d3c05336ba1c61eb | OnRenderImage:56, Update:88 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Blur_GaussianBlur.cs | 83 | 783c23abf250c5e9 | OnRenderImage:38, Update:68 | 0 | 4 | 0 | 1 | 1 | 5 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Chromatic_Aberration.cs | 82 | baa839726028eade | OnRenderImage:42, Update:67 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Contrast.cs | 78 | c60e89b1e211e30d | OnRenderImage:38, Update:63 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_GrayScale.cs | 66 | 8dd6e795383a081f | OnRenderImage:35, Update:55 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Invert.cs | 62 | c47a8b15f9e244bd | OnRenderImage:32, Update:51 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Color_Sepia.cs | 66 | fafb09461b19aa07 | OnRenderImage:35, Update:55 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Distortion_FishEye.cs | 82 | fbeac124e6c64c95 | OnRenderImage:42, Update:67 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Distortion_Wave_Horizontal.cs | 82 | 49bd4b9567f44a0e | OnRenderImage:42, Update:67 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Drawing_Paper.cs | 99 | 025e2a4aa8b48eec | OnRenderImage:60, Update:88 | 0 | 3 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Edge_BlackLine.cs | 62 | 7deab0e687be516a | OnRenderImage:32, Update:51 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Edge_Neon.cs | 78 | 41ee86bd83bce5c1 | OnRenderImage:38, Update:63 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Film_Grain.cs | 82 | 3fc8f7999a3dbaef | OnRenderImage:42, Update:67 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_8bits_gb.cs | 85 | 24909a84d9a67c68 | OnRenderImage:38, Update:70 | 0 | 1 | 0 | 1 | 1 | 5 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_Funk.cs | 62 | 152ba7aecaf138f5 | OnRenderImage:32, Update:51 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_Glitch1.cs | 78 | 38889003aa7e0b84 | OnRenderImage:38, Update:63 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_Hexagon_Black.cs | 78 | 0e94e614694dab27 | OnRenderImage:38, Update:63 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_FX_superDot.cs | 62 | dc166cf8e2cc6d7c | OnRenderImage:32, Update:51 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Light_Water2.cs | 105 | 22594507f0c74df3 | OnRenderImage:56, Update:87 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Noise_TV.cs | 113 | 92d760d992eae0f2 | OnRenderImage:63, Update:95 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Oculus_NightVision1.cs | 80 | b04b5cabf7912652 | OnRenderImage:46, Update:68 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Pixel_Pixelisation.cs | 91 | 175a2d9fab9599cb | OnRenderImage:52, Update:74 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Pixelisation_OilPaint.cs | 78 | 2527bd32665f9957 | OnRenderImage:38, Update:63 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Real_VHS.cs | 85 | b561854356f8c273 | OnRenderImage:53, Update:74 | 0 | 2 | 0 | 1 | 1 | 5 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_Sharpen_Sharpen.cs | 87 | eabf69079d92240f | OnRenderImage:44, Update:71 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_50.cs | 62 | ccafb6137e36bea7 | OnRenderImage:32, Update:51 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_80.cs | 62 | 11c891c4ab025fc5 | OnRenderImage:32, Update:51 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_ARCADE.cs | 62 | 6d62683f44647033 | OnRenderImage:32, Update:51 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_CompressionFX.cs | 66 | 0a98aef0da373a5b | OnRenderImage:35, Update:55 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_LED.cs | 70 | d101ac7e54a00fea | OnRenderImage:38, Update:59 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_Posterize.cs | 77 | 45f92ccf75bf9a78 | OnRenderImage:38, Update:62 | 0 | 1 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraFilterPackLegacy_TV_Video3D.cs | 62 | 40f77e334e4d1ecc | OnRenderImage:32, Update:51 | 0 | 2 | 0 | 1 | 1 | 2 |
| Assembly-CSharp-firstpass/CameraOverlay.cs | 69 | b611bf22600ad0b1 | Update:35 | 3 | 4 | 8 | 0 | 12 | 0 |
| Assembly-CSharp-firstpass/ColorSuite.cs | 303 | c33b8b61e1997216 | OnRenderImage:254 | 1 | 6 | 0 | 1 | 0 | 1 |
| Assembly-CSharp-firstpass/CoroutineWithData.cs | 26 | 331045df1dd306e8 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/CubeMover.cs | 35 | 1734a9dfb1d8f520 | Update:22 | 0 | 0 | 4 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/CustomLightingManager.cs | 137 | f7fb18b9fcb93a42 | Update:133 | 3 | 1 | 0 | 1 | 1 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenAnimation.cs | 854 | 4e63fcf4a9c12c44 |  | 4 | 8 | 10 | 2 | 3 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenAnimationExtensions.cs | 11 | 2931cd90467a8f98 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenCYInstruction.cs | 138 | 29fe9ff6131de448 |  | 0 | 0 | 3 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenModuleAudio.cs | 111 | 3476fd67f4f73885 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenModuleSprite.cs | 60 | bc30d1251b2a897b |  | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenModuleUI.cs | 502 | 936c6ab87ef81d0a |  | 1 | 18 | 1 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenModuleUnityVersion.cs | 271 | c784705d08729f07 |  | 8 | 6 | 2 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenModuleUtils.cs | 60 | 415f0145b5c0970f |  | 0 | 0 | 1 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenProShortcuts.cs | 35 | fc5df91a684a0a9f |  | 0 | 1 | 2 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/DOTweenTMPAnimator.cs | 802 | 838172947a267564 |  | 8 | 12 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/ShortcutExtensionsTMPText.cs | 120 | 6bf8c3561835361a |  | 0 | 1 | 3 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DG.Tweening/TMPSkewSpanMode.cs | 7 | bf64c82d03002ff5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DifficultyUIMode.cs | 7 | 77b0f792f7f925d6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DirectionalLightManager.cs | 60 | 79d998173424b64f | Update:56 | 3 | 1 | 6 | 1 | 1 | 0 |
| Assembly-CSharp-firstpass/DisableRendererOnMouseDown.cs | 9 | 82807b68f64f8e10 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/Discord/AchievementManager.cs | 162 | 70469294dd53887c |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Activity.cs | 29 | 205279d084c4d16a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ActivityActionType.cs | 7 | ffa18b9ad4d53add |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ActivityAssets.cs | 18 | 3d4619003c048615 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ActivityJoinRequestReply.cs | 8 | a24efbfe181169be |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ActivityManager.cs | 286 | f0175cbad0f3eba7 |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ActivityParty.cs | 11 | 8d144f3a0bc73f0c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ActivitySecrets.cs | 15 | afe874b2a2dad44a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ActivityTimestamps.cs | 8 | 09b7ca212b364608 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ActivityType.cs | 9 | aba7d7e0505408ab |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ApplicationManager.cs | 149 | 550fd73f39326afc |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Constants.cs | 6 | 1f0311fc4a0d193c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/CreateFlags.cs | 7 | 28d9927c55802540 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Discord.cs | 485 | 2646e58020451d4e |  | 0 | 14 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Entitlement.cs | 10 | ceeae2104f6b36e3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/EntitlementType.cs | 12 | 4fc3268e0d686d4b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/FileStat.cs | 13 | 58b19276aa8351c9 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ImageDimensions.cs | 8 | 9f9de94caeb756cd |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ImageHandle.cs | 25 | 60befeb7e40446ae |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ImageManager.cs | 127 | 5cf34d0b32101f77 |  | 0 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ImageType.cs | 6 | 77a08768d755f513 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/InputMode.cs | 11 | 1916f7d978ea81a0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/InputModeType.cs | 7 | 6654538bca755e20 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Lobby.cs | 19 | 03434441426f5b0c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/LobbyManager.cs | 847 | 4ee40e9f3b7f84ad |  | 1 | 31 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/LobbyMemberTransaction.cs | 60 | 9b0c460091d9dc44 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/LobbySearchCast.cs | 7 | f7f92c6f8e28d5a3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/LobbySearchComparison.cs | 11 | bfb53c7339238473 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/LobbySearchDistance.cs | 9 | 907c73e168caf1f7 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/LobbySearchQuery.cs | 94 | 0fa0ebc3e613b8be |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/LobbyTransaction.cs | 128 | 0709caca11f24fa1 |  | 0 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/LobbyType.cs | 7 | 92a77e619eea4a4d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/LogLevel.cs | 9 | a58fa1ef02e14654 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/MonoPInvokeCallbackAttribute.cs | 7 | 41b0e69896aca17a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/NetworkManager.cs | 200 | 76f2b26dab6e51fa |  | 0 | 10 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/OAuth2Token.cs | 14 | a179ef39387f3ed5 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/OverlayManager.cs | 192 | e1d7c37de5d20f0a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/PartySize.cs | 8 | 4b3aa99415942fa9 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/PremiumType.cs | 8 | 40ea7701715e12f0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Presence.cs | 8 | 98c0f694df456066 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Relationship.cs | 10 | 9f9919f47cdb8487 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/RelationshipManager.cs | 159 | 6faaaa099af9fa38 |  | 0 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/RelationshipType.cs | 11 | 35ac6160db0e675a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Result.cs | 49 | 767a9ea8a035852e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/ResultException.cs | 13 | 2f6d316d0c010500 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Sku.cs | 15 | ffac9b20d6dbb265 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/SkuPrice.cs | 11 | 8586017a5fb7f5c4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/SkuType.cs | 9 | c1ef7f32a3fd9234 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/Status.cs | 9 | 59c9ccc57320ce62 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/StorageManager.cs | 261 | 9b754e4504d9a6dd |  | 1 | 13 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/StoreManager.cs | 290 | 3ce218764a31a1ae |  | 2 | 9 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/User.cs | 19 | 5bad5374127bf8a3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/UserAchievement.cs | 15 | 4c485baa079f0b6d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/UserFlag.cs | 10 | db32c39aa2b6b070 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/UserManager.cs | 141 | d3bda780477c86cd |  | 0 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Discord/VoiceManager.cs | 229 | 469fe25363307b6d |  | 0 | 11 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DragDirection.cs | 47 | 1dae54bc9920016c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/DragForce.cs | 46 | 806efb480c501634 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/EditModeGridSnap.cs | 25 | 274b6efd454832c0 | Update:14 | 0 | 1 | 4 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/EnableCameraDepthTexture.cs | 10 | bb7f19443e4817e2 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/FileDragAndDrop.cs | 47 | 661073324631b87a |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Fps.cs | 38 | 88bdfd53ea9b81a3 | OnGUI:34 | 1 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/FrameRate.cs | 13 | 79f14fb16225ce5c | Update:10 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/FreakLib.SelectParent/AlwaysSelectMyParent.cs | 8 | 8d2e2d7930507061 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/GDMiniJSON/Json.cs | 472 | bfd529549ae306ed |  | 9 | 10 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/GoogleDocConnectorEvalScript.cs | 12 | 4514b133242095f6 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/Kirnu/FloatingObject.cs | 11 | 409eadb14299d8d5 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Kirnu/MarvelousBloom.cs | 83 | 5dc6b66dd6b2bab5 | OnRenderImage:41 | 1 | 3 | 0 | 0 | 0 | 11 |
| Assembly-CSharp-firstpass/Kirnu/OceanCreator.cs | 85 | b782011a7c131fe2 |  | 5 | 13 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Kirnu/OceanWaver.cs | 105 | d90500c1425ebb79 | Update:87 | 5 | 4 | 4 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/Kirnu/ScreenTextureBlend.cs | 113 | 1b01ee2f678ff0fb | OnRenderImage:58 | 0 | 0 | 0 | 0 | 0 | 2 |
| Assembly-CSharp-firstpass/MD5Hash.cs | 16 | cf50800811e1ec33 |  | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp/Buffer16BitStereo.cs | 111 | 0baceb5976a3e503 |  | 2 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp/MP3SharpException.cs | 46 | cb43d7df33cee5bb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp/MP3Stream.cs | 173 | 7207531506f44cde |  | 2 | 8 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp/SoundFormat.cs | 7 | 8aec584aa32864fe |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/ABuffer.cs | 39 | 86b1f4c4a763e2b1 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/BitReserve.cs | 110 | 4449259195225bfe |  | 2 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/Bitstream.cs | 328 | 0f96dbef718ec78f |  | 5 | 9 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/BitstreamErrors.cs | 23 | 3a6012a19c51bf7e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/BitstreamException.cs | 51 | 20cef255ef94af03 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/CircularByteBuffer.cs | 181 | 1fc0d0bcb4292cf7 |  | 8 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/Crc16.cs | 45 | 6ced838fa28a3b60 |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/Decoder.cs | 207 | 92ae8683b5d021e9 |  | 0 | 12 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/DecoderErrors.cs | 11 | 2c7d08e1a92f9323 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/DecoderException.cs | 51 | e315971f5e4d2f0c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/Equalizer.cs | 152 | 86c65b93f47278f2 |  | 4 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/Header.cs | 572 | 8570dc5f43c594b3 |  | 1 | 36 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/Huffman.cs | 3066 | 8c7958a986e569e5 |  | 3 | 2859 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/OutputChannels.cs | 74 | c660444528d73a72 |  | 0 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/OutputChannelsEnum.cs | 9 | 0a13d61e20d3d6fc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/PushbackStream.cs | 65 | 02d4e12bac04ee4c |  | 2 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/SampleBuffer.cs | 73 | 06c553f09517fb82 |  | 3 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding/SynthesisFilter.cs | 1072 | 73797d1002300d76 |  | 24 | 12 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders/ASubband.cs | 80 | ed17c62ef7b55e20 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders/IFrameDecoder.cs | 6 | 66b71f868a07e7c2 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders/LayerIDecoder.cs | 134 | abe1385b514001f9 |  | 10 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders/LayerIIDecoder.cs | 44 | c87681ace46217e1 |  | 5 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders/LayerIIIDecoder.cs | 2048 | cbc78c1f8d7ae74c |  | 93 | 106 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerI/SubbandLayer1.cs | 109 | 77c6d3d6ce6b755c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerI/SubbandLayer1IntensityStereo.cs | 56 | 704094f18ae48a28 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerI/SubbandLayer1Stereo.cs | 96 | 53a5d674687f6718 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerII/SubbandLayer2.cs | 2740 | 3f3c648d9c34bc93 |  | 0 | 32 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerII/SubbandLayer2IntensityStereo.cs | 116 | 642bd3835d87e60f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerII/SubbandLayer2Stereo.cs | 134 | 75baae103da9e264 |  | 0 | 5 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerIII/ChannelData.cs | 16 | 89203366bf6d2169 |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerIII/GranuleInfo.cs | 38 | dcb106b154123d30 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerIII/Layer3SideInfo.cs | 17 | e32190fec2ec20ea |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerIII/SBI.cs | 20 | c730663343e0f4bc |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerIII/ScaleFactorData.cs | 18 | 1c8f70ec98b5edf8 |  | 1 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Decoding.Decoders.LayerIII/ScaleFactorTable.cs | 31 | 6e7f3be34c2f01b7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.IO/RandomAccessFileStream.cs | 21 | 8ec526592c8a0c11 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.IO/RiffFile.cs | 520 | ed99d5e21e505704 |  | 2 | 11 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.IO/WaveFile.cs | 221 | d956eac913642534 |  | 0 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.IO/WaveFileBuffer.cs | 77 | 428581a28ed9721e |  | 3 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/MP3Sharp.Support/SupportClass.cs | 104 | 18144a74b3259c71 |  | 4 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Newgrounds/API.cs | 313 | a7fa924a64192dd3 | Update:156 | 5 | 11 | 0 | 0 | 10 | 0 |
| Assembly-CSharp-firstpass/Newgrounds/APIEvent.cs | 47 | 40f68793b6c0b2a1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Newgrounds/APIEventList.cs | 20 | 0d3f1c91a2cd300f |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Newgrounds/JSONCollection.cs | 194 | eb171453d9b09e3a |  | 8 | 5 | 0 | 0 | 3 | 0 |
| Assembly-CSharp-firstpass/Newgrounds/JSONEntry.cs | 18 | 8fa322c3d9fe89f4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Newgrounds/Main.cs | 59 | d907cbc2deb5216f | OnGUI:43 | 1 | 3 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/Newgrounds/Medal.cs | 78 | cc52bae12e018ec3 |  | 0 | 0 | 0 | 0 | 8 | 0 |
| Assembly-CSharp-firstpass/Newgrounds/SendString.cs | 37 | 7caaaa0be76e019a |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/ObjectRotator.cs | 249 | 857fa13a91954db9 |  | 1 | 2 | 13 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/ArrayExtensions.cs | 28 | 29e97dc41d4817c7 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/Block.cs | 8000 | 08a30d163bceb915 |  | 0 | 9 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/CodecSetup.cs | 32 | 7de62dfeedde9266 |  | 0 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/Comments.cs | 20 | 2db817ace32813b2 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/EncodeBuffer.cs | 101 | 04738d97e89422a0 |  | 1 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/EncodeSetup.cs | 107 | 12eec446582d7d87 |  | 2 | 19 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/Encoding.cs | 111 | c279938405057d07 |  | 7 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/FloatExtensions.cs | 11 | 1deeb44d8006be22 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/HeaderPacketBuilder.cs | 334 | 0af3ae381c495cf4 |  | 24 | 8 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/LookupCollection.cs | 73 | 6ca19e7358524f34 |  | 4 | 16 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/OffsetArray.cs | 84 | ec8fc43d3baf71e6 |  | 0 | 9 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/OggPacket.cs | 20 | d14671ee0a30468c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/OggPage.cs | 14 | 2fec7088d914d0a0 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/OggStream.cs | 277 | 55d2325381e1e982 |  | 10 | 9 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/ProcessingState.cs | 520 | 37ff0dec53e62f10 |  | 32 | 28 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/StaticCodeBookExtensions.cs | 28 | b8f94c75528d2b21 |  | 2 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder/VorbisInfo.cs | 451 | 4c79fa4c7ffae445 |  | 25 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Example/Encoder.cs | 188 | 611c19aa8d5a93d6 |  | 12 | 11 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/Delta.cs | 14 | 60780cb0f1d41431 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/DrftLookup.cs | 653 | 5105e9cccf233be1 |  | 52 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/EnvelopeBand.cs | 24 | a73c239254debfd3 |  | 1 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/EnvelopeFilterState.cs | 74 | 04ab54a9b078742c |  | 1 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/EnvelopeLookup.cs | 222 | a941479ca6515243 |  | 9 | 14 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/FloorLookup.cs | 673 | e58a7d3c2028ff2d |  | 29 | 17 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/MdctLookup.cs | 366 | d42a65cdff9ce17b |  | 14 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/PsyGlobalLookup.cs | 37 | 1a86d014ad6254a8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/PsyLookup.cs | 2037 | cbe449cee736880b |  | 71 | 159 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Lookups/ResidueLookup.cs | 323 | f9c3124947807c4d |  | 29 | 10 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/AdjBlock.cs | 11 | ea60fa892eca0681 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/AdjStereo.cs | 20 | 01ffca7d644a2baf |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/Att3.cs | 17 | 5101438f42db99e3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/CodeBook.cs | 57 | 4e879e1b62ab3c68 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/CodeBookMapType.cs | 8 | d63caab63d80bc36 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/CompandBlock.cs | 11 | 1ceb03b99e7ab6d9 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/Floor.cs | 54 | b9a1a2008da8aeee |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/IMappingTemplate.cs | 8 | a9fec4023579d6a8 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/IResidueTemplate.cs | 20 | 6c25ef75e45c6e3c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/ISetupTemplate.cs | 76 | 79f9fa1b65ff6c7c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/IStaticBookBlock.cs | 6 | c0a1735a7aaaba2d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/IStaticCodeBook.cs | 20 | f607bf77d87873b6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/Mapping.cs | 45 | 7436a9f06dbdb5d2 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/MappingTemplate.cs | 14 | 4bc8350cd24e8409 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/Mode.cs | 20 | a7e343d64c26c76e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/Noise3.cs | 11 | 9b587122d13352c1 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/NoiseGuard.cs | 17 | 303d1af1d6aefce4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/PsyGlobal.cs | 52 | 81ebab50db044ad1 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/PsyInfo.cs | 137 | 87df8015d706073d |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/ResidueEntry.cs | 46 | 0bbfad60ba66806b |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/ResidueLimitType.cs | 8 | 61aea9c53b80b733 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/ResidueTemplate.cs | 32 | af8ff188328e6db9 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup/ResidueType.cs | 8 | 0bff2513143bd9f3 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Psy.cs | 933 | e12300ba64948af6 |  | 0 | 208 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/SharedFloors.cs | 222 | 11f8ccc4ae5107be |  | 0 | 153 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Stereo11SetupDataTemplate.cs | 87 | f8ac125a0bb90d1a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Stereo16SetupDataTemplate.cs | 87 | 73f3942c0c5e0ff7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Stereo22SetupDataTemplate.cs | 87 | c227b9622ed49734 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Stereo32SetupDataTemplate.cs | 87 | b138b955c02aad45 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Stereo44SetupDataTemplate.cs | 87 | 37161cc588b02cb9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Stereo8SetupDataTemplate.cs | 87 | 1aba7941db01647d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/StereoXSetupDataTemplate.cs | 87 | fed3bac9e54bad88 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/StereoXXSetupDataTemplate.cs | 87 | 6bbdfdca9347f07e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Uncoupled11SetupDataTemplate.cs | 87 | 9bc68268b62d21a2 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Uncoupled16SetupDataTemplate.cs | 87 | 02899a36e7c94a6a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Uncoupled22SetupDataTemplate.cs | 87 | acae07975ebdc5c7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Uncoupled32SetupDataTemplate.cs | 87 | 4ce03eb2ff8932b0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Uncoupled44SetupDataTemplate.cs | 87 | 0526dc1a83951d31 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/Uncoupled8SetupDataTemplate.cs | 87 | e32c4911a391aa8f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/UncoupledXSetupDataTemplate.cs | 87 | 7412388d4b305045 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates/UncoupledXXSetupDataTemplate.cs | 87 | 02a33150360641b3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled/Blocks.cs | 176 | 6ce2cf770d354e6b |  | 0 | 72 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Chapter0Single.cs | 32 | 14f3319ce4c46359 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page1_0.cs | 679 | fcbba64d729eb56d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page3_0.cs | 85 | 47bce06fdf0eddc4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page4_0.cs | 31 | a16a11755798aac5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page5_0.cs | 31 | 63aa06d573bc16ff |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page6_0.cs | 55 | 646cc9ca494232b8 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page7_0.cs | 31 | 5f405adcf5701873 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page7_1.cs | 39 | e304f2b617d405d5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page8_0.cs | 43 | 353f12cdcf91770f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page8_1.cs | 25 | 6f613a4ac5169f81 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page9_0.cs | 31 | b113c135b4908093 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page9_1.cs | 49 | c2defaac00edc343 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter0/Page9_2.cs | 72 | 976293608b352f29 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Chapter1Long.cs | 32 | 8872a71463e5f4f0 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Chapter1Short.cs | 32 | a806c01af0fcfbe7 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page1_0.cs | 679 | b848a52319af1e89 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page3_0.cs | 85 | cabe8bb460db5afd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page4_0.cs | 31 | 762374d0003e7bcc |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page5_0.cs | 31 | b2b9576973dd4560 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page6_0.cs | 55 | c7c95c9898364583 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page7_0.cs | 31 | 00422362f03328a6 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page7_1.cs | 39 | 55210d21e0cc1a14 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page8_0.cs | 43 | 0374ef657e864aa2 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page8_1.cs | 25 | bd0662eb7ca47f70 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page9_0.cs | 43 | 3a7a51cb5707574f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page9_1.cs | 49 | e75a124b0e3d2455 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter1/Page9_2.cs | 72 | c7075cbd72005b16 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Chapter2Long.cs | 32 | b36e1043091b2de2 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Chapter2Short.cs | 32 | 1fd85d7e82983f3a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page1_0.cs | 31 | 3ce202743f0ee81f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page2_0.cs | 85 | 7334a7fc3992a775 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page3_0.cs | 31 | d996669d7cabb6a7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page4_0.cs | 55 | cac8e51ef5fdaf7a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page5_0.cs | 31 | 26076187e755a846 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page5_1.cs | 39 | 5a7bddf4ef88a1ea |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page6_0.cs | 43 | 2775ac647518f57b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page6_1.cs | 25 | 91c341f0e88acdf0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page7_0.cs | 43 | 07d9b50a9451941a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page7_1.cs | 39 | 9821a683f3368ede |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page8_0.cs | 49 | 15c79bc95b5a9515 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page8_1.cs | 72 | 0279145b1120feac |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page9_0.cs | 55 | 98ce3141ef9bcbaa |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page9_1.cs | 63 | 559f8c025e6f0340 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Coupled.Chapter2/Page9_2.cs | 34 | b6371040453a794c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled/Blocks.cs | 175 | 317c2ef31843db87 |  | 0 | 69 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Chapter0Single.cs | 29 | c655ba3ba9f001d7 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page1_0.cs | 31 | 5a072e2119f10222 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page2_0.cs | 31 | b5f04c26b9d2b0a0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page3_0.cs | 85 | 4f91551ed6d838d3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page4_0.cs | 85 | b12e4740f2a8428c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page5_0.cs | 31 | 456f20391aeb30e0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page6_0.cs | 43 | dffecb7a25d8b288 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page6_1.cs | 25 | c2c9ab0949a7e5e5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page7_0.cs | 31 | 879392b07fa64a58 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page7_1.cs | 49 | 8f936b6ac7d2f272 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter0/Page7_2.cs | 72 | 18c3cc6877fb0298 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Chapter1Long.cs | 32 | e3cda4ba4fa3a913 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Chapter1Short.cs | 32 | b5ba4c41fe94c7f7 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page1_0.cs | 31 | b4c77ddc111226a6 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page2_0.cs | 31 | 095c660b38a24ca4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page3_0.cs | 85 | 78d54e067fcfac38 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page4_0.cs | 85 | 27b45c21cb91d197 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page5_0.cs | 31 | 8b50767bc1f9b057 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page6_0.cs | 31 | ce68b56bf50ee990 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page7_0.cs | 31 | 74581af5787f7ed0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page7_1.cs | 39 | 340761d05f89f758 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page8_0.cs | 39 | 4bec76c1521f2fb3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page8_1.cs | 39 | cb0b825596e86355 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page9_0.cs | 49 | 7180b9e88d773d1f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page9_1.cs | 49 | 3c43c87b7c7c6844 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter1/Page9_2.cs | 55 | 19597e673b12082e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Chapter2Long.cs | 32 | 4068d9a25a08cbaf |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Chapter2Short.cs | 32 | 1812750b01764df6 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page1_0.cs | 31 | 3bbae5ea8497b827 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page2_0.cs | 85 | b03b1e93a2d8f403 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page3_0.cs | 31 | 61ed4a4cf905f07e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page4_0.cs | 55 | 548f09d03e50480c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page5_0.cs | 31 | fb45dc08dd561cf3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page5_1.cs | 39 | 3613274077d1ed60 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page6_0.cs | 43 | 01393bec616fb293 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page6_1.cs | 25 | b65034d5bf52ba14 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page7_0.cs | 43 | ac076efdfe2f80b0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page7_1.cs | 39 | 9964435dac848c0a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page8_0.cs | 49 | f71758bec5bf668a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page8_1.cs | 72 | 67fdeaa140469025 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page9_0.cs | 49 | 63346910055787cc |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page9_1.cs | 63 | 026f3e7a7b70db51 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo16.Uncoupled.Chapter2/Page9_2.cs | 34 | b0b00b495436ab81 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled/Blocks.cs | 815 | f0926d1d19bb22aa |  | 0 | 332 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Chapter0Long.cs | 31 | 10fb2c24203e8d71 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Chapter0Short.cs | 31 | 98313bef68013a92 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page1_0.cs | 679 | 36ef3f6e08e5a508 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page2_0.cs | 85 | 8fbb696f5d81ef9b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page3_0.cs | 31 | 40efb86d52e2160b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page4_0.cs | 31 | 3ea24058430c876a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page5_0.cs | 55 | 818364b5e20ccb6a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page6_0.cs | 31 | 741ac025bc199db5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page6_1.cs | 39 | 79fc9eed22560fc4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page7_0.cs | 43 | 9807f262c25ddaed |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page7_1.cs | 25 | 38b3e5f886fa40a7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page8_0.cs | 85 | 1e45e6be8152a1b4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page8_1.cs | 43 | 69aa7a7966d744f9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter0/Page8_2.cs | 55 | 64180afb80d337d5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Chapter1Long.cs | 31 | 933268d59af994fe |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Chapter1Short.cs | 31 | ef53e7b2a07df32e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page1_0.cs | 679 | c704e4f66b1b82a2 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page2_0.cs | 85 | 70acc7f47bd21f85 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page3_0.cs | 31 | 37bd9dbe0de174e1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page4_0.cs | 31 | 50b49c5c03e4af4d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page5_0.cs | 55 | c1632b2f89de4802 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page6_0.cs | 31 | e7f4ef400ebff3dd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page6_1.cs | 39 | f69f462e73a2e122 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page7_0.cs | 43 | 39f0c2c588c43a0e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page7_1.cs | 25 | c47be785e20b6dd3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page8_0.cs | 43 | 8409919e99a2d975 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page8_1.cs | 43 | 9039ae1bc3667b57 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter1/Page8_2.cs | 55 | 41840a0fd06a85d7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Chapter2Long.cs | 32 | f26c85c4de41b118 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Chapter2Short.cs | 32 | 33156887d81b2698 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page1_0.cs | 679 | cf480e5cb3ebb2f4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page2_0.cs | 85 | e40514d25dc92c80 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page3_0.cs | 85 | 2278980b27249553 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page4_0.cs | 31 | 0612450aea75ea01 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page5_0.cs | 31 | 45c21ba91a08ecf7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page6_0.cs | 55 | 2a6b750f6095cd3d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page7_0.cs | 31 | 3cc9fad2d49cb2cb |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page7_1.cs | 39 | 8aeb28333dcd1d77 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page8_0.cs | 43 | e759e2f3403646e0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page8_1.cs | 25 | 6b227ef7cfa3efaf |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page9_0.cs | 43 | 13b02dfe89a3eeb9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page9_1.cs | 43 | 976867708cd94e16 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter2/Page9_2.cs | 55 | 792fdcd9105d341d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Chapter3Long.cs | 32 | a9da806c0dae9cca |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Chapter3Short.cs | 32 | a505de007fa25f6c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page1_0.cs | 679 | 8a0e4d492582621e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page2_0.cs | 85 | abda32afe17a0dca |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page3_0.cs | 85 | f5ae0036696002c2 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page4_0.cs | 31 | 389cf59d1168d093 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page5_0.cs | 31 | 869386ab22205c92 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page6_0.cs | 55 | ca3b56e518a3ac1d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page7_0.cs | 31 | ecdf18bf98079595 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page7_1.cs | 39 | 163a1f6c973b7c3b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page8_0.cs | 43 | acde636531b1fb27 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page8_1.cs | 25 | b35ee08858f5ef06 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page9_0.cs | 43 | 94ec01eb81800928 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page9_1.cs | 49 | df4cc50d1966f862 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter3/Page9_2.cs | 55 | 4bb239686ca9ef1c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Chapter4Long.cs | 32 | 1206fb01fe11b9af |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Chapter4Short.cs | 32 | a2a50639a8dc12b6 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page1_0.cs | 679 | a4f11e91bb1bd87a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page2_0.cs | 85 | 1d044dbb76971f97 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page3_0.cs | 85 | 25ec1a29a5d8f0cf |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page4_0.cs | 31 | f35f50de748c6a45 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page5_0.cs | 31 | 67c0110752c4a110 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page6_0.cs | 55 | 82e66e52e188de78 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page7_0.cs | 31 | 7a368e91cdc89401 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page7_1.cs | 39 | 33c53e0d6765e71e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page8_0.cs | 43 | 1cecfa15aeff8725 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page8_1.cs | 25 | d5aa03a1112b1ca6 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page9_0.cs | 43 | b5685e8a7c94e25e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page9_1.cs | 49 | 6927430e727e104f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter4/Page9_2.cs | 72 | 2ebdec4c1a419587 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Chapter5Long.cs | 32 | 0a176d5704832bdf |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Chapter5Short.cs | 32 | 35a80d8c46147984 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page1_0.cs | 679 | 734ac4e9127add01 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page2_0.cs | 85 | e73cf2aa605dc40b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page3_0.cs | 85 | f6711c0fa63ac086 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page4_0.cs | 31 | 4edb03f5f2675abc |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page5_0.cs | 31 | 59d54cecddb7e204 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page6_0.cs | 55 | e418806afc14ac9b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page7_0.cs | 31 | db79e67836f955df |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page7_1.cs | 39 | 4d9c4036648d34bb |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page8_0.cs | 43 | af62ea28f1431f68 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page8_1.cs | 25 | a822512cdf0a6a6b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page9_0.cs | 49 | 15ca572f574937a5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page9_1.cs | 55 | 455fcbc74e818d9c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter5/Page9_2.cs | 72 | 66ba2433788585b3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Chapter6Long.cs | 32 | a941507ad19fa905 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Chapter6Short.cs | 32 | f056b4248b3b4f63 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page1_0.cs | 31 | 7c17d0fc0eabb5e9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page2_0.cs | 85 | 21d53f63ac619a65 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page3_0.cs | 31 | 49a711e5100b66fe |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page4_0.cs | 55 | cf6ecf15eb63ae95 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page5_0.cs | 31 | cc90f6d912ffd8ae |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page5_1.cs | 39 | a636554505568b24 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page6_0.cs | 43 | bbee218b684cc435 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page6_1.cs | 25 | d916d9ba4ad777a0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page7_0.cs | 43 | 4be40bef0ac631dd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page7_1.cs | 39 | 83919c2c38df0e77 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page8_0.cs | 49 | 701a58a285897714 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page8_1.cs | 72 | f153cf44ca56cbb7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page9_0.cs | 43 | 98a94aaf0a63ef2e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page9_1.cs | 43 | 5c4183f9b972682a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter6/Page9_2.cs | 34 | 185ac1b325d62ea8 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Chapter7Long.cs | 32 | 4aab36aeef8e9e46 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Chapter7Short.cs | 32 | e09b99a679ec39d5 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page1_0.cs | 31 | 487fa03a147d1b26 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page2_0.cs | 85 | be39ad5a3980427e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page3_0.cs | 31 | c714ff18d8fc769e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page4_0.cs | 55 | ac7b6daf127bb4a5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page5_0.cs | 31 | d8a14cd579287833 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page5_1.cs | 39 | a64a8ceb41f295f3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page6_0.cs | 43 | 73e0ba797c46ff84 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page6_1.cs | 25 | 88572656d188eb54 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page7_0.cs | 43 | 4bdbd9d46d3fb18c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page7_1.cs | 39 | c26f02bdd75a3638 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page8_0.cs | 49 | 79f1e9f1503714c0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page8_1.cs | 72 | 3d4794c9bcc53d85 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page9_0.cs | 43 | f921cf4baa43ee84 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page9_1.cs | 43 | 15103c2114c73ad6 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter7/Page9_2.cs | 34 | 69fc0fc266cc621d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Chapter8Long.cs | 32 | 1be396b33a5c9d3c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Chapter8Short.cs | 32 | 14cba69af6359dbf |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page1_0.cs | 31 | f05167e3fd8de18b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page2_0.cs | 85 | 70996842e5a9ee1c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page3_0.cs | 31 | 6d4c3c1a7add4fb5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page4_0.cs | 55 | 78c1878ff0a02579 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page5_0.cs | 31 | b5bbe223d2826e6a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page5_1.cs | 39 | 1b7a4eeaba3f87ed |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page6_0.cs | 43 | 2fd7b6b3f140cd03 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page6_1.cs | 25 | a20ed2875054f6bb |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page7_0.cs | 43 | f64770b225ea88fa |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page7_1.cs | 39 | 50884da4da3e1753 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page8_0.cs | 49 | d80f81d82ce47501 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page8_1.cs | 72 | 078086fd5a7aff92 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page9_0.cs | 55 | c1bb06e76ebddb2f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page9_1.cs | 63 | b5055b2289cc8747 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter8/Page9_2.cs | 34 | a5c866616780a484 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Chapter9Long.cs | 32 | e240202dc6613c88 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Chapter9Short.cs | 32 | 04da7ce5c8f8c3a8 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page1_0.cs | 31 | 851397b3539ba09f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page2_0.cs | 85 | f8d1b6d0b1f9aff9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page3_0.cs | 31 | 5baaf847baaafaca |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page4_0.cs | 55 | b5d86b9b83c8d1ca |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page5_0.cs | 31 | bcd4330f8a8191f7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page5_1.cs | 39 | 43e66e348a2a0e47 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page6_0.cs | 43 | 5f00f8dcafe76a84 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page6_1.cs | 25 | 1a26c466bce75289 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page7_0.cs | 43 | 9cf69b52ebb8cf84 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page7_1.cs | 39 | 3a7fc252a3658173 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page8_0.cs | 49 | 759f92a4ffbc6cc0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page8_1.cs | 72 | 36ea44c2de5b0eb1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page9_0.cs | 63 | b5e3bb580f79a98a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page9_1.cs | 63 | 70c9d3360c6f522a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.Chapter9/Page9_2.cs | 34 | b5e16745f76e49da |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/ChapterNeg1Long.cs | 31 | ce4b7a9d8c858518 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/ChapterNeg1Short.cs | 31 | d0856184774fc09d |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page1_0.cs | 679 | ce00f8323d3575b9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page2_0.cs | 85 | 99eb88f6fb8c85bf |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page3_0.cs | 31 | 20fff307a8558731 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page4_0.cs | 31 | 50a7af10a3a5d489 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page5_0.cs | 55 | 6a89277cf4f62950 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page6_0.cs | 31 | 4ec6063b8f41500b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page6_1.cs | 39 | 35529e3995fc7a73 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page7_0.cs | 43 | 4070797292ebb5e7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page7_1.cs | 25 | 465d173b862c28d4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page8_0.cs | 85 | 97ca4731d1c22c79 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page8_1.cs | 43 | cb34608e4101e57b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ChapterNeg1/Page8_2.cs | 55 | 058e5c26dbf252a5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/ManagedChapter0Long.cs | 31 | 1e9095ff9f2f8dff |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/ManagedChapter0Short.cs | 31 | 072e3432fcb52e86 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page1_0.cs | 679 | acf1265c8cde5ff4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page2_0.cs | 85 | 98ea16e166265079 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page3_0.cs | 31 | 7c1c632ce1da2746 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page4_0.cs | 31 | 46e3de9d9ed9d02c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page5_0.cs | 55 | 2877e873cf7820ee |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page6_0.cs | 31 | e811ba6fc6f0e53e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page6_1.cs | 39 | 5a5b8e9c47321a63 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page7_0.cs | 43 | 49eb2f2c813a2dd4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page7_1.cs | 25 | f12ee5617dac8834 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page8_0.cs | 31 | 8849be042d0465c3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page8_1.cs | 43 | 930df40fca82676c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter0/Page8_2.cs | 55 | e7ef5969a3420987 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/ManagedChapter1Long.cs | 31 | c256107b7a7836db |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/ManagedChapter1Short.cs | 31 | 5edc6ea531ba2c18 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page1_0.cs | 679 | e9f0ed72d518e04f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page2_0.cs | 85 | 0664d1b666dc9518 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page3_0.cs | 31 | dfe085410c62de69 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page4_0.cs | 31 | fdcb1fdffd0abea9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page5_0.cs | 55 | 818ac4eae2b13ab9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page6_0.cs | 31 | 99acd24341ce8c4b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page6_1.cs | 39 | b77fd22ee0f48145 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page7_0.cs | 43 | 763c1e2f6b2983da |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page7_1.cs | 25 | c0822c4315181728 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page8_0.cs | 43 | fc08eca5715f2373 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page8_1.cs | 43 | f1ed8edb13b340c7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapter1/Page8_2.cs | 55 | fb8fc36bb8bc2b3a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/ManagedChapterNeg1Long.cs | 31 | 4c55605c25a3807a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/ManagedChapterNeg1Short.cs | 31 | 15a39dc0dc4a150b |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page1_0.cs | 679 | 1510f01307f676e1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page2_0.cs | 85 | 8cdc26c2fcfc714c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page3_0.cs | 31 | aa08149ff44606b2 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page4_0.cs | 31 | 9b4b21567887000c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page5_0.cs | 55 | 1f53f7f136dcbdde |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page6_0.cs | 31 | dc2beaa61e3949ed |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page6_1.cs | 39 | abb656205cf2495c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page7_0.cs | 43 | 2675a4fb6ddf174f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page7_1.cs | 25 | bae24e845ac2365f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page8_0.cs | 31 | 0d152ef7443e2c56 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page8_1.cs | 43 | 9c364a04e5a91747 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Coupled.ManagedChapterNeg1/Page8_2.cs | 55 | 81ab2fe0fcf700b1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled/Blocks.cs | 606 | e73579d745ce02ae |  | 0 | 238 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Chapter0Long.cs | 29 | 1c2722ba1a15e058 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Chapter0Short.cs | 29 | 6b71ba2b1ce1f9b2 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page1_0.cs | 31 | 450f0f05e65ac8ce |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page2_0.cs | 31 | 3f4ee8210a055027 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page3_0.cs | 85 | 3427a361bb21b93a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page4_0.cs | 85 | e93fafc8490d5dd0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page5_0.cs | 31 | 8f906c733b2d52c0 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page6_0.cs | 43 | f2418e0d18a42055 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page6_1.cs | 25 | 9244f7392fd3ec8a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page7_0.cs | 85 | c0f0d485f4875f44 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page7_1.cs | 43 | 1dfd8df1ed1e3aba |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter0/Page7_2.cs | 43 | 296e7d2fa78d271b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Chapter1Long.cs | 29 | 0dd3f676f1f58810 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Chapter1Short.cs | 29 | 1205dd581b35e4fa |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page1_0.cs | 31 | d258ae1dd897e9fd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page2_0.cs | 31 | f3c191d4cdf220fc |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page3_0.cs | 85 | b92c2eb347bf0ecc |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page4_0.cs | 85 | 1aab23ddb49fb993 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page5_0.cs | 31 | c492bcbf92bc8a75 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page6_0.cs | 43 | d6c30ca5b0ad232a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page6_1.cs | 25 | f128a5032dcbe434 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page7_0.cs | 27 | f7db09d3e9c24448 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page7_1.cs | 43 | 04c1b8d25347974f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter1/Page7_2.cs | 43 | ac593824508a499e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Chapter2Long.cs | 29 | 0499c5ab40defd99 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Chapter2Short.cs | 29 | 7e77472cf4441154 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page1_0.cs | 31 | 8529d9fcfe5cc4cd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page2_0.cs | 31 | 6da156df9f9293ab |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page3_0.cs | 85 | e3140935e1f1f480 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page4_0.cs | 85 | ab3639e5797db8e1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page5_0.cs | 31 | 08552c666364e6c1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page6_0.cs | 43 | 7c89025ead6f41ac |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page6_1.cs | 25 | 30916ecdeeb896a3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page7_0.cs | 31 | cf85f06c2a36b967 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page7_1.cs | 43 | 0a64d46d88424707 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter2/Page7_2.cs | 43 | 0ac1f3d2285a355f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Chapter3Long.cs | 29 | 3906b870276e552f |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Chapter3Short.cs | 29 | f6a573edb542c7f5 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page1_0.cs | 31 | 4a6d3d331c7e8484 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page2_0.cs | 31 | 9d16a300eb34d76e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page3_0.cs | 85 | 8ccc10ca1d0c4939 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page4_0.cs | 85 | 1c93872cd81c8d6c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page5_0.cs | 31 | aedad2abe58ee689 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page6_0.cs | 43 | eb508828c3d8deba |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page6_1.cs | 25 | b06be03eb33cab9a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page7_0.cs | 31 | cb7a15e4a6291ece |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page7_1.cs | 49 | 7808c6a83736e697 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter3/Page7_2.cs | 55 | 98733a36bde207c9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Chapter4Long.cs | 29 | ee1845c3bbd8dc56 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Chapter4Short.cs | 29 | 56c56133a5680b58 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page1_0.cs | 31 | 86482dea3758b4f5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page2_0.cs | 31 | 00e740a429bcf980 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page3_0.cs | 85 | c112eed91169f5a1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page4_0.cs | 85 | 53a41f2e38b795aa |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page5_0.cs | 31 | 1bb710b444e6909e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page6_0.cs | 43 | c08ddb4e7958d285 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page6_1.cs | 25 | 5003e2f56358a847 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page7_0.cs | 43 | 5fe6eda1a514db39 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page7_1.cs | 49 | 5c3e4db0c7ca7e65 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter4/Page7_2.cs | 55 | 3a8715eb69f67ae4 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Chapter5Long.cs | 32 | dfd80f7a9f4f245a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Chapter5Short.cs | 32 | 3225373d46757218 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page1_0.cs | 31 | 3e25b72e5088154f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page2_0.cs | 31 | f439518c0daf0980 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page3_0.cs | 85 | d847fc6da72b052f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page4_0.cs | 85 | 13decf10d60eb6be |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page5_0.cs | 31 | ad400ca8cb4af645 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page6_0.cs | 31 | be0c202a76b6fddf |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page7_0.cs | 31 | 134bc79983469118 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page7_1.cs | 39 | 666507fce38da0fb |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page8_0.cs | 39 | da5f9c7e40e738b5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page8_1.cs | 39 | 88e483b1103dd9f3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page9_0.cs | 43 | 65ac983b145a754f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page9_1.cs | 49 | c40c633efc173a55 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter5/Page9_2.cs | 55 | ce18c76f343a87c5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Chapter6Long.cs | 32 | faee0ed9f209605b |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Chapter6Short.cs | 32 | a5637677fdaac029 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page1_0.cs | 31 | 20148adbde6fb83e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page2_0.cs | 31 | d5d59b44e5c8d34a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page3_0.cs | 85 | bd023a636450b260 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page4_0.cs | 85 | 8706f570e91e26ce |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page5_0.cs | 31 | 7a72acc4dd97cee7 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page6_0.cs | 31 | 9554e81a0e8aeb92 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page7_0.cs | 31 | 094ba40723d2a0bc |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page7_1.cs | 39 | 1257c804c57e3773 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page8_0.cs | 39 | 47fef34edb8c8969 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page8_1.cs | 39 | a668e30d3cdddbda |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page9_0.cs | 49 | a14262b1a5f3d80e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page9_1.cs | 49 | 4e73b52d92a9b5fb |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter6/Page9_2.cs | 55 | 8a34c63ae75f0c66 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Chapter7Long.cs | 32 | 82785379797dc184 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Chapter7Short.cs | 32 | 78ef1c65e3573cd0 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page1_0.cs | 31 | e186498f28feac58 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page2_0.cs | 31 | 31aa23eb3c85a898 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page3_0.cs | 85 | ae01894a4330a176 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page4_0.cs | 85 | d8444dcc3da79ffd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page5_0.cs | 31 | a59c98b0d64abb86 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page6_0.cs | 31 | bbbc564285805edd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page7_0.cs | 31 | d07f755f738e8f11 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page7_1.cs | 39 | 02ed4e86f3dbdabd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page8_0.cs | 39 | 3c3f2f0e0f637492 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page8_1.cs | 39 | 93bf5950565fb707 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page9_0.cs | 39 | a741b3f7464351f2 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page9_1.cs | 43 | fad4e66aa6227437 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter7/Page9_2.cs | 34 | 4554c22fc9a7e8f2 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Chapter8Long.cs | 32 | bd5f990552ce9487 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Chapter8Short.cs | 32 | 3abc155af77a8426 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page1_0.cs | 31 | 5543a571790c2d4e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page2_0.cs | 85 | 729fd0e2a394120e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page3_0.cs | 31 | 4f4ad4cec1bf6a3e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page4_0.cs | 55 | b17c996a1fb3dd04 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page5_0.cs | 31 | 9a3ec9f5a4af8eb6 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page5_1.cs | 39 | 15d506e537c84d3b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page6_0.cs | 43 | b00d14de6afe6056 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page6_1.cs | 25 | 5c5200117fa7cf4c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page7_0.cs | 43 | 4a26a2084d4578ef |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page7_1.cs | 39 | 3f912f5e09ca7923 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page8_0.cs | 49 | f182720c68011eae |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page8_1.cs | 72 | e069b0b3a26cda23 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page9_0.cs | 31 | 19ce564f3bf246d1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page9_1.cs | 63 | 556e5a173d6ee427 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter8/Page9_2.cs | 34 | 640fa6adb3226eaa |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Chapter9Long.cs | 32 | 683f42e8ad8fab71 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Chapter9Short.cs | 32 | 59b18eace5afc048 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page1_0.cs | 31 | ca85df71a5b3e0e5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page2_0.cs | 85 | 7e2ba0dac944dc15 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page3_0.cs | 31 | 3a6ee3a518e8dca3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page4_0.cs | 55 | 72deb50a49dd47b8 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page5_0.cs | 31 | 5a2086a6a1ca7199 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page5_1.cs | 39 | 642959f8f9a62b1c |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page6_0.cs | 43 | 3e6b607444b54e74 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page6_1.cs | 25 | f2093d22e32c8ebd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page7_0.cs | 43 | 9d16058cab6445a3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page7_1.cs | 39 | 393067d1bc0eff56 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page8_0.cs | 49 | 9f43fef3ba45cbfa |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page8_1.cs | 72 | 27dbe72726014189 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page9_0.cs | 49 | 6e0e5fe50d0010b9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page9_1.cs | 63 | 3207cb90a15651cd |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.Chapter9/Page9_2.cs | 34 | 83812cbbe34bcfe1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/ChapterNeg1Long.cs | 29 | 56ce1507379fa915 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/ChapterNeg1Short.cs | 29 | b76f9557237fec61 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page1_0.cs | 31 | 72e91d3a4e7a7fde |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page2_0.cs | 31 | 1b875d6465fe8c88 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page3_0.cs | 85 | acb0dac97b6fd92d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page4_0.cs | 85 | 94934668d3a88c5b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page5_0.cs | 31 | e3362618efd56c95 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page6_0.cs | 43 | 34445e5ce583a147 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page6_1.cs | 25 | b5ab6bcf5d63346a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page7_0.cs | 85 | 05073f4a2eba899e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page7_1.cs | 43 | fa3b82224d3611b5 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo44.Uncoupled.ChapterNeg1/Page7_2.cs | 43 | dbce339a1b0d33b6 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled/Blocks.cs | 117 | d72634f1c5912336 |  | 0 | 46 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Chapter0Single.cs | 32 | 0e6bdf6bdab26e09 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page1_0.cs | 679 | 91906c04dc8aca65 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page3_0.cs | 85 | 5d7f1a67edaaea0e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page4_0.cs | 31 | db7f131299e70825 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page5_0.cs | 31 | 40e4386d4cb38f23 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page6_0.cs | 55 | c1e545ff8d196e13 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page7_0.cs | 31 | 46c60dd7d36f0431 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page7_1.cs | 39 | 2537c6fe24032173 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page8_0.cs | 43 | 8a26b62a57a04f01 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page8_1.cs | 25 | 2c2c5bc90630decf |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page9_0.cs | 31 | 5111bfc4cb94dbaf |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page9_1.cs | 49 | 198fdec41a455635 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter0/Page9_2.cs | 72 | 9e03caaf0ef23a6d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Chapter1Single.cs | 32 | 95efd130199265af |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page1_0.cs | 679 | e4f7e6a473491f35 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page3_0.cs | 85 | ff059c62fc28688a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page4_0.cs | 31 | 5a208dc2863a1152 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page5_0.cs | 31 | 0f892eb5ffce9f7b |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page6_0.cs | 55 | 00d6fa63ffee4fa1 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page7_0.cs | 31 | 2945f07996a60345 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page7_1.cs | 39 | 76097d05cb4153c9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page8_0.cs | 43 | 82c5c41c957d5527 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page8_1.cs | 25 | edaf793ef8b34041 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page9_0.cs | 43 | 58ad8ca883f03f18 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page9_1.cs | 49 | f15876463999c171 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Coupled.Chapter1/Page9_2.cs | 72 | 88aff9132dfb50d3 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled/Blocks.cs | 116 | 376a02115a873977 |  | 0 | 43 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Chapter0Single.cs | 29 | b66d2285c194e331 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page1_0.cs | 31 | 6a6b9e06f2cb8162 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page2_0.cs | 31 | 1f72b00ae6672efe |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page3_0.cs | 85 | 2d637162ff81a135 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page4_0.cs | 85 | dd2455369a317300 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page5_0.cs | 31 | 4f255c7e19eea227 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page6_0.cs | 43 | 717c17d9155dd961 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page6_1.cs | 25 | 9ae55f364b785e10 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page7_0.cs | 31 | 556c22359acae6fc |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page7_1.cs | 49 | bcde064f7a269cab |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter0/Page7_2.cs | 72 | 05f05dc7987e1687 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Chapter1Single.cs | 32 | 7742056195e50ce8 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page1_0.cs | 31 | a7ae85cc1b052f56 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page2_0.cs | 31 | 02bbcb4695f511da |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page3_0.cs | 85 | 3c426e96f41680fe |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page4_0.cs | 85 | ffebaeb2cfc3439e |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page5_0.cs | 31 | f41280fac82f0a41 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page6_0.cs | 31 | b5bf1c4f8311a891 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page7_0.cs | 31 | c9aa811b2c495612 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page7_1.cs | 39 | c32a05660e2a206f |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page8_0.cs | 39 | 8f8f2bd9d5ea2f63 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page8_1.cs | 39 | 3d352ce3e436a1bc |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page9_0.cs | 49 | fc74370af506378a |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page9_1.cs | 49 | 2daa827a4aa79829 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.BookBlocks.Stereo8.Uncoupled.Chapter1/Page9_2.cs | 55 | d23abbf16493f871 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_0Sub0.cs | 35 | e295138075efafa3 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_1Sub0.cs | 26 | 8739772a2d975506 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_1Sub1.cs | 35 | ff8a703f25b3562a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_2Sub0.cs | 26 | e72d54015b0ad7cd |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_2Sub1.cs | 35 | 4e9cad4accecfc2e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_3Sub1.cs | 24 | f7c83628eb4d17b7 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_3Sub2.cs | 27 | de4954d16034a292 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_3Sub3.cs | 35 | 0a89722d6712c9b4 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_4Sub1.cs | 24 | 8fa09c98a8b7b599 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_4Sub2.cs | 27 | 99f767790b5daf9d |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27_4Sub3.cs | 35 | 5e18ce6a67e46bbd |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27Class1.cs | 24 | 5161fa93b0e57b9e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27Class2.cs | 20 | 19522806e1f83b4e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27Class3.cs | 48 | db369429456e555f |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line1024X27Class4.cs | 29 | e405eb9f9071148b |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11_0Sub0.cs | 35 | f130e2dbb8b43fef |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11_1Sub0.cs | 26 | f0df0c639d981b6a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11_1Sub1.cs | 35 | 4721e290f8034c83 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11_2Sub1.cs | 24 | 3bed8063e55e277a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11_2Sub2.cs | 27 | e8eb95d8afe73147 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11_2Sub3.cs | 35 | 2aa5b57eb9c06aff |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11_3Sub1.cs | 24 | f780816fcf576341 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11_3Sub2.cs | 27 | 4cf287f2378f0457 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11_3Sub3.cs | 35 | 9d58f37ebf1f6976 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11Class1.cs | 20 | 906a216d4988dd3d |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11Class2.cs | 29 | d52d805b25e04e1a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X11Class3.cs | 29 | df1f971021049f85 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17_0Sub0.cs | 35 | c36cf26f1275c46e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17_1Sub0.cs | 26 | c36a2b1071a8831a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17_1Sub1.cs | 35 | 23453ff09b532dbe |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17_2Sub1.cs | 24 | a95d98bbfd29fc5e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17_2Sub2.cs | 27 | 705aeebead7010ee |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17_2Sub3.cs | 35 | 54f951ddbb9b8d7c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17_3Sub1.cs | 24 | 4b9e4a45c86a77d7 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17_3Sub2.cs | 27 | 0addbd399765303c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17_3Sub3.cs | 35 | 693e88991a85e01f |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17Class1.cs | 20 | a6d37197356a8652 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17Class2.cs | 29 | 188a52e04c1bb01a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X17Class3.cs | 29 | a6a4615f643ec295 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X4Class0.cs | 48 | 7f1bb35761ae6fef |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X4Sub0.cs | 20 | 8f912b8ea1c0e6b1 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X4Sub1.cs | 20 | beeeaba7245d720f |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X4Sub2.cs | 25 | f0501b3f76b3b9b6 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X4Sub3.cs | 29 | db14f244cc89c0a6 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X7_0Sub1.cs | 20 | dfbb1e263515d69b |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X7_0Sub2.cs | 25 | a2955f9af5bb2e64 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X7_0Sub3.cs | 29 | 0229bdf1ebea615d |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X7_1Sub1.cs | 20 | 9fcb42c59ce66648 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X7_1Sub2.cs | 25 | 04663c5c338d1ebd |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X7_1Sub3.cs | 29 | 3c5518727cb26339 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X7Class0.cs | 29 | 922abc25549a2674 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line128X7Class1.cs | 48 | bc40cd1db339574d |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_0Sub0.cs | 35 | 1147c52e0dc190c6 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_1Sub0.cs | 26 | ee4b89bb012682c2 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_1Sub1.cs | 35 | 5cecc13bb898d630 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_2Sub0.cs | 26 | ea40729bfe4ac636 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_2Sub1.cs | 35 | 7e2162863cca8a02 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_3Sub1.cs | 24 | 332cac644b543c7a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_3Sub2.cs | 27 | f476bcf847af6b05 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_3Sub3.cs | 35 | 9af4aa096b6def27 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_4Sub1.cs | 24 | c4139e069a1e4c19 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_4Sub2.cs | 27 | 146ac2a06642adeb |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27_4Sub3.cs | 35 | 0cee6c454a216487 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27Class1.cs | 24 | 49b371d6647691cc |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27Class2.cs | 20 | 4aa9fbe06877a66e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27Class3.cs | 48 | c74e7c0151a0b1e7 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line2048X27Class4.cs | 29 | 367f548dc7d1ed49 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4Class0.cs | 48 | ace0e61d571a449e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4LowClass0.cs | 48 | 6914856847e6c7e9 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4LowSub0.cs | 20 | 20d6da57ace34e5b |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4LowSub1.cs | 20 | 4bbea8acc9639a53 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4LowSub2.cs | 25 | 9b9555a97885a472 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4LowSub3.cs | 29 | 10efe73489e2539a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4Sub0.cs | 20 | d01a3278530e66f1 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4Sub1.cs | 20 | dd0561cf598f96d5 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4Sub2.cs | 25 | 3a9f6734dbc0016c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X4Sub3.cs | 29 | 4bdee6ebf1d0d00e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X7_0Sub1.cs | 20 | bf73ad000c89ba88 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X7_0Sub2.cs | 25 | 8cfe2f226f6250a2 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X7_0Sub3.cs | 29 | fb99dcbe20bfb24e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X7_1Sub1.cs | 20 | 8b75ce134043ab50 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X7_1Sub2.cs | 25 | 81c58d53ba78da4c |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X7_1Sub3.cs | 29 | 17e0de08f36a6fcc |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X7Class0.cs | 29 | 39a73ab500d8eec5 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line256X7Class1.cs | 48 | a12ac0cff24bbad6 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17_0Sub0.cs | 35 | 84e62a4153d2e6a6 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17_1Sub0.cs | 26 | 115a045912f6434a |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17_1Sub1.cs | 35 | 4bc60c67c54bb4a2 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17_2Sub1.cs | 24 | 0d4cb112a22f6acb |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17_2Sub2.cs | 27 | 4e3e6aac170f9425 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17_2Sub3.cs | 35 | 8cc670b4e2834a29 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17_3Sub1.cs | 24 | b91eead97b0141da |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17_3Sub2.cs | 27 | b33ebbc5696f2d22 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17_3Sub3.cs | 35 | b0737f666582c735 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17Class1.cs | 20 | 54b1a8193f1a78b3 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17Class2.cs | 29 | d0bbea0ff7dc039f |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.FloorBooks/Line512X17Class3.cs | 29 | 4aa1439a27e1d1e3 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Psyche/Psy11.cs | 104 | 53d1050ec69cdb5c |  | 0 | 29 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Psyche/Psy16.cs | 368 | d15be0d4003c17cc |  | 0 | 97 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Psyche/Psy22.cs | 10 | c711e1fef53474ce |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Psyche/Psy32.cs | 22 | 2e527491ae434b2f |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Psyche/Psy44.cs | 519 | 7b025ca06fb1d49d |  | 0 | 140 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Psyche/Psy8.cs | 193 | af11ee3ced0f8f12 |  | 0 | 53 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Psyche/PsyX.cs | 10 | be4601a6ee458cdd |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Residue/Residue16.cs | 93 | 5f606af5f29a8b3a |  | 0 | 40 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Residue/Residue44.cs | 344 | 09c185967abd9d92 |  | 0 | 165 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Residue/Residue8.cs | 59 | f7274ab679f4640a |  | 0 | 22 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OggVorbisEncoder.Setup.Templates.Residue/Shared.cs | 28 | 0c28c4eccbb9ec4d |  | 0 | 12 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/OnDocLoaded.cs | 1 | 7871ac8c15a59048 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/PluginsExtensions.cs | 22 | 1b614f68f68058fc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Properties/AssemblyInfo.cs | 5 | 3f2d9aef361f6bd4 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/RC4.cs | 59 | 38f29a87be58de92 |  | 3 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/RDAudioLoadResult.cs | 20 | b7c9cc220f54e26f |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/RDAudioLoadType.cs | 9 | 7d0cfe7587a03f92 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/API.cs | 468 | 2e494a8f8733354b |  | 10 | 26 | 0 | 0 | 18 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/Cell.cs | 94 | 2d10eda5647ee79e |  | 1 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/CellDictionaryRange.cs | 40 | 09ffaee7efbb8b01 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/CellRange.cs | 31 | 1fec818af21e233e |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/DataType.cs | 9 | 395e793d1e99cb32 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/DocTemplate.cs | 138 | ed5a7d2c87742ffd |  | 6 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/LangSection.cs | 11 | 4c01ab7fb0f80b8c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/Localization.cs | 87 | 6b8b4b9984693dbb |  | 0 | 4 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/LocalizationClient.cs | 143 | e24ebd75ac19966f |  | 5 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/LocalizationConfig.cs | 40 | 6b4e3932cc5cf66c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/RangeDirection.cs | 7 | 2cd695b5dac296ac |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/Reader.cs | 361 | c184c54996be8330 |  | 11 | 12 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/Settings.cs | 160 | f52b2d9a492b6dba |  | 5 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/SpreadsheetData.cs | 41 | 13d4969d1ae099a9 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/VariableType.cs | 9 | 1e80e4a98f40896e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA.GoogleDoc/WorksheetTemplate.cs | 17 | 3759b2638561851d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_Ad_EditorUIController.cs | 77 | c35dbc330aed373d |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/SA_Config.cs | 4 | 31e3880c2354d324 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_DataConverter.cs | 47 | bd45203718c4993b |  | 2 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_EditorAd.cs | 155 | 74dc209d547691cf |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_EditorTesting.cs | 34 | dcd27ad174880b10 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/SA_EditorTestingSceneController.cs | 49 | a5dc9f7c316e45bc | FixedUpdate:30 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_Gameobject.cs | 9 | 3e66a361d00da431 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_IdFactory.cs | 33 | 9a895dbf5bfc2ef6 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_ModulesInfo.cs | 71 | b619882a431998fe |  | 1 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_NonMonoSingleton.cs | 16 | 7ef535a718e202a8 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_PrefabAsyncLoader.cs | 37 | fd55d3c6adaa38d2 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SA_Singleton.cs | 59 | e6dfc5d0b4162b77 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/SA_UIHightDependence.cs | 78 | 6289b90275cf40b0 | Update:38 | 0 | 4 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/SA_UIWidthDependence.cs | 78 | 7da2eda61b1ae60e | Update:38 | 0 | 4 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/SA_WWWTextureLoader.cs | 56 | a8d0bb6beb22afaa |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/ScrollingUVs_Layers.cs | 19 | 4e0c689fbde3f647 | LateUpdate:11 | 0 | 1 | 0 | 1 | 2 | 0 |
| Assembly-CSharp-firstpass/SimpleRotator.cs | 15 | 11a65937669f4367 | Update:11 | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/SpaceShipTrail.cs | 5 | 37b28acd7f88ae24 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/Splasher.cs | 64 | 46b76b53edf5effc | Update:31 | 1 | 1 | 3 | 0 | 2 | 0 |
| Assembly-CSharp-firstpass/TimeScale.cs | 12 | 792e9011c6dc9ae0 | Update:8 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI/ExtensionsToggle.cs | 200 | d4ed2415d0a45caa |  | 0 | 2 | 1 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI/ExtensionsToggleGroup.cs | 123 | 9cda41315c6a86d8 |  | 2 | 4 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI/ReturnKeyTriggersButton.cs | 39 | 6c6b372e29e691c0 |  | 0 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/BezierPath.cs | 220 | 861ab91e075df6d1 |  | 7 | 7 | 0 | 0 | 6 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/CableCurve.cs | 246 | 840f59b738c97fbf |  | 3 | 6 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/CardExpanding2D.cs | 100 | f567bfa51c9ca528 | Update:49 | 0 | 10 | 1 | 0 | 6 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/CardExpanding3D.cs | 192 | 74b9649d768f883c | Update:76 | 6 | 22 | 25 | 0 | 4 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/CardPopup2D.cs | 72 | 8082d03a6bc1a5d4 | Update:32 | 0 | 5 | 8 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/CardStack2D.cs | 104 | fdbcc86606f2bf27 | Update:65 | 3 | 4 | 4 | 0 | 3 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/Circle.cs | 96 | 5b416f17413e7460 |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/CLZF2.cs | 201 | 8edc89d1679debd4 |  | 10 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ContentScrollSnapHorizontal.cs | 578 | 9ccb36e293d41de0 | Update:495 | 14 | 18 | 18 | 0 | 9 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/CurvedLayout.cs | 73 | c43e9518699fd981 |  | 1 | 3 | 5 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/DragCorrector.cs | 24 | 44a34cfd63e87c95 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ExtensionMethods.cs | 42 | f30992404f68e0c4 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FancyCell.cs | 35 | 461eae7012f7df5c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FancyCellGroup.cs | 47 | 59f36aa388b16e8a |  | 3 | 1 | 1 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FancyGridView.cs | 90 | 2748c62a6401b6b1 |  | 0 | 3 | 1 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FancyGridViewCell.cs | 21 | 8d52d5b22f332870 |  | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FancyGridViewContext.cs | 18 | 0452f075e899c81b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FancyScrollRect.cs | 179 | 366d83f7b933cd81 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FancyScrollRectCell.cs | 27 | 0dd34b5bdb752874 |  | 0 | 1 | 1 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FancyScrollRectContext.cs | 10 | 6f0e3797d70a3c74 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FancyScrollView.cs | 134 | 290d50a7b12d84a0 |  | 2 | 4 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/FlowLayoutGroup.cs | 456 | f1a4689b299ed60c |  | 5 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/HorizontalScrollSnap.cs | 280 | a7910b0c17967acd | Update:21 | 2 | 5 | 5 | 0 | 4 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/IFancyCellGroupContext.cs | 10 | 6d7c436535007922 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/IFancyGridViewContext.cs | 10 | 2c40b18fcf03d087 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/IFancyScrollRectContext.cs | 10 | 9376a80eab00352d |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/InputFieldEnterSubmit.cs | 39 | ba5144c954f978ce |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/IScrollSnap.cs | 12 | 9ad998cd5f28de77 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/MeshCreator.cs | 24 | 21f04692cca9fd65 |  | 1 | 5 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/MinMaxValues.cs | 49 | 8fe1238072f5544b |  | 0 | 1 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/MovementDirection.cs | 9 | 46a405aea95a33d6 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/MovementType.cs | 8 | fed26641915555dc |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/NonDrawingGraphic.cs | 19 | 4cc73f6322cbf22c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/NullContext.cs | 5 | 79ec0e51be148156 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/PaginationManager.cs | 103 | 9d53c0d3378bb148 |  | 2 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/PPIViewer.cs | 23 | 2805a6d4536968df |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/RadialLayout.cs | 91 | 1bd3f48bfb7b7ab0 |  | 2 | 2 | 7 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ReadOnlyAttribute.cs | 5 | 6a363e38d1954164 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/RectTransformExtension.cs | 14 | 4047ed99a2f2ff07 |  | 0 | 2 | 1 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ResetSelectableHighlight.cs | 24 | 51318b1909fec184 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ResolutionMode.cs | 8 | 5e580865ae96fdcb |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollConflictManager.cs | 159 | affe3dd841808800 |  | 4 | 0 | 2 | 0 | 5 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollDirection.cs | 7 | 0ebcf258306d101e |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/Scroller.cs | 541 | 25a6dd7b63db78c2 | Update:436 | 0 | 3 | 2 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollPositionController.cs | 349 | fedbd233f037f2ee | Update:227 | 0 | 2 | 2 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollRectEx.cs | 121 | bbe3806e82940694 |  | 2 | 0 | 1 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollRectExtensions.cs | 14 | d459608b969d171d |  | 0 | 2 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollRectLinker.cs | 34 | e59cad25246978a9 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollRectTweener.cs | 138 | 2f32f4e496960a1e |  | 1 | 7 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollSnap.cs | 451 | 4967566f4ee30412 | LateUpdate:275 | 5 | 2 | 14 | 0 | 7 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollSnapBase.cs | 630 | 33536be179a0a17c |  | 6 | 4 | 9 | 0 | 14 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ScrollSnapScrollbarHelper.cs | 49 | ef12d31ccde95008 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/SelectableScaler.cs | 96 | f26dd45da98f5236 |  | 2 | 0 | 7 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/SetPropertyUtility.cs | 34 | 21f982615fda29c2 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/ShaderLibrary.cs | 24 | 5825cb8e2b373e56 |  | 0 | 1 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/SuperellipsePoints.cs | 74 | d5d7db3fab5b0463 | Update:42 | 1 | 3 | 0 | 1 | 2 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/TableLayoutGroup.cs | 254 | 8c04a0dd3b392099 |  | 9 | 3 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/TestCompression.cs | 12 | 10405c37daf37baa | Update:9 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/TileSizeFitter.cs | 139 | 1de921e734fe9850 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/Triangulator.cs | 148 | da47580391ab8c3e |  | 6 | 7 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UI_InfiniteScroll.cs | 184 | 01e345c899356f3b |  | 5 | 1 | 9 | 0 | 14 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UI_MagneticInfiniteScroll.cs | 188 | ed6a4b3ab7357f7f | Update:60 | 2 | 3 | 1 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UI_ScrollRectOcclusion.cs | 181 | 3449590bde99e12c | LateUpdate:167 | 3 | 1 | 3 | 0 | 14 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UI_TweenScale.cs | 74 | 5c7f51cc0f50f146 |  | 1 | 0 | 5 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UIExtensionMethods.cs | 54 | 118f0a08f6a292e0 |  | 1 | 0 | 1 | 0 | 3 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UIExtensionsInputManager.cs | 58 | b55b07d3de959ace |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UIHighlightable.cs | 223 | 802c7bd43eb70bd5 |  | 0 | 0 | 0 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UIHorizontalScroller.cs | 258 | a506c30831a0bfe8 | Update:188 | 5 | 14 | 4 | 0 | 9 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UILineConnector.cs | 76 | 58db9aafea4b457c | Update:29 | 5 | 5 | 2 | 0 | 4 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UILineRenderer.cs | 503 | 8581a2872c9c10d8 |  | 6 | 36 | 14 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UIPrimitiveBase.cs | 387 | 200edb507a313eb6 |  | 7 | 5 | 1 | 1 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UIScrollToSelection.cs | 257 | dce154c83e276970 | LateUpdate:127 | 2 | 3 | 6 | 0 | 5 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UIScrollToSelectionXY.cs | 74 | 1733602da4e06581 | Update:28 | 0 | 0 | 1 | 0 | 3 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UISelectableExtension.cs | 77 | 3e1179c67c62cf22 | Update:49 | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/UIVerticalScroller.cs | 254 | 332ff5beed28981c | Update:184 | 5 | 14 | 4 | 0 | 9 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions/VerticalScrollSnap.cs | 280 | 7e6e6c0798278394 | Update:21 | 2 | 5 | 5 | 0 | 4 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions.EasingCore/Ease.cs | 36 | 4de8498f76745e92 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions.EasingCore/Easing.cs | 230 | f5a6b3bcfe24ed3c |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityEngine.UI.Extensions.EasingCore/EasingFunction.cs | 3 | cb9dbdef2e974698 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/AAMode.cs | 12 | ea17a40692502302 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Antialiasing.cs | 139 | baa6d736516d25c8 | OnRenderImage:88 | 0 | 0 | 0 | 0 | 0 | 12 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Bloom.cs | 329 | 481ff1230670d0d5 | OnRenderImage:116 | 2 | 19 | 0 | 0 | 1 | 45 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/BloomAndFlares.cs | 257 | 907c2e4274862911 | OnRenderImage:95 | 3 | 19 | 0 | 0 | 1 | 28 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/BloomOptimized.cs | 96 | f1518ec1f58ecd2b | OnRenderImage:61 | 1 | 2 | 0 | 0 | 0 | 11 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/BloomScreenBlendMode.cs | 7 | 42c4c9b164654a5b |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Blur.cs | 74 | e21a4e9771df0556 | OnRenderImage:58 | 1 | 3 | 0 | 1 | 0 | 7 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/BlurOptimized.cs | 84 | abd44324ac148444 | OnRenderImage:50 | 1 | 2 | 0 | 0 | 0 | 11 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/CameraMotionBlur.cs | 353 | 39ed782e0cac26eb | OnRenderImage:142 | 0 | 3 | 15 | 0 | 8 | 20 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ColorCorrectionCurves.cs | 204 | f57d762c2bdef7e8 | OnRenderImage:144 | 1 | 20 | 0 | 0 | 1 | 6 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ColorCorrectionLookup.cs | 142 | acb06a5aa3bcbb0f | OnRenderImage:124 | 6 | 5 | 0 | 0 | 0 | 2 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ColorCorrectionRamp.cs | 16 | d34d32a937fc61b5 | OnRenderImage:11 | 0 | 0 | 0 | 2 | 0 | 1 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ContrastEnhance.cs | 64 | 3363b83bd3d858dd | OnRenderImage:36 | 0 | 2 | 0 | 0 | 0 | 14 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ContrastStretch.cs | 171 | 9be0af4a30db2266 | OnRenderImage:132 | 3 | 7 | 0 | 4 | 0 | 8 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/CreaseShading.cs | 76 | cb0ddae0ad6ee16a | OnRenderImage:41 | 1 | 2 | 0 | 0 | 0 | 14 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/DepthOfFieldDeprecated.cs | 451 | 28e78624f5e93ef1 | OnRenderImage:195 | 1 | 20 | 3 | 0 | 1 | 41 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/EdgeDetection.cs | 96 | 0f048d28795813b9 | OnRenderImage:80 | 0 | 3 | 0 | 0 | 2 | 2 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Fisheye.cs | 41 | 714bd91362a154e8 | OnRenderImage:29 | 0 | 1 | 0 | 0 | 0 | 2 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/GlobalFog.cs | 123 | b27fca01956391bb | OnRenderImage:45 | 0 | 3 | 8 | 0 | 1 | 1 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Grayscale.cs | 19 | 073b9eadd9666a3e | OnRenderImage:13 | 0 | 0 | 0 | 3 | 0 | 1 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/HDRBloomMode.cs | 8 | bcf6d0f3b3e12558 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ImageEffectBase.cs | 41 | ef24225f79e27436 |  | 0 | 1 | 0 | 1 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ImageEffects.cs | 34 | f8f9aa8aedfee04a |  | 0 | 1 | 0 | 0 | 0 | 5 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/LensflareStyle34.cs | 8 | df3a3741bebe3b87 |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/MotionBlur.cs | 49 | 1587b6798db22ed0 | OnRenderImage:27 | 0 | 1 | 0 | 3 | 0 | 7 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/NoiseAndGrain.cs | 158 | a88735d7f2b74041 | OnRenderImage:61 | 2 | 6 | 1 | 0 | 0 | 7 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/NoiseAndScratches.cs | 127 | ebd6c32d8815a2ca | OnRenderImage:108 | 0 | 5 | 0 | 3 | 0 | 1 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/PostEffectsBase.cs | 216 | 4c1545b955f8741f |  | 1 | 2 | 0 | 2 | 1 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/PostEffectsHelper.cs | 172 | 42ec37223af409ec | OnRenderImage:10 | 3 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Quads.cs | 102 | 3ba07abf659b1528 |  | 4 | 15 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ScreenOverlay.cs | 53 | 2a368d0ff30b0526 | OnRenderImage:40 | 0 | 1 | 0 | 0 | 0 | 2 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ScreenSpaceAmbientObscurance.cs | 95 | dfdadee8d8635b55 | OnRenderImage:52 | 1 | 3 | 0 | 0 | 1 | 14 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/ScreenSpaceAmbientOcclusion.cs | 155 | 425673eeea4d4fc2 | OnRenderImage:101 | 0 | 6 | 0 | 1 | 4 | 10 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/SepiaTone.cs | 13 | 1ba3b32ac0b7da6f | OnRenderImage:9 | 0 | 0 | 0 | 1 | 0 | 1 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/SunShafts.cs | 134 | 1a4427cc9f26c252 | OnRenderImage:63 | 1 | 9 | 1 | 0 | 4 | 14 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/TiltShift.cs | 79 | dba9225ea88e73e1 | OnRenderImage:50 | 0 | 0 | 0 | 0 | 0 | 5 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Tonemapping.cs | 225 | 182aba88d0d0bec4 | OnRenderImage:131 | 5 | 6 | 0 | 0 | 0 | 18 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Triangles.cs | 95 | 0b635db1fb274e6c |  | 4 | 13 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/TweakMode34.cs | 7 | e711bc7e5ec1aebe |  | 0 | 0 | 0 | 0 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Twirl.cs | 19 | 509d555d5acfe3ac | OnRenderImage:15 | 0 | 2 | 0 | 1 | 0 | 0 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/VignetteAndChromaticAberration.cs | 110 | 84f51ba0a65a112e | OnRenderImage:57 | 1 | 3 | 0 | 0 | 0 | 14 |
| Assembly-CSharp-firstpass/UnityStandardAssets.ImageEffects/Vortex.cs | 19 | 4d855478ee3d5304 | OnRenderImage:15 | 0 | 2 | 0 | 1 | 0 | 0 |
| Assembly-CSharp-firstpass/UVHeightGenerator.cs | 71 | a5a3a1e21a5f26c5 |  | 5 | 2 | 2 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/UVLayoutGenerator.cs | 75 | 0e6d48b92fee18e0 |  | 4 | 5 | 2 | 0 | 1 | 0 |
| Assembly-CSharp-firstpass/WWWDocLoader.cs | 41 | c7b812c8a0f51d00 |  | 0 | 2 | 0 | 0 | 0 | 0 |
| DOTween/DOTweenComponent.cs | 259 | 4591f132846211e1 | Update:61, LateUpdate:91, FixedUpdate:99 | 7 | 1 | 2 | 0 | 0 | 0 |

## 합계

```text
{'Assembly-CSharp': 1235, 'OnRenderImage': 378, 'callbacks': 1128, 'Assembly-CSharp-firstpass': 1294, 'DOTween': 1}
```
