namespace StutterFix
{
    // 한 소스로 두 가지 DLL 을 만든다.
    //   개발자용 (기본, DEV 정의): 끊김 기록, 단계/그리기/함수 측정, Ctrl+F5 다시 불러오기, F6~F9 진단
    //   플레이어용 (dotnet build -p:Edition=Player): 효과가 측정된 수정만. 측정 패치를 걸지 않고 로그도 거의 안 남긴다.
    // 순수 진단 모듈은 일반 플레이어용에서 컴파일 제외한다. 자동 시험용은 별도 AUTOTEST 빌드에만 포함한다.
    internal static class Edition
    {
#if DEV
        internal const bool Dev = true;
        internal const string Name = "개발자용";
#else
        internal const bool Dev = false;
        internal const string Name = "플레이어용";
#endif
        // (측정용, 배포 안 함) 플레이어용에서도 자동 시험(AutoTest)을 켠 빌드: -p:AutoTestBuild=1
#if DEV || AUTOTEST
        internal const bool AutoTest = true;
#else
        internal const bool AutoTest = false;
#endif
    }
}
