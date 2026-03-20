// C# 9 の record / init アクセサを Unity (.NET Standard 2.1) で使用するためのポリフィル。
// .NET 5+ のランタイムには組み込みで存在するが、Unity の Mono ランタイムには存在しないため定義する。
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
