from pathlib import Path
p=Path('AutoTest.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('case "filterstate":', '''#if DEV || AUTOTEST
                case "freefps":
                    FreeFpsProbe.Command(arg); return true;
#endif
                case "filterstate":''',1)
p.write_text(s,encoding='utf-8-sig')
p=Path('StutterFix.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('EndDisplayTransition();\n            Try(FrameGen.Shutdown);','EndDisplayTransition();\n#if DEV || AUTOTEST\n            Try(FreeFpsProbe.Restore);\n#endif\n            Try(FrameGen.Shutdown);',1);p.write_text(s,encoding='utf-8-sig')
p=Path('StutterFix.csproj');s=p.read_text();s=s.replace('<Compile Include="AutoTest.cs" />','<Compile Include="AutoTest.cs" />\n    <Compile Include="FreeFpsProbe.cs" />');p.write_text(s)
